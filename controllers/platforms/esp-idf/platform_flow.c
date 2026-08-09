#include "platform/flow.h"

#include <string.h>

#include "nvs.h"
#include "nvs_flash.h"
#include "psa/crypto.h"

/* One versioned NVS blob makes committed metadata and bytes visible atomically. */
enum
{
    FLOW_RECORD_VERSION = 1,
};

static const char FLOW_NAMESPACE[]  = "fcp_flow";
static const char FLOW_RECORD_KEY[] = "generation";

typedef struct
{
    uint32_t version;
    controller_flow_metadata_t metadata;
    uint8_t artifact[CONTROLLER_FLOW_ARTIFACT_CAPACITY];
} platform_flow_record_t;

static nvs_handle_t flow_handle;

/* Initializes the default NVS partition and repairs only documented layout exhaustion states. */
static bool initialize_persistence(void)
{
    esp_err_t result = nvs_flash_init();

    if (result == ESP_ERR_NVS_NO_FREE_PAGES || result == ESP_ERR_NVS_NEW_VERSION_FOUND)
    {
        /* NVS cannot recover either layout error in place, so rebuild its dedicated partition. */
        if (nvs_flash_erase() != ESP_OK)
        {
            return false;
        }
        result = nvs_flash_init();
    }
    return result == ESP_OK;
}

/* Loads one complete versioned generation and rejects malformed blob lengths. */
static bool load_flow(void * /* context */, controller_flow_metadata_t *metadata, uint8_t *artifact, size_t capacity)
{
    platform_flow_record_t record;
    size_t record_size = sizeof(record);

    if (flow_handle == 0 || nvs_get_blob(flow_handle, FLOW_RECORD_KEY, &record, &record_size) != ESP_OK ||
        record_size != sizeof(record) || record.version != FLOW_RECORD_VERSION || record.metadata.size > capacity ||
        record.metadata.size > sizeof(record.artifact))
    {
        return false;
    }
    *metadata = record.metadata;
    memcpy(artifact, record.artifact, metadata->size);
    return true;
}

/* Atomically replaces the durable generation after copying only bounded artifact bytes. */
static bool commit_flow(void * /* context */, const controller_flow_metadata_t *metadata, const uint8_t *artifact)
{
    if (flow_handle == 0 || metadata == NULL || artifact == NULL || metadata->size > CONTROLLER_FLOW_ARTIFACT_CAPACITY)
    {
        return false;
    }
    platform_flow_record_t record = {.version = FLOW_RECORD_VERSION, .metadata = *metadata};
    memcpy(record.artifact, artifact, metadata->size);
    return nvs_set_blob(flow_handle, FLOW_RECORD_KEY, &record, sizeof(record)) == ESP_OK && nvs_commit(flow_handle) == ESP_OK;
}

/* Removes the durable generation only after an explicit service-level state check. */
static bool remove_flow(void * /* context */)
{
    return flow_handle != 0 && nvs_erase_key(flow_handle, FLOW_RECORD_KEY) == ESP_OK && nvs_commit(flow_handle) == ESP_OK;
}

/* Opens the dedicated durable flow namespace and returns atomic store callbacks. */
bool platform_flow_initialize(controller_flow_store_t *store)
{
    if (store == NULL || !initialize_persistence() || nvs_open(FLOW_NAMESPACE, NVS_READWRITE, &flow_handle) != ESP_OK)
    {
        return false;
    }
    *store = (controller_flow_store_t){.load = load_flow, .commit = commit_flow, .remove = remove_flow};
    return true;
}

/* Calculates SHA-256 for staged and recovered artifact integrity checks. */
bool platform_flow_get_digest(void * /* context */, const uint8_t *data, size_t size, uint8_t digest[CONTROLLER_FLOW_DIGEST_SIZE])
{
    size_t digest_size = 0;
    return data != NULL && size > 0 && digest != NULL && psa_crypto_init() == PSA_SUCCESS &&
           psa_hash_compute(PSA_ALG_SHA_256, data, size, digest, CONTROLLER_FLOW_DIGEST_SIZE, &digest_size) == PSA_SUCCESS &&
           digest_size == CONTROLLER_FLOW_DIGEST_SIZE;
}

/* Accepts only the implemented opaque schema while preserving non-empty bounded artifacts. */
bool platform_flow_is_artifact_valid(void * /* context */, const controller_flow_metadata_t *metadata, const uint8_t *artifact)
{
    const uint32_t supported_schema = 1;
    return metadata != NULL && artifact != NULL && metadata->artifact_schema == supported_schema && metadata->size > 0;
}
