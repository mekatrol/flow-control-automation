#include "controller/auth.h"

#include <string.h>

/* Domain-separated message limits cover the largest authenticated protocol body. */
enum
{
    AUTH_DOMAIN_SIZE          = 8,
    AUTH_PROOF_MESSAGE_SIZE   = AUTH_DOMAIN_SIZE + 2 + 4 + (CONTROLLER_AUTH_NONCE_SIZE * 2),
    AUTH_ENVELOPE_PREFIX_SIZE = AUTH_DOMAIN_SIZE + 2 + 4 + 8 + 1,
    AUTH_MAXIMUM_BODY_SIZE    = 197,
    AUTH_MAXIMUM_MESSAGE_SIZE = AUTH_ENVELOPE_PREFIX_SIZE + AUTH_MAXIMUM_BODY_SIZE,
};

static const uint8_t PROOF_DOMAIN[AUTH_DOMAIN_SIZE]    = {'F', 'C', 'P', '1', 'P', 'R', 'O', 'F'};
static const uint8_t REQUEST_DOMAIN[AUTH_DOMAIN_SIZE]  = {'F', 'C', 'P', '1', 'R', 'E', 'Q', 'T'};
static const uint8_t RESPONSE_DOMAIN[AUTH_DOMAIN_SIZE] = {'F', 'C', 'P', '1', 'R', 'E', 'S', 'P'};

/* Writes one little-endian 16-bit value into a cryptographic transcript. */
static void put_u16(uint8_t *output, uint16_t value)
{
    output[0] = (uint8_t)value;
    output[1] = (uint8_t)(value >> 8U);
}

/* Writes one little-endian 32-bit value into a cryptographic transcript. */
static void put_u32(uint8_t *output, uint32_t value)
{
    for (size_t index = 0; index < sizeof(value); index++)
    {
        output[index] = (uint8_t)(value >> (index * 8U));
    }
}

/* Writes one little-endian 64-bit value into a cryptographic transcript. */
static void put_u64(uint8_t *output, uint64_t value)
{
    for (size_t index = 0; index < sizeof(value); index++)
    {
        output[index] = (uint8_t)(value >> (index * 8U));
    }
}

/* Compares authentication tags without data-dependent early exit. */
static bool is_tag_equal(const uint8_t *left, const uint8_t *right)
{
    uint8_t difference = 0;
    for (size_t index = 0; index < CONTROLLER_AUTH_TAG_SIZE; index++)
    {
        difference |= left[index] ^ right[index];
    }
    return difference == 0;
}

/* Finds an allocated session only when both peer and random ID match. */
static controller_auth_session_t *get_session(controller_auth_t *auth, uint16_t peer, uint32_t session_id)
{
    for (size_t index = 0; index < CONTROLLER_AUTH_SESSION_CAPACITY; index++)
    {
        controller_auth_session_t *session = &auth->sessions[index];
        if (session->is_allocated && session->peer == peer && session->id == session_id)
        {
            return session;
        }
    }
    return NULL;
}

/* Expires stale slots before allocation or verification to keep capacity reusable. */
static void expire_sessions(controller_auth_t *auth, uint64_t now_ms)
{
    for (size_t index = 0; index < CONTROLLER_AUTH_SESSION_CAPACITY; index++)
    {
        if (auth->sessions[index].is_allocated && now_ms >= auth->sessions[index].expires_at_ms)
        {
            auth->sessions[index] = (controller_auth_session_t){0};
            auth->health.expired_count++;
        }
    }
}

/* Initializes bounded challenge/session state with platform cryptographic callbacks. */
bool controller_auth_init(controller_auth_t *auth, const controller_auth_config_t *config)
{
    if (auth == NULL || config == NULL || config->get_hmac == NULL || config->get_random == NULL ||
        config->challenge_lifetime_ms == 0 || config->session_lifetime_ms == 0 || config->maximum_attempts == 0)
    {
        return false;
    }
    *auth        = (controller_auth_t){0};
    auth->config = *config;
    return true;
}

/* Allocates a challenge bound to one peer and returns its session ID and device nonce. */
bool controller_auth_create_challenge(controller_auth_t *auth, uint16_t peer,
                                      const uint8_t client_nonce[CONTROLLER_AUTH_NONCE_SIZE], uint64_t now_ms,
                                      uint32_t *session_id, uint8_t device_nonce[CONTROLLER_AUTH_NONCE_SIZE])
{
    if (auth == NULL || client_nonce == NULL || session_id == NULL || device_nonce == NULL)
    {
        return false;
    }
    expire_sessions(auth, now_ms);
    controller_auth_session_t *session = NULL;
    for (size_t index = 0; index < CONTROLLER_AUTH_SESSION_CAPACITY; index++)
    {
        if (!auth->sessions[index].is_allocated)
        {
            session = &auth->sessions[index];
            break;
        }
    }
    uint8_t random_data[sizeof(uint32_t) + CONTROLLER_AUTH_NONCE_SIZE];
    if (session == NULL || !auth->config.get_random(auth->config.context, random_data, sizeof(random_data)))
    {
        auth->health.saturation_count++;
        return false;
    }
    uint32_t id = 0;
    for (size_t index = 0; index < sizeof(id); index++)
    {
        id |= (uint32_t)random_data[index] << (index * 8U);
    }
    if (id == 0 || get_session(auth, peer, id) != NULL)
    {
        auth->health.saturation_count++;
        return false;
    }
    *session = (controller_auth_session_t){
        .is_allocated = true, .peer = peer, .id = id, .expires_at_ms = now_ms + auth->config.challenge_lifetime_ms};
    (void)memcpy(session->client_nonce, client_nonce, CONTROLLER_AUTH_NONCE_SIZE);
    (void)memcpy(session->device_nonce, &random_data[sizeof(uint32_t)], CONTROLLER_AUTH_NONCE_SIZE);
    *session_id = id;
    (void)memcpy(device_nonce, session->device_nonce, CONTROLLER_AUTH_NONCE_SIZE);
    auth->health.challenge_count++;
    return true;
}

/* Calculates the client proof expected for one pending challenge. */
bool controller_auth_get_proof(controller_auth_t *auth, uint16_t peer, uint32_t session_id,
                               uint8_t proof[CONTROLLER_AUTH_TAG_SIZE])
{
    if (auth == NULL || proof == NULL)
    {
        return false;
    }
    const controller_auth_session_t *session = get_session(auth, peer, session_id);
    if (session == NULL || session->is_authenticated)
    {
        return false;
    }
    uint8_t message[AUTH_PROOF_MESSAGE_SIZE];
    (void)memcpy(message, PROOF_DOMAIN, sizeof(PROOF_DOMAIN));
    put_u16(&message[AUTH_DOMAIN_SIZE], peer);
    put_u32(&message[AUTH_DOMAIN_SIZE + 2], session_id);
    (void)memcpy(&message[AUTH_DOMAIN_SIZE + 6], session->client_nonce, CONTROLLER_AUTH_NONCE_SIZE);
    (void)memcpy(&message[AUTH_DOMAIN_SIZE + 6 + CONTROLLER_AUTH_NONCE_SIZE], session->device_nonce, CONTROLLER_AUTH_NONCE_SIZE);
    return auth->config.get_hmac(auth->config.context, message, sizeof(message), proof);
}

/* Verifies a challenge proof in constant time and promotes the bounded session. */
bool controller_auth_verify_proof(controller_auth_t *auth, uint16_t peer, uint32_t session_id,
                                  const uint8_t proof[CONTROLLER_AUTH_TAG_SIZE], uint64_t now_ms)
{
    if (auth == NULL || proof == NULL)
    {
        return false;
    }
    expire_sessions(auth, now_ms);
    controller_auth_session_t *session = get_session(auth, peer, session_id);
    uint8_t expected[CONTROLLER_AUTH_TAG_SIZE];
    if (session == NULL || session->is_authenticated || !controller_auth_get_proof(auth, peer, session_id, expected) ||
        !is_tag_equal(expected, proof))
    {
        auth->health.failed_proof_count++;
        if (session != NULL && ++session->failed_attempts >= auth->config.maximum_attempts)
        {
            *session = (controller_auth_session_t){0};
        }
        return false;
    }
    session->is_authenticated = true;
    session->expires_at_ms    = now_ms + auth->config.session_lifetime_ms;
    auth->health.authenticated_count++;
    return true;
}

/* Builds and authenticates one request or response transcript. */
static bool get_envelope_tag(controller_auth_t *auth, const uint8_t *domain, uint16_t peer, uint32_t session_id,
                             uint64_t sequence, uint8_t operation, const uint8_t *body, size_t body_size,
                             uint8_t tag[CONTROLLER_AUTH_TAG_SIZE])
{
    if (body_size > AUTH_MAXIMUM_BODY_SIZE || (body == NULL && body_size > 0))
    {
        return false;
    }
    uint8_t message[AUTH_MAXIMUM_MESSAGE_SIZE];
    (void)memcpy(message, domain, AUTH_DOMAIN_SIZE);
    put_u16(&message[AUTH_DOMAIN_SIZE], peer);
    put_u32(&message[AUTH_DOMAIN_SIZE + 2], session_id);
    put_u64(&message[AUTH_DOMAIN_SIZE + 6], sequence);
    message[AUTH_DOMAIN_SIZE + 14] = operation;
    (void)memcpy(&message[AUTH_ENVELOPE_PREFIX_SIZE], body, body_size);
    return auth->config.get_hmac(auth->config.context, message, AUTH_ENVELOPE_PREFIX_SIZE + body_size, tag);
}

/* Verifies an increasing authenticated request sequence and its body tag. */
bool controller_auth_verify_request(controller_auth_t *auth, uint16_t peer, uint32_t session_id, uint64_t sequence,
                                    uint8_t operation, const uint8_t *body, size_t body_size,
                                    const uint8_t tag[CONTROLLER_AUTH_TAG_SIZE], uint64_t now_ms)
{
    if (auth == NULL || tag == NULL)
    {
        return false;
    }
    expire_sessions(auth, now_ms);
    controller_auth_session_t *session = get_session(auth, peer, session_id);
    uint8_t expected[CONTROLLER_AUTH_TAG_SIZE];
    if (session == NULL || !session->is_authenticated || sequence <= session->receive_sequence ||
        !get_envelope_tag(auth, REQUEST_DOMAIN, peer, session_id, sequence, operation, body, body_size, expected) ||
        !is_tag_equal(expected, tag))
    {
        auth->health.replay_count++;
        return false;
    }
    session->receive_sequence = sequence;
    return true;
}

/* Signs the next authenticated response sequence and returns that sequence and tag. */
bool controller_auth_sign_response(controller_auth_t *auth, uint16_t peer, uint32_t session_id, uint8_t operation,
                                   const uint8_t *body, size_t body_size, uint64_t now_ms, uint64_t *sequence,
                                   uint8_t tag[CONTROLLER_AUTH_TAG_SIZE])
{
    if (auth == NULL || sequence == NULL || tag == NULL)
    {
        return false;
    }
    expire_sessions(auth, now_ms);
    controller_auth_session_t *session = get_session(auth, peer, session_id);
    if (session == NULL || !session->is_authenticated || session->transmit_sequence == UINT64_MAX)
    {
        return false;
    }
    const uint64_t next_sequence = session->transmit_sequence + 1U;
    if (!get_envelope_tag(auth, RESPONSE_DOMAIN, peer, session_id, next_sequence, operation, body, body_size, tag))
    {
        return false;
    }
    session->transmit_sequence = next_sequence;
    *sequence                  = next_sequence;
    return true;
}

/* Invalidates one peer session without affecting other bounded peers. */
void controller_auth_close(controller_auth_t *auth, uint16_t peer, uint32_t session_id)
{
    if (auth == NULL)
    {
        return;
    }
    controller_auth_session_t *session = get_session(auth, peer, session_id);
    if (session != NULL)
    {
        *session = (controller_auth_session_t){0};
    }
}

/* Gets authentication attempt, replay, expiry, and saturation counters. */
controller_auth_health_t controller_auth_get_health(const controller_auth_t *auth)
{
    return auth != NULL ? auth->health : (controller_auth_health_t){0};
}
