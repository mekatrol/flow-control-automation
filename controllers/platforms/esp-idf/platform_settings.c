#include "platform/settings.h"

#include <string.h>

#include "driver/gpio.h"
#include "driver/sdspi_host.h"
#include "driver/spi_common.h"
#include "esp_mac.h"
#include "esp_random.h"
#include "freertos/FreeRTOS.h"
#include "freertos/task.h"
#include "psa/crypto.h"
#include "sdkconfig.h"
#include "sdmmc_cmd.h"

/* Two fixed slots retain the previous generation until its replacement verifies. */
enum
{
    SETTINGS_SPI_HOST                     = SPI3_HOST,
    SETTINGS_SLOT_COUNT                   = 2,
    SETTINGS_SECTORS_PER_SLOT             = 8,
    SETTINGS_SECTOR_SIZE                  = 512,
    SETTINGS_SLOT_BYTES                   = SETTINGS_SECTORS_PER_SLOT * SETTINGS_SECTOR_SIZE,
    SETTINGS_RECORD_CAPACITY              = 2048,
    SETTINGS_KEY_BYTES                    = 32,
    SETTINGS_NONCE_BYTES                  = 12,
    SETTINGS_TAG_BYTES                    = 16,
    SETTINGS_KEY_HEX_CHARACTERS           = SETTINGS_KEY_BYTES * 2,
    SETTINGS_CARD_INITIALIZATION_ATTEMPTS = 3,
    SETTINGS_CARD_RETRY_DELAY_MS          = 100,
};

/* Slot ownership and authenticated-encryption metadata are written with each generation. */
static const uint8_t SLOT_MAGIC[8] = {'F', 'C', 'S', 'D', '0', '1', '\0', '\0'};
static const uint8_t KEY_CONTEXT[] = {'f', 'l', 'o', 'w', '-', 's', 'e', 't', 't', 'i', 'n', 'g', 's'};

typedef struct
{
    uint8_t magic[8];
    uint32_t generation;
    uint32_t bootstrap_size;
    uint32_t settings_size;
    uint8_t nonce[SETTINGS_NONCE_BYTES];
    uint8_t tag[SETTINGS_TAG_BYTES];
} settings_slot_header_t;

typedef struct
{
    uint8_t bootstrap[SETTINGS_RECORD_CAPACITY];
    size_t bootstrap_size;
    uint8_t settings[SETTINGS_RECORD_CAPACITY];
    size_t settings_size;
} settings_slot_payload_t;

typedef struct
{
    sdmmc_card_t card;
    sdspi_dev_handle_t device;
    settings_slot_payload_t current;
    settings_slot_payload_t staged;
    uint32_t generation;
    uint32_t active_slot;
    uint32_t first_reserved_sector;
    uint8_t key[SETTINGS_KEY_BYTES];
    bool is_ready;
    bool is_media_initializable;
    bool is_device_initialized;
    int chip_select_gpio;
} platform_settings_context_t;

static platform_settings_context_t settings_context;

/* Probes the card with bounded retries and converts low-level failures into stable redacted reasons. */
static platform_settings_result_t get_card_initialization_result(const sdmmc_host_t *host)
{
    esp_err_t result = ESP_FAIL;
    for (uint32_t attempt = 0; attempt < SETTINGS_CARD_INITIALIZATION_ATTEMPTS; attempt++)
    {
        result = sdmmc_card_init(host, &settings_context.card);
        if (result == ESP_OK)
        {
            return PLATFORM_SETTINGS_READY;
        }
        if (attempt + 1 < SETTINGS_CARD_INITIALIZATION_ATTEMPTS)
        {
            /* Some cards need another complete low-speed idle sequence after an interrupted or noisy response. */
            vTaskDelay(pdMS_TO_TICKS(SETTINGS_CARD_RETRY_DELAY_MS));
        }
    }
    if (result == ESP_ERR_TIMEOUT)
    {
        return PLATFORM_SETTINGS_CARD_INITIALIZATION_TIMEOUT;
    }
    if (result == ESP_ERR_INVALID_RESPONSE)
    {
        return PLATFORM_SETTINGS_CARD_INITIALIZATION_INVALID_RESPONSE;
    }
    if (result == ESP_ERR_INVALID_CRC)
    {
        return PLATFORM_SETTINGS_CARD_INITIALIZATION_CRC_FAILED;
    }
    if (result == ESP_ERR_NOT_SUPPORTED)
    {
        return PLATFORM_SETTINGS_CARD_INITIALIZATION_UNSUPPORTED;
    }
    return PLATFORM_SETTINGS_CARD_INITIALIZATION_FAILED;
}

/* Imports the derived key only for one operation so the crypto subsystem owns no persistent copy. */
static bool get_crypto_key(const uint8_t key[SETTINGS_KEY_BYTES], mbedtls_svc_key_id_t *key_id)
{
    psa_key_attributes_t attributes = PSA_KEY_ATTRIBUTES_INIT;
    psa_set_key_usage_flags(&attributes, PSA_KEY_USAGE_ENCRYPT | PSA_KEY_USAGE_DECRYPT);
    psa_set_key_algorithm(&attributes, PSA_ALG_GCM);
    psa_set_key_type(&attributes, PSA_KEY_TYPE_AES);
    psa_set_key_bits(&attributes, SETTINGS_KEY_BYTES * 8);
    const psa_status_t result = psa_import_key(&attributes, key, SETTINGS_KEY_BYTES, key_id);
    psa_reset_key_attributes(&attributes);
    return result == PSA_SUCCESS;
}

/* Converts one hexadecimal character and rejects invalid key material. */
static bool get_hex_nibble(char character, uint8_t *value)
{
    if (character >= '0' && character <= '9')
    {
        *value = (uint8_t)(character - '0');
        return true;
    }
    if (character >= 'a' && character <= 'f')
    {
        *value = (uint8_t)(character - 'a' + 10);
        return true;
    }
    if (character >= 'A' && character <= 'F')
    {
        *value = (uint8_t)(character - 'A' + 10);
        return true;
    }
    return false;
}

/* Derives a device-bound encryption key from local provisioning material and the factory identity. */
static bool derive_settings_key(uint8_t key[SETTINGS_KEY_BYTES])
{
    const char *configured_key = CONFIG_CONTROLLER_SETTINGS_MASTER_KEY_HEX;
    if (strlen(configured_key) != SETTINGS_KEY_HEX_CHARACTERS)
    {
        return false;
    }
    uint8_t provisioned_key[SETTINGS_KEY_BYTES];
    for (size_t index = 0; index < sizeof(provisioned_key); index++)
    {
        uint8_t high;
        uint8_t low;
        if (!get_hex_nibble(configured_key[index * 2], &high) || !get_hex_nibble(configured_key[index * 2 + 1], &low))
        {
            return false;
        }
        provisioned_key[index] = (uint8_t)((high << 4) | low);
    }
    uint8_t factory_mac[6];
    if (esp_read_mac(factory_mac, ESP_MAC_WIFI_STA) != ESP_OK)
    {
        memset(provisioned_key, 0, sizeof(provisioned_key));
        return false;
    }
    uint8_t derivation_input[SETTINGS_KEY_BYTES + sizeof(factory_mac) + sizeof(KEY_CONTEXT)];
    memcpy(derivation_input, provisioned_key, sizeof(provisioned_key));
    memcpy(derivation_input + sizeof(provisioned_key), factory_mac, sizeof(factory_mac));
    memcpy(derivation_input + sizeof(provisioned_key) + sizeof(factory_mac), KEY_CONTEXT, sizeof(KEY_CONTEXT));
    size_t key_size       = 0;
    const bool is_success = psa_crypto_init() == PSA_SUCCESS &&
                            psa_hash_compute(PSA_ALG_SHA_256, derivation_input, sizeof(derivation_input), key, SETTINGS_KEY_BYTES,
                                             &key_size) == PSA_SUCCESS &&
                            key_size == SETTINGS_KEY_BYTES;
    memset(derivation_input, 0, sizeof(derivation_input));
    memset(provisioned_key, 0, sizeof(provisioned_key));
    return is_success;
}

/* Gets the first sector of one explicitly reserved controller storage slot. */
static size_t get_slot_sector(const platform_settings_context_t *context, uint32_t slot)
{
    return context->first_reserved_sector + slot * SETTINGS_SECTORS_PER_SLOT;
}

/* Decrypts and authenticates one candidate slot into bounded owned buffers. */
static bool read_slot(platform_settings_context_t *context, uint32_t slot, settings_slot_payload_t *payload, uint32_t *generation,
                      bool *is_blank)
{
    uint8_t storage[SETTINGS_SLOT_BYTES];
    if (sdmmc_read_sectors(&context->card, storage, get_slot_sector(context, slot), SETTINGS_SECTORS_PER_SLOT) != ESP_OK)
    {
        return false;
    }
    *is_blank = true;
    for (size_t index = 0; index < sizeof(storage); index++)
    {
        if (storage[index] != 0 && storage[index] != UINT8_MAX)
        {
            *is_blank = false;
            break;
        }
    }
    const settings_slot_header_t *header = (const settings_slot_header_t *)storage;
    if (memcmp(header->magic, SLOT_MAGIC, sizeof(header->magic)) != 0 || header->bootstrap_size > SETTINGS_RECORD_CAPACITY ||
        header->settings_size > SETTINGS_RECORD_CAPACITY)
    {
        return false;
    }
    const size_t plaintext_size = header->bootstrap_size + header->settings_size;
    if (sizeof(*header) + plaintext_size > sizeof(storage))
    {
        return false;
    }
    uint8_t ciphertext[SETTINGS_RECORD_CAPACITY * 2 + SETTINGS_TAG_BYTES];
    memcpy(ciphertext, storage + sizeof(*header), plaintext_size);
    memcpy(ciphertext + plaintext_size, header->tag, sizeof(header->tag));
    uint8_t plaintext[SETTINGS_RECORD_CAPACITY * 2];
    size_t decrypted_size       = 0;
    mbedtls_svc_key_id_t key_id = MBEDTLS_SVC_KEY_ID_INIT;
    const bool is_decrypted =
        get_crypto_key(context->key, &key_id) &&
        psa_aead_decrypt(key_id, PSA_ALG_GCM, header->nonce, sizeof(header->nonce), storage,
                         offsetof(settings_slot_header_t, tag), ciphertext, plaintext_size + SETTINGS_TAG_BYTES, plaintext,
                         sizeof(plaintext), &decrypted_size) == PSA_SUCCESS &&
        decrypted_size == plaintext_size;
    (void)psa_destroy_key(key_id);
    memset(ciphertext, 0, sizeof(ciphertext));
    if (!is_decrypted)
    {
        memset(plaintext, 0, sizeof(plaintext));
        return false;
    }
    memcpy(payload->bootstrap, plaintext, header->bootstrap_size);
    payload->bootstrap_size = header->bootstrap_size;
    memcpy(payload->settings, plaintext + header->bootstrap_size, header->settings_size);
    payload->settings_size = header->settings_size;
    *generation            = header->generation;
    memset(plaintext, 0, sizeof(plaintext));
    return true;
}

/* Encrypts, writes, reads back, and authenticates the inactive transaction slot. */
static settings_store_result_t write_slot(platform_settings_context_t *context, uint32_t slot,
                                          const settings_slot_payload_t *payload, uint32_t generation)
{
    uint8_t storage[SETTINGS_SLOT_BYTES] = {0};
    settings_slot_header_t *header       = (settings_slot_header_t *)storage;
    memcpy(header->magic, SLOT_MAGIC, sizeof(header->magic));
    header->generation     = generation;
    header->bootstrap_size = payload->bootstrap_size;
    header->settings_size  = payload->settings_size;
    esp_fill_random(header->nonce, sizeof(header->nonce));
    uint8_t plaintext[SETTINGS_RECORD_CAPACITY * 2];
    memcpy(plaintext, payload->bootstrap, payload->bootstrap_size);
    memcpy(plaintext + payload->bootstrap_size, payload->settings, payload->settings_size);
    const size_t plaintext_size = payload->bootstrap_size + payload->settings_size;
    uint8_t ciphertext[SETTINGS_RECORD_CAPACITY * 2 + SETTINGS_TAG_BYTES];
    size_t encrypted_size       = 0;
    mbedtls_svc_key_id_t key_id = MBEDTLS_SVC_KEY_ID_INIT;
    const bool is_encrypted     = get_crypto_key(context->key, &key_id) &&
                              psa_aead_encrypt(key_id, PSA_ALG_GCM, header->nonce, sizeof(header->nonce), storage,
                                               offsetof(settings_slot_header_t, tag), plaintext, plaintext_size, ciphertext,
                                               sizeof(ciphertext), &encrypted_size) == PSA_SUCCESS &&
                              encrypted_size == plaintext_size + SETTINGS_TAG_BYTES;
    (void)psa_destroy_key(key_id);
    memset(plaintext, 0, sizeof(plaintext));
    if (!is_encrypted)
    {
        return SETTINGS_STORE_IO_ERROR;
    }
    memcpy(storage + sizeof(*header), ciphertext, plaintext_size);
    memcpy(header->tag, ciphertext + plaintext_size, sizeof(header->tag));
    memset(ciphertext, 0, sizeof(ciphertext));
    if (sdmmc_write_sectors(&context->card, storage, get_slot_sector(context, slot), SETTINGS_SECTORS_PER_SLOT) != ESP_OK)
    {
        return SETTINGS_STORE_IO_ERROR;
    }
    settings_slot_payload_t verified = {0};
    uint32_t verified_generation     = 0;
    bool is_blank                    = false;
    if (!read_slot(context, slot, &verified, &verified_generation, &is_blank) || verified_generation != generation ||
        verified.bootstrap_size != payload->bootstrap_size || verified.settings_size != payload->settings_size ||
        memcmp(verified.bootstrap, payload->bootstrap, payload->bootstrap_size) != 0 ||
        memcmp(verified.settings, payload->settings, payload->settings_size) != 0)
    {
        return SETTINGS_STORE_IO_ERROR;
    }
    return SETTINGS_STORE_OK;
}

/* Copies a current record through the abstract store read contract. */
static settings_store_result_t get_record(const platform_settings_context_t *context, const uint8_t *source, size_t source_size,
                                          void *record, size_t capacity, size_t *size)
{
    if (!context->is_ready)
    {
        return SETTINGS_STORE_UNAVAILABLE;
    }
    if (source_size == 0)
    {
        return SETTINGS_STORE_MISSING;
    }
    if (source_size > capacity)
    {
        return SETTINGS_STORE_CORRUPT;
    }
    memcpy(record, source, source_size);
    *size = source_size;
    return SETTINGS_STORE_OK;
}

/* Reads current bootstrap metadata. */
static settings_store_result_t get_bootstrap(void *opaque, void *record, size_t capacity, size_t *size)
{
    platform_settings_context_t *context = opaque;
    return get_record(context, context->current.bootstrap, context->current.bootstrap_size, record, capacity, size);
}

/* Reads current typed settings. */
static settings_store_result_t get_settings(void *opaque, void *record, size_t capacity, size_t *size)
{
    platform_settings_context_t *context = opaque;
    return get_record(context, context->current.settings, context->current.settings_size, record, capacity, size);
}

/* Stages one bounded record without modifying the durable active slot. */
static settings_store_result_t stage_record(platform_settings_context_t *context, uint8_t *destination, size_t *destination_size,
                                            const void *record, size_t size)
{
    if (!context->is_ready)
    {
        return SETTINGS_STORE_UNAVAILABLE;
    }
    if (size > SETTINGS_RECORD_CAPACITY)
    {
        return SETTINGS_STORE_FULL;
    }
    memcpy(destination, record, size);
    *destination_size = size;
    return SETTINGS_STORE_OK;
}

/* Stages bootstrap metadata for the next atomic slot commit. */
static settings_store_result_t stage_bootstrap(void *opaque, const void *record, size_t size)
{
    platform_settings_context_t *context = opaque;
    return stage_record(context, context->staged.bootstrap, &context->staged.bootstrap_size, record, size);
}

/* Stages typed settings for the next atomic slot commit. */
static settings_store_result_t stage_settings(void *opaque, const void *record, size_t size)
{
    platform_settings_context_t *context = opaque;
    return stage_record(context, context->staged.settings, &context->staged.settings_size, record, size);
}

/* Publishes a complete encrypted generation to the inactive slot. */
static settings_store_result_t commit(void *opaque)
{
    platform_settings_context_t *context = opaque;
    settings_slot_payload_t next         = context->current;
    if (context->staged.bootstrap_size > 0)
    {
        memcpy(next.bootstrap, context->staged.bootstrap, context->staged.bootstrap_size);
        next.bootstrap_size = context->staged.bootstrap_size;
    }
    if (context->staged.settings_size > 0)
    {
        memcpy(next.settings, context->staged.settings, context->staged.settings_size);
        next.settings_size = context->staged.settings_size;
    }
    const uint32_t target_slot           = (context->active_slot + 1) % SETTINGS_SLOT_COUNT;
    const uint32_t generation            = context->generation + 1;
    const settings_store_result_t result = write_slot(context, target_slot, &next, generation);
    if (result == SETTINGS_STORE_OK)
    {
        context->current     = next;
        context->generation  = generation;
        context->active_slot = target_slot;
        memset(&context->staged, 0, sizeof(context->staged));
    }
    return result;
}

/* Discards plaintext staging buffers after a failed or cancelled transaction. */
static void abort_transaction(void *opaque)
{
    platform_settings_context_t *context = opaque;
    memset(&context->staged, 0, sizeof(context->staged));
}

/* Initializes encrypted raw SD settings storage without waiting for card insertion. */
platform_settings_result_t platform_settings_initialize(const settings_storage_config_t *config, settings_store_t *store)
{
    memset(&settings_context, 0, sizeof(settings_context));
    settings_context.first_reserved_sector = config->first_reserved_sector;
    *store                                 = (settings_store_t){.get_bootstrap   = get_bootstrap,
                                                                .stage_bootstrap = stage_bootstrap,
                                                                .get_settings    = get_settings,
                                                                .stage_settings  = stage_settings,
                                                                .commit          = commit,
                                                                .abort           = abort_transaction,
                                                                .context         = &settings_context};
    const gpio_config_t detect_config      = {
             .pin_bit_mask = UINT64_C(1) << config->card_detect_gpio, .mode = GPIO_MODE_INPUT, .pull_up_en = GPIO_PULLUP_ENABLE};
    if (config->first_reserved_sector == 0)
    {
        return PLATFORM_SETTINGS_DISABLED;
    }
    if (gpio_config(&detect_config) != ESP_OK)
    {
        return PLATFORM_SETTINGS_DETECT_CONFIGURATION_FAILED;
    }
    if (gpio_get_level(config->card_detect_gpio) != 0)
    {
        return PLATFORM_SETTINGS_CARD_ABSENT;
    }
    if (!derive_settings_key(settings_context.key))
    {
        return PLATFORM_SETTINGS_KEY_INVALID;
    }
    const spi_bus_config_t bus = {.mosi_io_num     = config->mosi_gpio,
                                  .miso_io_num     = config->miso_gpio,
                                  .sclk_io_num     = config->clock_gpio,
                                  .quadwp_io_num   = -1,
                                  .quadhd_io_num   = -1,
                                  .max_transfer_sz = SETTINGS_SLOT_BYTES};
    if (spi_bus_initialize(SETTINGS_SPI_HOST, &bus, SPI_DMA_CH_AUTO) != ESP_OK)
    {
        return PLATFORM_SETTINGS_SPI_INITIALIZATION_FAILED;
    }
    sdmmc_host_t host                   = SDSPI_HOST_DEFAULT();
    host.slot                           = SETTINGS_SPI_HOST;
    host.max_freq_khz                   = config->spi_clock_hz / 1000;
    sdspi_device_config_t device_config = SDSPI_DEVICE_CONFIG_DEFAULT();
    device_config.host_id               = SETTINGS_SPI_HOST;
    device_config.gpio_cs               = config->chip_select_gpio;
    if (sdspi_host_init_device(&device_config, &host.slot) != ESP_OK)
    {
        return PLATFORM_SETTINGS_DEVICE_INITIALIZATION_FAILED;
    }
    settings_context.device                      = host.slot;
    settings_context.is_device_initialized       = true;
    settings_context.chip_select_gpio            = config->chip_select_gpio;
    const platform_settings_result_t card_result = get_card_initialization_result(&host);
    if (card_result != PLATFORM_SETTINGS_READY)
    {
        return card_result;
    }
    if (settings_context.card.csd.capacity <
        settings_context.first_reserved_sector + SETTINGS_SLOT_COUNT * SETTINGS_SECTORS_PER_SLOT)
    {
        return PLATFORM_SETTINGS_CARD_TOO_SMALL;
    }
    settings_slot_payload_t candidates[SETTINGS_SLOT_COUNT] = {0};
    uint32_t generations[SETTINGS_SLOT_COUNT]               = {0};
    bool blank[SETTINGS_SLOT_COUNT]                         = {false};
    bool valid[SETTINGS_SLOT_COUNT];
    for (uint32_t slot = 0; slot < SETTINGS_SLOT_COUNT; slot++)
    {
        valid[slot] = read_slot(&settings_context, slot, &candidates[slot], &generations[slot], &blank[slot]);
    }
    if (valid[0] || valid[1])
    {
        settings_context.active_slot = valid[1] && (!valid[0] || generations[1] > generations[0]) ? 1 : 0;
        settings_context.current     = candidates[settings_context.active_slot];
        settings_context.generation  = generations[settings_context.active_slot];
    }
    else if (!blank[0] || !blank[1])
    {
        /* Nonblank unauthenticated data is foreign or corrupt and must not be overwritten. */
        settings_context.is_media_initializable = true;
        return PLATFORM_SETTINGS_MEDIA_INVALID;
    }
    settings_context.is_ready = true;
    return PLATFORM_SETTINGS_READY;
}

/* Clears and verifies only the reserved settings slots after explicit user authorization. */
bool platform_settings_initialize_media(void)
{
    if (!settings_context.is_media_initializable)
    {
        return false;
    }
    uint8_t cleared_slot[SETTINGS_SLOT_BYTES] = {0};
    uint8_t verified_slot[SETTINGS_SLOT_BYTES];
    for (uint32_t slot = 0; slot < SETTINGS_SLOT_COUNT; slot++)
    {
        const size_t first_sector = get_slot_sector(&settings_context, slot);
        /* Limit destructive writes to the configured settings range and verify the media accepted them. */
        if (sdmmc_write_sectors(&settings_context.card, cleared_slot, first_sector, SETTINGS_SECTORS_PER_SLOT) != ESP_OK ||
            sdmmc_read_sectors(&settings_context.card, verified_slot, first_sector, SETTINGS_SECTORS_PER_SLOT) != ESP_OK ||
            memcmp(cleared_slot, verified_slot, sizeof(cleared_slot)) != 0)
        {
            memset(verified_slot, 0, sizeof(verified_slot));
            return false;
        }
    }
    memset(verified_slot, 0, sizeof(verified_slot));
    settings_context.is_media_initializable = false;
    return true;
}

/* Releases the active SD device and leaves chip select inactive before a software reboot. */
void platform_settings_prepare_reboot(void)
{
    if (!settings_context.is_device_initialized)
    {
        return;
    }
    /* Detach the protocol driver before reset so a powered card cannot remain selected across the warm boot. */
    (void)sdspi_host_remove_device(settings_context.device);
    (void)spi_bus_free(SETTINGS_SPI_HOST);
    (void)gpio_set_direction(settings_context.chip_select_gpio, GPIO_MODE_OUTPUT);
    (void)gpio_set_level(settings_context.chip_select_gpio, 1);
    settings_context.is_device_initialized = false;
}

/* Gets a stable redacted reason name for platform settings initialization. */
const char *platform_settings_get_result_name(platform_settings_result_t result)
{
    static const char *const names[] = {"ready",
                                        "disabled",
                                        "card_detect_configuration_failed",
                                        "card_absent",
                                        "encryption_key_invalid",
                                        "spi_initialization_failed",
                                        "sd_device_initialization_failed",
                                        "card_initialization_failed",
                                        "card_initialization_timeout",
                                        "card_initialization_invalid_response",
                                        "card_initialization_crc_failed",
                                        "card_initialization_unsupported",
                                        "card_too_small",
                                        "media_invalid_or_foreign"};
    return result <= PLATFORM_SETTINGS_MEDIA_INVALID ? names[result] : "unknown";
}
