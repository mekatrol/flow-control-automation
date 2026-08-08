#ifndef CONTROLLER_FLOW_SHA256_H
#define CONTROLLER_FLOW_SHA256_H

#include <stddef.h>
#include <stdint.h>

/* Computes the SHA-256 digest of one bounded byte sequence into the caller's 32-byte buffer. */
void flow_sha256(const uint8_t *data, size_t size, uint8_t digest[32]);

#endif
