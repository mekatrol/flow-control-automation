#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "controller/auth.h"

static uint8_t random_seed               = 1;
static const char TEST_SUCCESS_MESSAGE[] = "Controller authentication tests passed";

/* Supplies deterministic nonzero fixture bytes for bounded session tests. */
static bool get_random(void *context, uint8_t *output, size_t size)
{
    assert(context == NULL);

    for (size_t index = 0; index < size; index++)
    {
        output[index] = random_seed++;
    }

    return true;
}

/* Supplies a deterministic keyed stand-in so state tests do not depend on a platform crypto library. */
static bool get_hmac(void *context, const uint8_t *message, size_t message_size, uint8_t tag[CONTROLLER_AUTH_TAG_SIZE])
{
    assert(context == NULL);
    uint32_t state = UINT32_C(2166136261);

    for (size_t index = 0; index < message_size; index++)
    {
        state = (state ^ message[index]) * UINT32_C(16777619);
    }

    for (size_t index = 0; index < CONTROLLER_AUTH_TAG_SIZE; index++)
    {
        state      = (state ^ (uint32_t)index) * UINT32_C(16777619);
        tag[index] = (uint8_t)(state >> ((index % sizeof(state)) * 8U));
    }

    return true;
}

/* Builds one initialized authentication service with short deterministic lifetimes. */
static controller_auth_t get_auth(void)
{
    controller_auth_t auth;
    const controller_auth_config_t config = {.get_hmac              = get_hmac,
                                             .get_random            = get_random,
                                             .challenge_lifetime_ms = 100,
                                             .session_lifetime_ms   = 1000,
                                             .maximum_attempts      = 3};
    assert(controller_auth_init(&auth, &config));

    return auth;
}

/* Encodes one request transcript and produces the deterministic fixture tag. */
static void get_request_tag(uint16_t peer, uint32_t session_id, uint64_t sequence, uint8_t operation, const uint8_t *body,
                            size_t body_size, uint8_t tag[CONTROLLER_AUTH_TAG_SIZE])
{
    const uint8_t domain[] = {'F', 'C', 'P', '1', 'R', 'E', 'Q', 'T'};
    uint8_t message[64]    = {0};
    assert(sizeof(domain) + 2U + 4U + 8U + 1U + body_size <= sizeof(message));
    size_t offset = 0;
    memcpy(&message[offset], domain, sizeof(domain));
    offset += sizeof(domain);
    message[offset++] = (uint8_t)peer;
    message[offset++] = (uint8_t)(peer >> 8U);

    for (size_t index = 0; index < sizeof(session_id); index++)
    {
        message[offset++] = (uint8_t)(session_id >> (index * 8U));
    }

    for (size_t index = 0; index < sizeof(sequence); index++)
    {
        message[offset++] = (uint8_t)(sequence >> (index * 8U));
    }

    message[offset++] = operation;
    memcpy(&message[offset], body, body_size);
    assert(get_hmac(NULL, message, offset + body_size, tag));
}

/* Establishes one session and verifies increasing request and response sequences. */
static void test_session_and_sequences(void)
{
    controller_auth_t auth                                 = get_auth();
    const uint8_t client_nonce[CONTROLLER_AUTH_NONCE_SIZE] = {1};
    uint8_t device_nonce[CONTROLLER_AUTH_NONCE_SIZE];
    uint32_t session_id = 0;
    assert(controller_auth_create_challenge(&auth, 7, client_nonce, 10, &session_id, device_nonce));
    uint8_t proof[CONTROLLER_AUTH_TAG_SIZE];
    assert(controller_auth_get_proof(&auth, 7, session_id, proof));
    assert(controller_auth_verify_proof(&auth, 7, session_id, proof, 20));

    const uint8_t body[] = {1, 2, 3};
    uint8_t request_tag[CONTROLLER_AUTH_TAG_SIZE];
    get_request_tag(7, session_id, 1, 0x40, body, sizeof(body), request_tag);
    assert(controller_auth_verify_request(&auth, 7, session_id, 1, 0x40, body, sizeof(body), request_tag, 30));
    assert(!controller_auth_verify_request(&auth, 7, session_id, 1, 0x40, body, sizeof(body), request_tag, 31));
    request_tag[0] ^= 1U;
    assert(!controller_auth_verify_request(&auth, 7, session_id, 2, 0x40, body, sizeof(body), request_tag, 32));
    uint64_t response_sequence = 0;
    assert(controller_auth_sign_response(&auth, 7, session_id, 0x40, body, sizeof(body), 33, &response_sequence, request_tag));
    assert(response_sequence == 1);
    controller_auth_close(&auth, 7, session_id);
}

/* Verifies bad proofs are bounded and expired challenges cannot authenticate. */
static void test_bad_proof_and_expiry(void)
{
    controller_auth_t auth                                 = get_auth();
    const uint8_t client_nonce[CONTROLLER_AUTH_NONCE_SIZE] = {2};
    uint8_t device_nonce[CONTROLLER_AUTH_NONCE_SIZE];
    uint8_t proof[CONTROLLER_AUTH_TAG_SIZE] = {0};
    uint32_t session_id                     = 0;
    assert(controller_auth_create_challenge(&auth, 8, client_nonce, 0, &session_id, device_nonce));
    assert(!controller_auth_verify_proof(&auth, 8, session_id, proof, 1));
    assert(!controller_auth_verify_proof(&auth, 8, session_id, proof, 101));
    const controller_auth_health_t health = controller_auth_get_health(&auth);
    assert(health.failed_proof_count == 2 && health.expired_count == 1);
}

/* Verifies fixed session capacity rejects excess peers and becomes reusable after expiry. */
static void test_capacity(void)
{
    controller_auth_t auth                          = get_auth();
    const uint8_t nonce[CONTROLLER_AUTH_NONCE_SIZE] = {3};
    uint8_t device_nonce[CONTROLLER_AUTH_NONCE_SIZE];
    uint32_t session_id = 0;

    for (uint16_t peer = 1; peer <= CONTROLLER_AUTH_SESSION_CAPACITY; peer++)
    {
        assert(controller_auth_create_challenge(&auth, peer, nonce, 0, &session_id, device_nonce));
    }

    assert(!controller_auth_create_challenge(&auth, 9, nonce, 0, &session_id, device_nonce));
    assert(controller_auth_create_challenge(&auth, 9, nonce, 101, &session_id, device_nonce));
}

/* Runs bounded authentication state tests and returns success. */
int main(void)
{
    test_session_and_sequences();
    test_bad_proof_and_expiry();
    test_capacity();
    puts(TEST_SUCCESS_MESSAGE);

    return 0;
}
