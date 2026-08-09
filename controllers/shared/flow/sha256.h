#ifndef CONTROLLER_FLOW_SHA256_H
#define CONTROLLER_FLOW_SHA256_H

/*
 * Purpose: Declare the portable SHA-256 integrity primitive shared by flow
 * artifact preparation, durable staging, and debug snapshot publication.
 *
 * Why this contract exists: Artifacts and snapshots cross bounded chunked
 * transports, so consumers must detect missing, mixed, reordered, or corrupted
 * bytes before accepting the assembled object. Digest integrity does not prove
 * caller identity; FCP authentication and session ownership do that separately.
 *
 * How callers use it: Pass one complete caller-owned byte range and a 32-byte
 * destination. The implementation streams fixed blocks without allocation and
 * writes the standard SHA-256 digest, giving host tests and firmware identical
 * content-verification behavior.
 */

#include <stddef.h>
#include <stdint.h>

/*
 * What: Computes standard SHA-256 for data[0..size) and writes all 32 digest bytes.
 * Why: Artifact and snapshot consumers compare this value before trusting chunked content.
 * How: The implementation processes fixed-size blocks without allocation; data and digest must be valid caller-owned buffers.
 */
void flow_sha256(const uint8_t *data, size_t size, uint8_t digest[32]);

#endif
