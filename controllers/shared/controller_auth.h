#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/* Authentication bounds prevent peer traffic from growing runtime state. */
enum
{
    CONTROLLER_AUTH_NONCE_SIZE       = 16,
    CONTROLLER_AUTH_TAG_SIZE         = 32,
    CONTROLLER_AUTH_SESSION_CAPACITY = 4,
};

typedef bool (*controller_auth_hmac_t)(void *context, const uint8_t *message, size_t message_size,
                                       uint8_t tag[CONTROLLER_AUTH_TAG_SIZE]);
typedef bool (*controller_auth_random_t)(void *context, uint8_t *output, size_t size);

typedef struct
{
    controller_auth_hmac_t get_hmac;
    controller_auth_random_t get_random;
    void *context;
    uint64_t challenge_lifetime_ms;
    uint64_t session_lifetime_ms;
    uint8_t maximum_attempts;
} controller_auth_config_t;

typedef struct
{
    bool is_allocated;
    bool is_authenticated;
    uint16_t peer;
    uint32_t id;
    uint8_t client_nonce[CONTROLLER_AUTH_NONCE_SIZE];
    uint8_t device_nonce[CONTROLLER_AUTH_NONCE_SIZE];
    uint64_t expires_at_ms;
    uint64_t receive_sequence;
    uint64_t transmit_sequence;
    uint8_t failed_attempts;
} controller_auth_session_t;

typedef struct
{
    uint32_t challenge_count;
    uint32_t authenticated_count;
    uint32_t failed_proof_count;
    uint32_t replay_count;
    uint32_t expired_count;
    uint32_t saturation_count;
} controller_auth_health_t;

typedef struct
{
    controller_auth_config_t config;
    controller_auth_session_t sessions[CONTROLLER_AUTH_SESSION_CAPACITY];
    controller_auth_health_t health;
} controller_auth_t;

/* Initializes bounded challenge/session state with platform cryptographic callbacks. */
bool controller_auth_init(controller_auth_t *auth, const controller_auth_config_t *config);

/* Allocates a challenge bound to one peer and returns its session ID and device nonce. */
bool controller_auth_create_challenge(controller_auth_t *auth, uint16_t peer,
                                      const uint8_t client_nonce[CONTROLLER_AUTH_NONCE_SIZE], uint64_t now_ms,
                                      uint32_t *session_id, uint8_t device_nonce[CONTROLLER_AUTH_NONCE_SIZE]);

/* Calculates the client proof expected for one pending challenge. */
bool controller_auth_get_proof(controller_auth_t *auth, uint16_t peer, uint32_t session_id,
                               uint8_t proof[CONTROLLER_AUTH_TAG_SIZE]);

/* Verifies a challenge proof in constant time and promotes the bounded session. */
bool controller_auth_verify_proof(controller_auth_t *auth, uint16_t peer, uint32_t session_id,
                                  const uint8_t proof[CONTROLLER_AUTH_TAG_SIZE], uint64_t now_ms);

/* Verifies an increasing authenticated request sequence and its body tag. */
bool controller_auth_verify_request(controller_auth_t *auth, uint16_t peer, uint32_t session_id, uint64_t sequence,
                                    uint8_t operation, const uint8_t *body, size_t body_size,
                                    const uint8_t tag[CONTROLLER_AUTH_TAG_SIZE], uint64_t now_ms);

/* Signs the next authenticated response sequence and returns that sequence and tag. */
bool controller_auth_sign_response(controller_auth_t *auth, uint16_t peer, uint32_t session_id, uint8_t operation,
                                   const uint8_t *body, size_t body_size, uint64_t now_ms, uint64_t *sequence,
                                   uint8_t tag[CONTROLLER_AUTH_TAG_SIZE]);

/* Invalidates one peer session without affecting other bounded peers. */
void controller_auth_close(controller_auth_t *auth, uint16_t peer, uint32_t session_id);

/* Gets authentication attempt, replay, expiry, and saturation counters. */
controller_auth_health_t controller_auth_get_health(const controller_auth_t *auth);
