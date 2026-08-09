#pragma once

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

#include "controller/auth.h"
#include "controller/points.h"
#include "flow/debug.h"
#include "flow/service.h"

/* Version-one wire limits preserve the 256-byte RS485 transport bound. */
enum
{
    CONTROLLER_PROTOCOL_FRAME_CAPACITY    = 256,
    CONTROLLER_PROTOCOL_HEADER_SIZE       = 13,
    CONTROLLER_PROTOCOL_CRC_SIZE          = 2,
    CONTROLLER_PROTOCOL_PAYLOAD_CAPACITY  = 241,
    CONTROLLER_PROTOCOL_POINT_ID_CAPACITY = 65,
    CONTROLLER_PROTOCOL_UNITS_CAPACITY    = 33,
    CONTROLLER_PROTOCOL_TEXT_CAPACITY     = 129,
};

/* Stable operation codes form the version-one device and point API. */
typedef enum
{
    CONTROLLER_PROTOCOL_OPERATION_ECHO                  = 0x01,
    CONTROLLER_PROTOCOL_OPERATION_DISCOVER              = 0x02,
    CONTROLLER_PROTOCOL_OPERATION_GET_CAPABILITIES      = 0x03,
    CONTROLLER_PROTOCOL_OPERATION_GET_DEVICE_INFO       = 0x04,
    CONTROLLER_PROTOCOL_OPERATION_GET_HEALTH            = 0x05,
    CONTROLLER_PROTOCOL_OPERATION_LIST_POINTS           = 0x10,
    CONTROLLER_PROTOCOL_OPERATION_GET_POINT_DEFINITION  = 0x11,
    CONTROLLER_PROTOCOL_OPERATION_GET_POINT_VALUE       = 0x12,
    CONTROLLER_PROTOCOL_OPERATION_SUBSCRIBE_CHANGES     = 0x13,
    CONTROLLER_PROTOCOL_OPERATION_POINT_CHANGE_EVENT    = 0x14,
    CONTROLLER_PROTOCOL_OPERATION_GET_IO_BLOCK          = 0x15,
    CONTROLLER_PROTOCOL_OPERATION_COMMAND_POINT         = 0x18,
    CONTROLLER_PROTOCOL_OPERATION_RELINQUISH_COMMAND    = 0x19,
    CONTROLLER_PROTOCOL_OPERATION_COMMAND_OUTPUT_BLOCK  = 0x1a,
    CONTROLLER_PROTOCOL_OPERATION_AUTH_CHALLENGE        = 0x30,
    CONTROLLER_PROTOCOL_OPERATION_AUTH_PROVE            = 0x31,
    CONTROLLER_PROTOCOL_OPERATION_CLOSE_SESSION         = 0x32,
    CONTROLLER_PROTOCOL_OPERATION_LIST_FLOWS            = 0x40,
    CONTROLLER_PROTOCOL_OPERATION_GET_FLOW_METADATA     = 0x41,
    CONTROLLER_PROTOCOL_OPERATION_UPLOAD_BEGIN          = 0x42,
    CONTROLLER_PROTOCOL_OPERATION_UPLOAD_STATUS         = 0x43,
    CONTROLLER_PROTOCOL_OPERATION_UPLOAD_CHUNK          = 0x44,
    CONTROLLER_PROTOCOL_OPERATION_UPLOAD_VALIDATE       = 0x45,
    CONTROLLER_PROTOCOL_OPERATION_UPLOAD_COMMIT         = 0x46,
    CONTROLLER_PROTOCOL_OPERATION_UPLOAD_ABORT          = 0x47,
    CONTROLLER_PROTOCOL_OPERATION_DOWNLOAD_BEGIN        = 0x48,
    CONTROLLER_PROTOCOL_OPERATION_DOWNLOAD_CHUNK        = 0x49,
    CONTROLLER_PROTOCOL_OPERATION_ACTIVATE_FLOW         = 0x4a,
    CONTROLLER_PROTOCOL_OPERATION_DEACTIVATE_FLOW       = 0x4b,
    CONTROLLER_PROTOCOL_OPERATION_REMOVE_FLOW           = 0x4c,
    CONTROLLER_PROTOCOL_OPERATION_GET_FLOW_RUNTIME      = 0x4d,
    CONTROLLER_PROTOCOL_OPERATION_DEBUG_LOAD_BEGIN      = 0x50,
    CONTROLLER_PROTOCOL_OPERATION_DEBUG_LOAD_CHUNK      = 0x51,
    CONTROLLER_PROTOCOL_OPERATION_DEBUG_PREPARE         = 0x52,
    CONTROLLER_PROTOCOL_OPERATION_DEBUG_STATUS          = 0x53,
    CONTROLLER_PROTOCOL_OPERATION_DEBUG_STEP            = 0x54,
    CONTROLLER_PROTOCOL_OPERATION_DEBUG_SNAPSHOT_HEADER = 0x55,
    CONTROLLER_PROTOCOL_OPERATION_DEBUG_SNAPSHOT_CHUNK  = 0x56,
    CONTROLLER_PROTOCOL_OPERATION_DEBUG_RENEW           = 0x57,
    CONTROLLER_PROTOCOL_OPERATION_DEBUG_STOP            = 0x58,
    CONTROLLER_PROTOCOL_OPERATION_DEBUG_RUN             = 0x59,
    CONTROLLER_PROTOCOL_OPERATION_DEBUG_PAUSE           = 0x5a,
    CONTROLLER_PROTOCOL_OPERATION_DEBUG_ENABLE_LIVE_OUTPUT = 0x5b,
} controller_protocol_operation_t;

/* Protocol errors are stable wire values and never expose platform error codes. */
typedef enum
{
    CONTROLLER_PROTOCOL_ERROR_MALFORMED             = 1,
    CONTROLLER_PROTOCOL_ERROR_UNSUPPORTED_VERSION   = 2,
    CONTROLLER_PROTOCOL_ERROR_UNSUPPORTED_OPERATION = 3,
    CONTROLLER_PROTOCOL_ERROR_WRONG_STATE           = 4,
    CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT      = 5,
    CONTROLLER_PROTOCOL_ERROR_NOT_FOUND             = 6,
    CONTROLLER_PROTOCOL_ERROR_NOT_READY             = 7,
    CONTROLLER_PROTOCOL_ERROR_UNSUPPORTED           = 8,
    CONTROLLER_PROTOCOL_ERROR_UNAUTHORIZED          = 9,
    CONTROLLER_PROTOCOL_ERROR_BUSY                  = 12,
    CONTROLLER_PROTOCOL_ERROR_QUEUE_FULL            = 13,
    CONTROLLER_PROTOCOL_ERROR_STORAGE_UNAVAILABLE   = 14,
    CONTROLLER_PROTOCOL_ERROR_STORAGE_FULL          = 15,
    CONTROLLER_PROTOCOL_ERROR_REVISION_CONFLICT     = 16,
    CONTROLLER_PROTOCOL_ERROR_DIGEST_MISMATCH       = 17,
    CONTROLLER_PROTOCOL_ERROR_VALIDATION_FAILED     = 18,
    CONTROLLER_PROTOCOL_ERROR_INTERNAL              = 20,
} controller_protocol_error_t;

/* Decode outcomes distinguish silent framing rejection reasons for health counters. */
typedef enum
{
    CONTROLLER_PROTOCOL_DECODE_OK,
    CONTROLLER_PROTOCOL_DECODE_BAD_MAGIC,
    CONTROLLER_PROTOCOL_DECODE_BAD_VERSION,
    CONTROLLER_PROTOCOL_DECODE_BAD_FLAGS,
    CONTROLLER_PROTOCOL_DECODE_BAD_LENGTH,
    CONTROLLER_PROTOCOL_DECODE_BAD_CRC,
} controller_protocol_decode_result_t;

typedef struct
{
    uint8_t flags;
    uint16_t destination;
    uint16_t source;
    uint16_t transaction;
    uint8_t operation;
    size_t payload_size;
    uint8_t payload[CONTROLLER_PROTOCOL_PAYLOAD_CAPACITY];
} controller_protocol_message_t;

/* Canonical point types match the parent repository contract. */
typedef enum
{
    CONTROLLER_PROTOCOL_POINT_ANALOG      = 1,
    CONTROLLER_PROTOCOL_POINT_DIGITAL     = 2,
    CONTROLLER_PROTOCOL_POINT_MULTI_STATE = 3,
    CONTROLLER_PROTOCOL_POINT_INTEGER     = 4,
    CONTROLLER_PROTOCOL_POINT_TEXT        = 5,
} controller_protocol_point_type_t;

/* Point quality remains independent of the typed value. */
typedef enum
{
    CONTROLLER_PROTOCOL_QUALITY_GOOD,
    CONTROLLER_PROTOCOL_QUALITY_UNCERTAIN,
    CONTROLLER_PROTOCOL_QUALITY_BAD,
} controller_protocol_quality_t;

typedef struct
{
    char id[CONTROLLER_PROTOCOL_POINT_ID_CAPACITY];
    uint32_t revision;
    controller_protocol_point_type_t type;
    uint8_t service_flags;
    char units[CONTROLLER_PROTOCOL_UNITS_CAPACITY];
} controller_protocol_point_definition_t;

typedef struct
{
    controller_protocol_point_definition_t definition;
    union {
        double analog;
        bool digital;
        int64_t integer;
        char text[CONTROLLER_PROTOCOL_TEXT_CAPACITY];
    } value;
    controller_protocol_quality_t quality;
    uint16_t reliability;
    int64_t source_timestamp_ms;
    int64_t updated_at_ms;
    uint32_t sequence;
} controller_protocol_point_value_t;

/* Provider results keep point availability distinct from missing or unsupported data. */
typedef enum
{
    CONTROLLER_PROTOCOL_PROVIDER_OK,
    CONTROLLER_PROTOCOL_PROVIDER_NOT_FOUND,
    CONTROLLER_PROTOCOL_PROVIDER_NOT_READY,
    CONTROLLER_PROTOCOL_PROVIDER_UNSUPPORTED,
    CONTROLLER_PROTOCOL_PROVIDER_FAILED,
} controller_protocol_provider_result_t;

typedef struct
{
    controller_protocol_provider_result_t (*get_count)(void *context, size_t *count);
    controller_protocol_provider_result_t (*get_definition)(void *context, size_t index,
                                                            controller_protocol_point_definition_t *definition);
    controller_protocol_provider_result_t (*get_value)(void *context, const char *point_id,
                                                       controller_protocol_point_value_t *value);
    void *context;
} controller_protocol_point_provider_t;

typedef struct
{
    uint16_t inputs;
    uint16_t outputs;
    uint8_t validity_flags;
    int64_t sampled_at_ms;
    uint32_t sequence;
} controller_protocol_io_block_t;

/* Block providers return one coherent snapshot without performing field I/O in the dispatcher. */
typedef controller_protocol_provider_result_t (*controller_protocol_get_io_block_t)(void *context,
                                                                                    controller_protocol_io_block_t *block);
typedef controller_protocol_provider_result_t (*controller_protocol_set_output_t)(void *context, const char *point_id,
                                                                                  bool value);
typedef controller_protocol_provider_result_t (*controller_protocol_set_output_block_t)(void *context, uint16_t outputs);

typedef struct
{
    uint16_t address;
    const char *device_id;
    const char *hardware_model;
    const char *firmware_version;
    controller_protocol_point_provider_t point_provider;
    controller_protocol_get_io_block_t get_io_block;
    controller_protocol_set_output_t set_output;
    controller_protocol_set_output_block_t set_output_block;
    void *io_context;
    controller_auth_t *auth;
    controller_flow_t *flow;
    flow_debug_t *debug;
    controller_points_t *points;
} controller_protocol_config_t;

typedef struct
{
    uint32_t accepted_frame_count;
    uint32_t magic_error_count;
    uint32_t version_error_count;
    uint32_t flag_error_count;
    uint32_t length_error_count;
    uint32_t crc_error_count;
    uint32_t address_miss_count;
    uint32_t unsupported_operation_count;
    uint32_t provider_error_count;
    uint32_t response_drop_count;
    uint32_t duplicate_transaction_count;
} controller_protocol_health_t;

/* Response writers copy a complete encoded frame into a bounded transport queue. */
typedef bool (*controller_protocol_send_t)(void *context, const uint8_t *data, size_t size);

typedef struct
{
    controller_protocol_config_t config;
    controller_protocol_health_t health;
    controller_protocol_send_t send;
    void *send_context;
    bool is_discovery_pending;
    uint64_t discovery_deadline_ms;
    controller_protocol_message_t discovery_request;
    bool is_request_active;
    size_t active_request_size;
    uint8_t active_request[CONTROLLER_PROTOCOL_FRAME_CAPACITY];
    bool has_cached_response;
    uint16_t cached_source;
    uint16_t cached_transaction;
    size_t cached_request_size;
    uint8_t cached_request[CONTROLLER_PROTOCOL_FRAME_CAPACITY];
    size_t cached_response_size;
    uint8_t cached_response[CONTROLLER_PROTOCOL_FRAME_CAPACITY];
    bool is_authenticated_dispatch;
    bool is_session_close_pending;
    uint32_t authenticated_session_id;
    uint64_t authenticated_now_ms;
} controller_protocol_t;

/* Calculates the normative CRC-16/Modbus value for a byte range. */
uint16_t controller_protocol_get_crc(const uint8_t *data, size_t size);

/* Encodes one validated message into a bounded version-one wire frame. */
bool controller_protocol_encode(const controller_protocol_message_t *message, uint8_t *output, size_t capacity,
                                size_t *output_size);

/* Decodes and validates one complete version-one wire frame. */
controller_protocol_decode_result_t controller_protocol_decode(const uint8_t *frame, size_t size,
                                                               controller_protocol_message_t *message);

/* Initializes a protocol dispatcher with immutable identity and provider contracts. */
bool controller_protocol_init(controller_protocol_t *protocol, const controller_protocol_config_t *config,
                              controller_protocol_send_t send, void *send_context);

/* Validates and dispatches one owned transport frame without blocking. */
void controller_protocol_receive(controller_protocol_t *protocol, const uint8_t *frame, size_t size, uint64_t now_ms);

/* Sends a pending collision-delayed discovery response when its bounded slot expires. */
void controller_protocol_process(controller_protocol_t *protocol, uint64_t now_ms);

/* Gets a read-only snapshot of protocol validation and dispatch counters. */
controller_protocol_health_t controller_protocol_get_health(const controller_protocol_t *protocol);
