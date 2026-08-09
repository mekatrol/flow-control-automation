#include "platform/auth.h"

#include <string.h>

#include "psa/crypto.h"

/* The per-controller provisioned credential is a full HMAC-SHA-256 key. */
enum
{
    PROVISIONED_KEY_SIZE       = 32,
    PROVISIONED_HEX_CHARACTERS = PROVISIONED_KEY_SIZE * 2,
};

static psa_key_id_t protocol_key_id;

/* Decodes one hexadecimal character without accepting whitespace or separators. */
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

/* Imports one per-controller provisioned credential into volatile PSA key storage. */
bool platform_auth_initialize(const char *provisioned_key_hex)
{
    platform_auth_deinitialize();

    if (provisioned_key_hex == NULL || strlen(provisioned_key_hex) != PROVISIONED_HEX_CHARACTERS)
    {
        return false;
    }
    uint8_t provisioned_key[PROVISIONED_KEY_SIZE];

    for (size_t index = 0; index < sizeof(provisioned_key); index++)
    {
        uint8_t high = 0;
        uint8_t low  = 0;

        if (!get_hex_nibble(provisioned_key_hex[index * 2], &high) || !get_hex_nibble(provisioned_key_hex[index * 2 + 1], &low))
        {
            memset(provisioned_key, 0, sizeof(provisioned_key));
            return false;
        }
        provisioned_key[index] = (uint8_t)((high << 4U) | low);
    }

    if (psa_crypto_init() != PSA_SUCCESS)
    {
        memset(provisioned_key, 0, sizeof(provisioned_key));
        return false;
    }
    psa_key_attributes_t attributes = PSA_KEY_ATTRIBUTES_INIT;
    psa_set_key_type(&attributes, PSA_KEY_TYPE_HMAC);
    psa_set_key_bits(&attributes, sizeof(provisioned_key) * 8U);
    psa_set_key_algorithm(&attributes, PSA_ALG_HMAC(PSA_ALG_SHA_256));
    psa_set_key_usage_flags(&attributes, PSA_KEY_USAGE_SIGN_MESSAGE);
    const psa_status_t result = psa_import_key(&attributes, provisioned_key, sizeof(provisioned_key), &protocol_key_id);
    psa_reset_key_attributes(&attributes);
    /* Erase the decoded copy after PSA has imported it into opaque volatile storage. */
    memset(provisioned_key, 0, sizeof(provisioned_key));
    return result == PSA_SUCCESS;
}

/* Calculates HMAC-SHA-256 without exposing the imported device-bound key. */
bool platform_auth_get_hmac(void * /* context */, const uint8_t *message, size_t message_size,
                            uint8_t tag[CONTROLLER_AUTH_TAG_SIZE])
{
    size_t tag_size = 0;
    return protocol_key_id != 0 && message != NULL && tag != NULL &&
           psa_mac_compute(protocol_key_id, PSA_ALG_HMAC(PSA_ALG_SHA_256), message, message_size, tag, CONTROLLER_AUTH_TAG_SIZE,
                           &tag_size) == PSA_SUCCESS &&
           tag_size == CONTROLLER_AUTH_TAG_SIZE;
}

/* Fills a bounded nonce or identifier buffer from the platform CSPRNG. */
bool platform_auth_get_random(void * /* context */, uint8_t *output, size_t size)
{
    return output != NULL && size > 0 && psa_generate_random(output, size) == PSA_SUCCESS;
}

/* Destroys the volatile protocol key when provisioning changes or runtime stops. */
void platform_auth_deinitialize(void)
{
    if (protocol_key_id != 0)
    {
        psa_destroy_key(protocol_key_id);
        protocol_key_id = 0;
    }
}
