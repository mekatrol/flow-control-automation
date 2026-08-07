#include <assert.h>
#include <stdio.h>
#include <string.h>

#include "controller_protocol.h"

/* Fixture limits and identities make version-one wire expectations explicit. */
enum
{
    CONTROLLER_ADDRESS = 42,
    HOST_ADDRESS       = 7,
    TRANSACTION_ID     = 0x1234,
};

static uint8_t sent_frame[CONTROLLER_PROTOCOL_FRAME_CAPACITY];
static size_t sent_size;
static const char DEVICE_ID[]            = "controller-001";
static const char HARDWARE_MODEL[]       = "test-board";
static const char FIRMWARE_VERSION[]     = "1.2.3";
static const char TEST_SUCCESS_MESSAGE[] = "Controller protocol tests passed";
static uint16_t commanded_outputs;

/* Captures one single-output command for dispatcher contract tests. */
static controller_protocol_provider_result_t set_output(void *context, const char *point_id, bool value)
{
    assert(context == NULL && strcmp(point_id, "output-01") == 0);
    commanded_outputs = value ? 1U : 0U;
    return CONTROLLER_PROTOCOL_PROVIDER_OK;
}

/* Captures one complete output bitmap for dispatcher contract tests. */
static controller_protocol_provider_result_t set_output_block(void *context, uint16_t outputs)
{
    assert(context == NULL);
    commanded_outputs = outputs;
    return CONTROLLER_PROTOCOL_PROVIDER_OK;
}

/* Supplies one coherent digital block without performing hardware work in dispatch. */
static controller_protocol_provider_result_t get_io_block(void *context, controller_protocol_io_block_t *block)
{
    assert(context == NULL && block != NULL);
    *block = (controller_protocol_io_block_t){
        .inputs = UINT16_C(0x8001), .outputs = UINT16_C(0x4002), .validity_flags = 3, .sampled_at_ms = 1234, .sequence = 9};
    return CONTROLLER_PROTOCOL_PROVIDER_OK;
}

/* Captures one complete encoded response for deterministic dispatcher assertions. */
static bool capture_send(void *context, const uint8_t *data, size_t size)
{
    assert(context == NULL);
    assert(size <= sizeof(sent_frame));
    (void)memcpy(sent_frame, data, size);
    sent_size = size;
    return true;
}

/* Builds one initialized dispatcher without a point provider. */
static controller_protocol_t get_protocol(void)
{
    controller_protocol_t protocol;
    const controller_protocol_config_t config = {.address          = CONTROLLER_ADDRESS,
                                                 .device_id        = DEVICE_ID,
                                                 .hardware_model   = HARDWARE_MODEL,
                                                 .firmware_version = FIRMWARE_VERSION,
                                                 .get_io_block     = get_io_block,
                                                 .set_output       = set_output,
                                                 .set_output_block = set_output_block};
    assert(controller_protocol_init(&protocol, &config, capture_send, NULL));
    sent_size = 0;
    return protocol;
}

/* Encodes one request into local frame storage for dispatcher tests. */
static size_t encode_request(uint16_t destination, uint8_t operation, const uint8_t *payload, size_t payload_size, uint8_t *frame)
{
    controller_protocol_message_t request = {.destination  = destination,
                                             .source       = HOST_ADDRESS,
                                             .transaction  = TRANSACTION_ID,
                                             .operation    = operation,
                                             .payload_size = payload_size};
    if (payload_size > 0)
    {
        (void)memcpy(request.payload, payload, payload_size);
    }
    size_t frame_size = 0;
    assert(controller_protocol_encode(&request, frame, CONTROLLER_PROTOCOL_FRAME_CAPACITY, &frame_size));
    return frame_size;
}

/* Verifies the published CRC check value and frame header byte order. */
static void test_crc_and_encoding_vector(void)
{
    const uint8_t check_value[] = "123456789";
    assert(controller_protocol_get_crc(check_value, sizeof(check_value) - 1U) == UINT16_C(0x4b37));
    const uint8_t payload[] = {0xaa, 0x55};
    uint8_t frame[CONTROLLER_PROTOCOL_FRAME_CAPACITY];
    const size_t size = encode_request(CONTROLLER_ADDRESS, CONTROLLER_PROTOCOL_OPERATION_ECHO, payload, sizeof(payload), frame);
    assert(size == 17);
    const uint8_t expected_header[] = {0x46, 0x43, 0x01, 0x00, 0x2a, 0x00, 0x07, 0x00, 0x34, 0x12, 0x01, 0x02, 0x00, 0xaa, 0x55};
    assert(memcmp(frame, expected_header, sizeof(expected_header)) == 0);
    assert(((uint16_t)frame[15] | ((uint16_t)frame[16] << 8U)) == controller_protocol_get_crc(frame, 15));
}

/* Verifies a maximum payload round trip retains every trusted field and byte. */
static void test_maximum_round_trip(void)
{
    controller_protocol_message_t message = {.destination  = CONTROLLER_ADDRESS,
                                             .source       = HOST_ADDRESS,
                                             .transaction  = TRANSACTION_ID,
                                             .operation    = CONTROLLER_PROTOCOL_OPERATION_ECHO,
                                             .payload_size = CONTROLLER_PROTOCOL_PAYLOAD_CAPACITY};
    for (size_t index = 0; index < message.payload_size; index++)
    {
        message.payload[index] = (uint8_t)index;
    }
    uint8_t frame[CONTROLLER_PROTOCOL_FRAME_CAPACITY];
    size_t frame_size = 0;
    assert(controller_protocol_encode(&message, frame, sizeof(frame), &frame_size));
    assert(frame_size == CONTROLLER_PROTOCOL_FRAME_CAPACITY);
    controller_protocol_message_t decoded;
    assert(controller_protocol_decode(frame, frame_size, &decoded) == CONTROLLER_PROTOCOL_DECODE_OK);
    assert(decoded.destination == message.destination && decoded.source == message.source);
    assert(decoded.transaction == message.transaction && decoded.operation == message.operation);
    assert(decoded.payload_size == message.payload_size);
    assert(memcmp(decoded.payload, message.payload, message.payload_size) == 0);
}

/* Verifies malformed frame categories are rejected before dispatch. */
static void test_decode_rejections(void)
{
    uint8_t frame[CONTROLLER_PROTOCOL_FRAME_CAPACITY];
    const size_t size = encode_request(CONTROLLER_ADDRESS, CONTROLLER_PROTOCOL_OPERATION_ECHO, NULL, 0, frame);
    controller_protocol_message_t decoded;
    frame[0] = 0;
    assert(controller_protocol_decode(frame, size, &decoded) == CONTROLLER_PROTOCOL_DECODE_BAD_MAGIC);
    frame[0] = 0x46;
    frame[2] = 2;
    assert(controller_protocol_decode(frame, size, &decoded) == CONTROLLER_PROTOCOL_DECODE_BAD_VERSION);
    frame[2] = 1;
    frame[3] = 0x80;
    assert(controller_protocol_decode(frame, size, &decoded) == CONTROLLER_PROTOCOL_DECODE_BAD_FLAGS);
    frame[3] = 0;
    frame[size - 1U] ^= 1U;
    assert(controller_protocol_decode(frame, size, &decoded) == CONTROLLER_PROTOCOL_DECODE_BAD_CRC);
    assert(controller_protocol_decode(frame, size - 1U, &decoded) == CONTROLLER_PROTOCOL_DECODE_BAD_LENGTH);
}

/* Verifies echo responses preserve correlation, addressing, and payload ownership. */
static void test_echo_dispatch(void)
{
    controller_protocol_t protocol = get_protocol();
    const uint8_t payload[]        = {'e', 'c', 'h', 'o'};
    uint8_t frame[CONTROLLER_PROTOCOL_FRAME_CAPACITY];
    const size_t size = encode_request(CONTROLLER_ADDRESS, CONTROLLER_PROTOCOL_OPERATION_ECHO, payload, sizeof(payload), frame);
    controller_protocol_receive(&protocol, frame, size, 0);
    assert(sent_size > 0);
    controller_protocol_message_t response;
    assert(controller_protocol_decode(sent_frame, sent_size, &response) == CONTROLLER_PROTOCOL_DECODE_OK);
    assert(response.flags == 1 && response.destination == HOST_ADDRESS && response.source == CONTROLLER_ADDRESS);
    assert(response.transaction == TRANSACTION_ID && response.operation == CONTROLLER_PROTOCOL_OPERATION_ECHO);
    assert(response.payload_size == sizeof(payload) && memcmp(response.payload, payload, sizeof(payload)) == 0);
}

/* Verifies an exact retry returns cached bytes and records duplicate handling. */
static void test_duplicate_transaction(void)
{
    controller_protocol_t protocol = get_protocol();
    const uint8_t payload[]        = {'r', 'e', 't', 'r', 'y'};
    uint8_t frame[CONTROLLER_PROTOCOL_FRAME_CAPACITY];
    const size_t size = encode_request(CONTROLLER_ADDRESS, CONTROLLER_PROTOCOL_OPERATION_ECHO, payload, sizeof(payload), frame);
    controller_protocol_receive(&protocol, frame, size, 0);
    uint8_t first_response[CONTROLLER_PROTOCOL_FRAME_CAPACITY];
    const size_t first_size = sent_size;
    (void)memcpy(first_response, sent_frame, sent_size);
    sent_size = 0;
    controller_protocol_receive(&protocol, frame, size, 1);
    assert(sent_size == first_size && memcmp(sent_frame, first_response, sent_size) == 0);
    assert(controller_protocol_get_health(&protocol).duplicate_transaction_count == 1);
}

/* Verifies foreign addresses are silent and unsupported operations return a stable error. */
static void test_address_and_operation_handling(void)
{
    controller_protocol_t protocol = get_protocol();
    uint8_t frame[CONTROLLER_PROTOCOL_FRAME_CAPACITY];
    size_t size = encode_request(CONTROLLER_ADDRESS + 1U, CONTROLLER_PROTOCOL_OPERATION_ECHO, NULL, 0, frame);
    controller_protocol_receive(&protocol, frame, size, 0);
    assert(sent_size == 0 && controller_protocol_get_health(&protocol).address_miss_count == 1);
    size = encode_request(CONTROLLER_ADDRESS, 0x7f, NULL, 0, frame);
    controller_protocol_receive(&protocol, frame, size, 0);
    controller_protocol_message_t response;
    assert(controller_protocol_decode(sent_frame, sent_size, &response) == CONTROLLER_PROTOCOL_DECODE_OK);
    assert(response.flags == 3);
    assert(response.payload[0] == CONTROLLER_PROTOCOL_ERROR_UNSUPPORTED_OPERATION && response.payload[1] == 0);
}

/* Verifies absent physical point runtime returns not-ready instead of a fabricated value. */
static void test_unavailable_point_provider(void)
{
    controller_protocol_t protocol = get_protocol();
    const uint8_t point_request[]  = {4, 't', 'e', 'm', 'p'};
    uint8_t frame[CONTROLLER_PROTOCOL_FRAME_CAPACITY];
    const size_t size = encode_request(CONTROLLER_ADDRESS, CONTROLLER_PROTOCOL_OPERATION_GET_POINT_VALUE, point_request,
                                       sizeof(point_request), frame);
    controller_protocol_receive(&protocol, frame, size, 0);
    controller_protocol_message_t response;
    assert(controller_protocol_decode(sent_frame, sent_size, &response) == CONTROLLER_PROTOCOL_DECODE_OK);
    assert(response.flags == 3);
    assert(response.payload[0] == CONTROLLER_PROTOCOL_ERROR_NOT_READY && response.payload[1] == 0);
}

/* Verifies broadcast discovery waits for its deterministic slot before responding. */
static void test_discovery_delay(void)
{
    controller_protocol_t protocol = get_protocol();
    const uint8_t request[]        = {1, 2, 3, 4, 8, 10, 0};
    uint8_t frame[CONTROLLER_PROTOCOL_FRAME_CAPACITY];
    const size_t size = encode_request(UINT16_MAX, CONTROLLER_PROTOCOL_OPERATION_DISCOVER, request, sizeof(request), frame);
    controller_protocol_receive(&protocol, frame, size, 100);
    assert(sent_size == 0 && protocol.is_discovery_pending);
    controller_protocol_process(&protocol, protocol.discovery_deadline_ms);
    assert(sent_size > 0 && !protocol.is_discovery_pending);
    controller_protocol_message_t response;
    assert(controller_protocol_decode(sent_frame, sent_size, &response) == CONTROLLER_PROTOCOL_DECODE_OK);
    assert(response.operation == CONTROLLER_PROTOCOL_OPERATION_DISCOVER && response.source == CONTROLLER_ADDRESS);
}

/* Verifies complete I/O reads are coherent and complete output writes reach their provider. */
static void test_io_block_and_output_write(void)
{
    controller_protocol_t protocol = get_protocol();
    uint8_t frame[CONTROLLER_PROTOCOL_FRAME_CAPACITY];
    size_t size = encode_request(CONTROLLER_ADDRESS, CONTROLLER_PROTOCOL_OPERATION_GET_IO_BLOCK, NULL, 0, frame);
    controller_protocol_receive(&protocol, frame, size, 0);
    controller_protocol_message_t response;
    assert(controller_protocol_decode(sent_frame, sent_size, &response) == CONTROLLER_PROTOCOL_DECODE_OK);
    assert(response.payload_size == 17 && response.payload[0] == 1 && response.payload[1] == 0x80);
    protocol                = get_protocol();
    const uint8_t command[] = {1, 1};
    size =
        encode_request(CONTROLLER_ADDRESS, CONTROLLER_PROTOCOL_OPERATION_COMMAND_OUTPUT_BLOCK, command, sizeof(command), frame);
    controller_protocol_receive(&protocol, frame, size, 1);
    assert(controller_protocol_decode(sent_frame, sent_size, &response) == CONTROLLER_PROTOCOL_DECODE_OK);
    assert(response.flags == 1 && commanded_outputs == UINT16_C(0x0101));
}

/* Runs portable FCP codec and dispatcher tests and returns success. */
int main(void)
{
    test_crc_and_encoding_vector();
    test_maximum_round_trip();
    test_decode_rejections();
    test_echo_dispatch();
    test_duplicate_transaction();
    test_address_and_operation_handling();
    test_unavailable_point_provider();
    test_discovery_delay();
    test_io_block_and_output_write();
    puts(TEST_SUCCESS_MESSAGE);
    return 0;
}
