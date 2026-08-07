#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#include "controller/auth.h"

/* Initializes a volatile device-bound HMAC key from one provisioned hexadecimal credential. */
bool platform_auth_initialize(const char *provisioned_key_hex);

/* Calculates HMAC-SHA-256 without exposing the imported device-bound key. */
bool platform_auth_get_hmac(void *context, const uint8_t *message, size_t message_size, uint8_t tag[CONTROLLER_AUTH_TAG_SIZE]);

/* Fills a bounded nonce or identifier buffer from the platform CSPRNG. */
bool platform_auth_get_random(void *context, uint8_t *output, size_t size);

/* Destroys the volatile protocol key when provisioning changes or runtime stops. */
void platform_auth_deinitialize(void);
