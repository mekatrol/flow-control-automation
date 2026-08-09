#include "flow/sha256.h"

#include <string.h>

enum
{
    SHA256_BLOCK_BYTES  = 64,
    SHA256_DIGEST_WORDS = 8,
    SHA256_ROUNDS       = 64,
};

static const uint32_t ROUND_CONSTANTS[SHA256_ROUNDS] = {
    0x428a2f98U, 0x71374491U, 0xb5c0fbcfU, 0xe9b5dba5U, 0x3956c25bU, 0x59f111f1U, 0x923f82a4U, 0xab1c5ed5U,
    0xd807aa98U, 0x12835b01U, 0x243185beU, 0x550c7dc3U, 0x72be5d74U, 0x80deb1feU, 0x9bdc06a7U, 0xc19bf174U,
    0xe49b69c1U, 0xefbe4786U, 0x0fc19dc6U, 0x240ca1ccU, 0x2de92c6fU, 0x4a7484aaU, 0x5cb0a9dcU, 0x76f988daU,
    0x983e5152U, 0xa831c66dU, 0xb00327c8U, 0xbf597fc7U, 0xc6e00bf3U, 0xd5a79147U, 0x06ca6351U, 0x14292967U,
    0x27b70a85U, 0x2e1b2138U, 0x4d2c6dfcU, 0x53380d13U, 0x650a7354U, 0x766a0abbU, 0x81c2c92eU, 0x92722c85U,
    0xa2bfe8a1U, 0xa81a664bU, 0xc24b8b70U, 0xc76c51a3U, 0xd192e819U, 0xd6990624U, 0xf40e3585U, 0x106aa070U,
    0x19a4c116U, 0x1e376c08U, 0x2748774cU, 0x34b0bcb5U, 0x391c0cb3U, 0x4ed8aa4aU, 0x5b9cca4fU, 0x682e6ff3U,
    0x748f82eeU, 0x78a5636fU, 0x84c87814U, 0x8cc70208U, 0x90befffaU, 0xa4506cebU, 0xbef9a3f7U, 0xc67178f2U};

/* Rotates one SHA-256 word right by the specified bounded bit count. */
static uint32_t rotate_right(uint32_t value, uint8_t bits)
{
    return (value >> bits) | (value << (32U - bits));
}

/* Expands and compresses one complete SHA-256 block into the running state. */
static void transform(uint32_t state[SHA256_DIGEST_WORDS], const uint8_t block[SHA256_BLOCK_BYTES])
{
    uint32_t words[SHA256_ROUNDS];

    for (size_t index = 0; index < 16U; index++)
    {
        const size_t offset = index * 4U;
        words[index]        = ((uint32_t)block[offset] << 24U) | ((uint32_t)block[offset + 1U] << 16U) |
                       ((uint32_t)block[offset + 2U] << 8U) | block[offset + 3U];
    }

    for (size_t index = 16U; index < SHA256_ROUNDS; index++)
    {
        const uint32_t low =
            rotate_right(words[index - 15U], 7U) ^ rotate_right(words[index - 15U], 18U) ^ (words[index - 15U] >> 3U);
        const uint32_t high =
            rotate_right(words[index - 2U], 17U) ^ rotate_right(words[index - 2U], 19U) ^ (words[index - 2U] >> 10U);
        words[index] = words[index - 16U] + low + words[index - 7U] + high;
    }
    uint32_t a = state[0];
    uint32_t b = state[1];
    uint32_t c = state[2];
    uint32_t d = state[3];
    uint32_t e = state[4];
    uint32_t f = state[5];
    uint32_t g = state[6];
    uint32_t h = state[7];

    for (size_t index = 0; index < SHA256_ROUNDS; index++)
    {
        const uint32_t sum1     = rotate_right(e, 6U) ^ rotate_right(e, 11U) ^ rotate_right(e, 25U);
        const uint32_t choice   = (e & f) ^ ((~e) & g);
        const uint32_t first    = h + sum1 + choice + ROUND_CONSTANTS[index] + words[index];
        const uint32_t sum0     = rotate_right(a, 2U) ^ rotate_right(a, 13U) ^ rotate_right(a, 22U);
        const uint32_t majority = (a & b) ^ (a & c) ^ (b & c);
        const uint32_t second   = sum0 + majority;
        h                       = g;
        g                       = f;
        f                       = e;
        e                       = d + first;
        d                       = c;
        c                       = b;
        b                       = a;
        a                       = first + second;
    }
    state[0] += a;
    state[1] += b;
    state[2] += c;
    state[3] += d;
    state[4] += e;
    state[5] += f;
    state[6] += g;
    state[7] += h;
}

/* Computes the SHA-256 digest of one bounded byte sequence into the caller's 32-byte buffer. */
void flow_sha256(const uint8_t *data, size_t size, uint8_t digest[32])
{
    uint32_t state[SHA256_DIGEST_WORDS] = {0x6a09e667U, 0xbb67ae85U, 0x3c6ef372U, 0xa54ff53aU,
                                           0x510e527fU, 0x9b05688cU, 0x1f83d9abU, 0x5be0cd19U};
    size_t offset                       = 0;

    while (size - offset >= SHA256_BLOCK_BYTES)
    {
        transform(state, &data[offset]);
        offset += SHA256_BLOCK_BYTES;
    }
    uint8_t final_blocks[SHA256_BLOCK_BYTES * 2U] = {0};
    const size_t remainder                        = size - offset;

    if (remainder > 0U)
    {
        memcpy(final_blocks, &data[offset], remainder);
    }
    final_blocks[remainder] = 0x80U;
    const size_t final_size = remainder < 56U ? SHA256_BLOCK_BYTES : SHA256_BLOCK_BYTES * 2U;
    const uint64_t bit_size = (uint64_t)size * 8U;

    for (size_t index = 0; index < 8U; index++)
    {
        final_blocks[final_size - 1U - index] = (uint8_t)(bit_size >> (index * 8U));
    }
    transform(state, final_blocks);

    if (final_size > SHA256_BLOCK_BYTES)
    {
        transform(state, &final_blocks[SHA256_BLOCK_BYTES]);
    }

    for (size_t index = 0; index < SHA256_DIGEST_WORDS; index++)
    {
        digest[index * 4U]      = (uint8_t)(state[index] >> 24U);
        digest[index * 4U + 1U] = (uint8_t)(state[index] >> 16U);
        digest[index * 4U + 2U] = (uint8_t)(state[index] >> 8U);
        digest[index * 4U + 3U] = (uint8_t)state[index];
    }
}
