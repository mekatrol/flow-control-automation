#include "controller_protocol.h"

#include <math.h>
#include <string.h>

/* Wire constants define the fixed version-one framing and bounded discovery policy. */
enum
{
    PROTOCOL_MAGIC_FIRST             = 0x46,
    PROTOCOL_MAGIC_SECOND            = 0x43,
    PROTOCOL_VERSION                 = 1,
    PROTOCOL_FLAG_RESPONSE           = 1,
    PROTOCOL_FLAG_ERROR              = 2,
    PROTOCOL_FLAG_MORE               = 8,
    PROTOCOL_ALLOWED_FLAGS           = 0x0f,
    PROTOCOL_BROADCAST_ADDRESS       = 0xffff,
    PROTOCOL_CAPABILITY_MINOR        = 0,
    PROTOCOL_MAXIMUM_SLOTS           = 64,
    PROTOCOL_MAXIMUM_SLOT_TIME_MS    = 1000,
    PROTOCOL_POINT_TYPE_MASK         = 0x1f,
    PROTOCOL_OPERATION_BITMAP_SIZE   = 3,
    PROTOCOL_MAXIMUM_POINT_COUNT     = 1024,
    PROTOCOL_DISCOVERY_NONCE_SIZE    = 4,
    PROTOCOL_DISCOVERY_SEED_CAPACITY = UINT8_MAX + PROTOCOL_DISCOVERY_NONCE_SIZE,
};

/* Reads an unsigned 16-bit little-endian field without assuming buffer alignment. */
static uint16_t get_u16(const uint8_t *data)
{
    return (uint16_t)data[0] | ((uint16_t)data[1] << 8U);
}

/* Writes an unsigned 16-bit value in the normative little-endian order. */
static void put_u16(uint8_t *data, uint16_t value)
{
    data[0] = (uint8_t)value;
    data[1] = (uint8_t)(value >> 8U);
}

/* Writes an unsigned 32-bit value in the normative little-endian order. */
static void put_u32(uint8_t *data, uint32_t value)
{
    data[0] = (uint8_t)value;
    data[1] = (uint8_t)(value >> 8U);
    data[2] = (uint8_t)(value >> 16U);
    data[3] = (uint8_t)(value >> 24U);
}

/* Writes an unsigned 64-bit value in the normative little-endian order. */
static void put_u64(uint8_t *data, uint64_t value)
{
    for (size_t index = 0; index < sizeof(value); index++)
    {
        data[index] = (uint8_t)(value >> (index * 8U));
    }
}

/* Gets a bounded terminated string length and rejects unterminated provider data. */
static bool get_string_size(const char *value, size_t capacity, size_t *size)
{
    for (size_t index = 0; index < capacity; index++)
    {
        if (value[index] == '\0')
        {
            *size = index;
            return true;
        }
    }
    return false;
}

/* Appends one string8 field while preserving the response payload bound. */
static bool append_string8(uint8_t *payload, size_t capacity, size_t *offset, const char *value, size_t value_capacity)
{
    size_t size = 0;
    if (!get_string_size(value, value_capacity, &size) || size > UINT8_MAX || *offset + 1U + size > capacity)
    {
        return false;
    }
    payload[(*offset)++] = (uint8_t)size;
    (void)memcpy(&payload[*offset], value, size);
    *offset += size;
    return true;
}

/* Appends one string16 field while preserving the response payload bound. */
static bool append_string16(uint8_t *payload, size_t capacity, size_t *offset, const char *value, size_t value_capacity)
{
    size_t size = 0;
    if (!get_string_size(value, value_capacity, &size) || size > UINT16_MAX || *offset + 2U + size > capacity)
    {
        return false;
    }
    put_u16(&payload[*offset], (uint16_t)size);
    *offset += 2;
    (void)memcpy(&payload[*offset], value, size);
    *offset += size;
    return true;
}

/* Calculates the normative CRC-16/Modbus value for a byte range. */
uint16_t controller_protocol_get_crc(const uint8_t *data, size_t size)
{
    uint16_t crc = UINT16_C(0xffff);
    if (data == NULL && size > 0)
    {
        return 0;
    }
    for (size_t index = 0; index < size; index++)
    {
        crc ^= data[index];
        for (unsigned bit = 0; bit < 8U; bit++)
        {
            crc = (crc & 1U) != 0U ? (uint16_t)((crc >> 1U) ^ UINT16_C(0xa001)) : (uint16_t)(crc >> 1U);
        }
    }
    return crc;
}

/* Encodes one validated message into a bounded version-one wire frame. */
bool controller_protocol_encode(const controller_protocol_message_t *message, uint8_t *output, size_t capacity,
                                size_t *output_size)
{
    if (message == NULL || output == NULL || output_size == NULL || message->payload_size > sizeof(message->payload) ||
        (message->flags & (uint8_t)~PROTOCOL_ALLOWED_FLAGS) != 0U ||
        ((message->flags & PROTOCOL_FLAG_ERROR) != 0U && (message->flags & PROTOCOL_FLAG_RESPONSE) == 0U))
    {
        return false;
    }
    const size_t frame_size = CONTROLLER_PROTOCOL_HEADER_SIZE + message->payload_size + CONTROLLER_PROTOCOL_CRC_SIZE;
    if (frame_size > capacity)
    {
        return false;
    }
    output[0] = PROTOCOL_MAGIC_FIRST;
    output[1] = PROTOCOL_MAGIC_SECOND;
    output[2] = PROTOCOL_VERSION;
    output[3] = message->flags;
    put_u16(&output[4], message->destination);
    put_u16(&output[6], message->source);
    put_u16(&output[8], message->transaction);
    output[10] = message->operation;
    put_u16(&output[11], (uint16_t)message->payload_size);
    (void)memcpy(&output[CONTROLLER_PROTOCOL_HEADER_SIZE], message->payload, message->payload_size);
    put_u16(&output[frame_size - CONTROLLER_PROTOCOL_CRC_SIZE],
            controller_protocol_get_crc(output, frame_size - CONTROLLER_PROTOCOL_CRC_SIZE));
    *output_size = frame_size;
    return true;
}

/* Decodes and validates one complete version-one wire frame. */
controller_protocol_decode_result_t controller_protocol_decode(const uint8_t *frame, size_t size,
                                                               controller_protocol_message_t *message)
{
    if (frame == NULL || message == NULL || size < CONTROLLER_PROTOCOL_HEADER_SIZE + CONTROLLER_PROTOCOL_CRC_SIZE ||
        size > CONTROLLER_PROTOCOL_FRAME_CAPACITY)
    {
        return CONTROLLER_PROTOCOL_DECODE_BAD_LENGTH;
    }
    if (frame[0] != PROTOCOL_MAGIC_FIRST || frame[1] != PROTOCOL_MAGIC_SECOND)
    {
        return CONTROLLER_PROTOCOL_DECODE_BAD_MAGIC;
    }
    if (frame[2] != PROTOCOL_VERSION)
    {
        return CONTROLLER_PROTOCOL_DECODE_BAD_VERSION;
    }
    if ((frame[3] & (uint8_t)~PROTOCOL_ALLOWED_FLAGS) != 0U ||
        ((frame[3] & PROTOCOL_FLAG_ERROR) != 0U && (frame[3] & PROTOCOL_FLAG_RESPONSE) == 0U))
    {
        return CONTROLLER_PROTOCOL_DECODE_BAD_FLAGS;
    }
    const size_t payload_size = get_u16(&frame[11]);
    if (payload_size > CONTROLLER_PROTOCOL_PAYLOAD_CAPACITY ||
        size != CONTROLLER_PROTOCOL_HEADER_SIZE + payload_size + CONTROLLER_PROTOCOL_CRC_SIZE)
    {
        return CONTROLLER_PROTOCOL_DECODE_BAD_LENGTH;
    }
    if (get_u16(&frame[size - CONTROLLER_PROTOCOL_CRC_SIZE]) !=
        controller_protocol_get_crc(frame, size - CONTROLLER_PROTOCOL_CRC_SIZE))
    {
        return CONTROLLER_PROTOCOL_DECODE_BAD_CRC;
    }
    *message = (controller_protocol_message_t){.flags        = frame[3],
                                               .destination  = get_u16(&frame[4]),
                                               .source       = get_u16(&frame[6]),
                                               .transaction  = get_u16(&frame[8]),
                                               .operation    = frame[10],
                                               .payload_size = payload_size};
    (void)memcpy(message->payload, &frame[CONTROLLER_PROTOCOL_HEADER_SIZE], payload_size);
    return CONTROLLER_PROTOCOL_DECODE_OK;
}

/* Tests immutable identity strings before the dispatcher can format responses. */
static bool is_config_valid(const controller_protocol_config_t *config)
{
    size_t size = 0;
    return config != NULL && config->address != PROTOCOL_BROADCAST_ADDRESS && config->device_id != NULL &&
           config->hardware_model != NULL && config->firmware_version != NULL &&
           get_string_size(config->device_id, UINT8_MAX, &size) && size > 0 &&
           get_string_size(config->hardware_model, UINT8_MAX, &size) && size > 0 &&
           get_string_size(config->firmware_version, UINT8_MAX, &size) && size > 0;
}

/* Initializes a protocol dispatcher with immutable identity and provider contracts. */
bool controller_protocol_init(controller_protocol_t *protocol, const controller_protocol_config_t *config,
                              controller_protocol_send_t send, void *send_context)
{
    if (protocol == NULL || !is_config_valid(config) || send == NULL)
    {
        return false;
    }
    *protocol              = (controller_protocol_t){0};
    protocol->config       = *config;
    protocol->send         = send;
    protocol->send_context = send_context;
    return true;
}

/* Sends one response and accounts for encode or bounded transport rejection. */
static void send_response(controller_protocol_t *protocol, const controller_protocol_message_t *response)
{
    uint8_t frame[CONTROLLER_PROTOCOL_FRAME_CAPACITY];
    size_t frame_size = 0;
    if (!controller_protocol_encode(response, frame, sizeof(frame), &frame_size))
    {
        protocol->health.response_drop_count++;
        return;
    }
    if (protocol->is_request_active)
    {
        /* Cache the exact request and response so retries cannot repeat provider work. */
        protocol->cached_source        = response->destination;
        protocol->cached_transaction   = response->transaction;
        protocol->cached_request_size  = protocol->active_request_size;
        protocol->cached_response_size = frame_size;
        (void)memcpy(protocol->cached_request, protocol->active_request, protocol->active_request_size);
        (void)memcpy(protocol->cached_response, frame, frame_size);
        protocol->has_cached_response = true;
    }
    if (!protocol->send(protocol->send_context, frame, frame_size))
    {
        protocol->health.response_drop_count++;
    }
}

/* Initializes response addressing and correlation from a trusted request. */
static controller_protocol_message_t get_response(const controller_protocol_t *protocol,
                                                  const controller_protocol_message_t *request)
{
    return (controller_protocol_message_t){.flags       = PROTOCOL_FLAG_RESPONSE,
                                           .destination = request->source,
                                           .source      = protocol->config.address,
                                           .transaction = request->transaction,
                                           .operation   = request->operation};
}

/* Sends one stable empty-diagnostic error for a trusted request. */
static void send_error(controller_protocol_t *protocol, const controller_protocol_message_t *request,
                       controller_protocol_error_t error)
{
    controller_protocol_message_t response = get_response(protocol, request);
    response.flags |= PROTOCOL_FLAG_ERROR;
    response.payload_size = 9;
    put_u16(&response.payload[0], (uint16_t)error);
    put_u16(&response.payload[2], 0);
    put_u32(&response.payload[4], 0);
    response.payload[8] = 0;
    send_response(protocol, &response);
}

/* Maps provider-specific availability to the stable protocol error vocabulary. */
static controller_protocol_error_t get_provider_error(controller_protocol_provider_result_t result)
{
    switch (result)
    {
        case CONTROLLER_PROTOCOL_PROVIDER_NOT_FOUND:
            return CONTROLLER_PROTOCOL_ERROR_NOT_FOUND;
        case CONTROLLER_PROTOCOL_PROVIDER_NOT_READY:
            return CONTROLLER_PROTOCOL_ERROR_NOT_READY;
        case CONTROLLER_PROTOCOL_PROVIDER_UNSUPPORTED:
            return CONTROLLER_PROTOCOL_ERROR_UNSUPPORTED;
        case CONTROLLER_PROTOCOL_PROVIDER_FAILED:
            return CONTROLLER_PROTOCOL_ERROR_INTERNAL;
        case CONTROLLER_PROTOCOL_PROVIDER_OK:
            return CONTROLLER_PROTOCOL_ERROR_INTERNAL;
    }
    return CONTROLLER_PROTOCOL_ERROR_INTERNAL;
}

/* Encodes the common point definition prefix used by list and value responses. */
static bool append_point_definition(uint8_t *payload, size_t capacity, size_t *offset,
                                    const controller_protocol_point_definition_t *definition)
{
    if (definition->type < CONTROLLER_PROTOCOL_POINT_ANALOG || definition->type > CONTROLLER_PROTOCOL_POINT_TEXT ||
        !append_string8(payload, capacity, offset, definition->id, sizeof(definition->id)) || *offset + 6U > capacity)
    {
        return false;
    }
    put_u32(&payload[*offset], definition->revision);
    *offset += 4;
    payload[(*offset)++] = (uint8_t)definition->type;
    payload[(*offset)++] = definition->service_flags;
    return append_string8(payload, capacity, offset, definition->units, sizeof(definition->units));
}

/* Dispatches point enumeration through a provider without depending on physical I/O. */
static void handle_list_points(controller_protocol_t *protocol, const controller_protocol_message_t *request)
{
    if (request->payload_size != 3 || protocol->config.point_provider.get_count == NULL ||
        protocol->config.point_provider.get_definition == NULL)
    {
        send_error(protocol, request,
                   request->payload_size == 3 ? CONTROLLER_PROTOCOL_ERROR_NOT_READY : CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
        return;
    }
    size_t count = 0;
    controller_protocol_provider_result_t result =
        protocol->config.point_provider.get_count(protocol->config.point_provider.context, &count);
    if (result != CONTROLLER_PROTOCOL_PROVIDER_OK)
    {
        protocol->health.provider_error_count++;
        send_error(protocol, request, get_provider_error(result));
        return;
    }
    const size_t start   = get_u16(request->payload);
    const size_t maximum = request->payload[2];
    if (maximum == 0 || start > count || count > PROTOCOL_MAXIMUM_POINT_COUNT)
    {
        send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
        return;
    }
    controller_protocol_message_t response = get_response(protocol, request);
    put_u16(response.payload, (uint16_t)count);
    response.payload[2] = 0;
    size_t offset       = 3;
    for (size_t index = start; index < count && response.payload[2] < maximum; index++)
    {
        controller_protocol_point_definition_t definition = {0};
        result = protocol->config.point_provider.get_definition(protocol->config.point_provider.context, index, &definition);
        const size_t previous_offset = offset;
        if (result != CONTROLLER_PROTOCOL_PROVIDER_OK ||
            !append_point_definition(response.payload, sizeof(response.payload), &offset, &definition))
        {
            offset = previous_offset;
            if (response.payload[2] == 0)
            {
                protocol->health.provider_error_count++;
                send_error(protocol, request,
                           result == CONTROLLER_PROTOCOL_PROVIDER_OK ? CONTROLLER_PROTOCOL_ERROR_INTERNAL
                                                                     : get_provider_error(result));
                return;
            }
            response.flags |= PROTOCOL_FLAG_MORE;
            break;
        }
        response.payload[2]++;
    }
    response.payload_size = offset;
    send_response(protocol, &response);
}

/* Decodes one bounded point ID request into terminated local storage. */
static bool get_requested_point_id(const controller_protocol_message_t *request, char *point_id, size_t capacity)
{
    if (request->payload_size < 1)
    {
        return false;
    }
    const size_t size = request->payload[0];
    if (size == 0 || size + 1U != request->payload_size || size >= capacity)
    {
        return false;
    }
    (void)memcpy(point_id, &request->payload[1], size);
    point_id[size] = '\0';
    return true;
}

/* Finds a point definition by stable ID using the provider's bounded enumeration. */
static controller_protocol_provider_result_t get_definition_by_id(const controller_protocol_point_provider_t *provider,
                                                                  const char *point_id,
                                                                  controller_protocol_point_definition_t *definition)
{
    if (provider->get_count == NULL || provider->get_definition == NULL)
    {
        return CONTROLLER_PROTOCOL_PROVIDER_NOT_READY;
    }
    size_t count                                 = 0;
    controller_protocol_provider_result_t result = provider->get_count(provider->context, &count);
    if (result == CONTROLLER_PROTOCOL_PROVIDER_OK && count > PROTOCOL_MAXIMUM_POINT_COUNT)
    {
        return CONTROLLER_PROTOCOL_PROVIDER_FAILED;
    }
    for (size_t index = 0; result == CONTROLLER_PROTOCOL_PROVIDER_OK && index < count; index++)
    {
        result = provider->get_definition(provider->context, index, definition);
        if (result == CONTROLLER_PROTOCOL_PROVIDER_OK && strcmp(definition->id, point_id) == 0)
        {
            return CONTROLLER_PROTOCOL_PROVIDER_OK;
        }
    }
    return result == CONTROLLER_PROTOCOL_PROVIDER_OK ? CONTROLLER_PROTOCOL_PROVIDER_NOT_FOUND : result;
}

/* Dispatches one point-definition read through the abstract provider. */
static void handle_get_point_definition(controller_protocol_t *protocol, const controller_protocol_message_t *request)
{
    char point_id[CONTROLLER_PROTOCOL_POINT_ID_CAPACITY];
    if (!get_requested_point_id(request, point_id, sizeof(point_id)))
    {
        send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
        return;
    }
    controller_protocol_point_definition_t definition = {0};
    const controller_protocol_provider_result_t result =
        get_definition_by_id(&protocol->config.point_provider, point_id, &definition);
    if (result != CONTROLLER_PROTOCOL_PROVIDER_OK)
    {
        protocol->health.provider_error_count++;
        send_error(protocol, request, get_provider_error(result));
        return;
    }
    controller_protocol_message_t response = get_response(protocol, request);
    size_t offset                          = 0;
    if (!append_point_definition(response.payload, sizeof(response.payload), &offset, &definition))
    {
        protocol->health.provider_error_count++;
        send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INTERNAL);
        return;
    }
    response.payload_size = offset;
    send_response(protocol, &response);
}

/* Encodes one typed point value without allowing non-finite or malformed provider data. */
static bool append_point_value(controller_protocol_message_t *response, const controller_protocol_point_value_t *value)
{
    size_t offset = 0;
    if (value->definition.type < CONTROLLER_PROTOCOL_POINT_ANALOG || value->definition.type > CONTROLLER_PROTOCOL_POINT_TEXT ||
        !append_string8(response->payload, sizeof(response->payload), &offset, value->definition.id,
                        sizeof(value->definition.id)) ||
        offset + 5U > sizeof(response->payload))
    {
        return false;
    }
    put_u32(&response->payload[offset], value->definition.revision);
    offset += 4;
    response->payload[offset++] = (uint8_t)value->definition.type;
    switch (value->definition.type)
    {
        case CONTROLLER_PROTOCOL_POINT_ANALOG:
            if (!isfinite(value->value.analog) || offset + sizeof(double) > sizeof(response->payload))
            {
                return false;
            }
            uint64_t analog_bits;
            (void)memcpy(&analog_bits, &value->value.analog, sizeof(analog_bits));
            put_u64(&response->payload[offset], analog_bits);
            offset += sizeof(analog_bits);
            break;
        case CONTROLLER_PROTOCOL_POINT_DIGITAL:
            if (offset + 1U > sizeof(response->payload))
            {
                return false;
            }
            response->payload[offset++] = value->value.digital ? 1U : 0U;
            break;
        case CONTROLLER_PROTOCOL_POINT_INTEGER:
            if (offset + sizeof(int64_t) > sizeof(response->payload))
            {
                return false;
            }
            put_u64(&response->payload[offset], (uint64_t)value->value.integer);
            offset += sizeof(int64_t);
            break;
        case CONTROLLER_PROTOCOL_POINT_MULTI_STATE:
            if (!append_string8(response->payload, sizeof(response->payload), &offset, value->value.text,
                                sizeof(value->value.text)))
            {
                return false;
            }
            break;
        case CONTROLLER_PROTOCOL_POINT_TEXT:
            if (!append_string16(response->payload, sizeof(response->payload), &offset, value->value.text,
                                 sizeof(value->value.text)))
            {
                return false;
            }
            break;
    }
    if (!append_string8(response->payload, sizeof(response->payload), &offset, value->definition.units,
                        sizeof(value->definition.units)) ||
        value->quality > CONTROLLER_PROTOCOL_QUALITY_BAD || offset + 24U > sizeof(response->payload))
    {
        return false;
    }
    response->payload[offset++] = (uint8_t)value->quality;
    put_u16(&response->payload[offset], value->reliability);
    offset += 2;
    response->payload[offset++] = value->definition.service_flags;
    put_u64(&response->payload[offset], (uint64_t)value->source_timestamp_ms);
    offset += 8;
    put_u64(&response->payload[offset], (uint64_t)value->updated_at_ms);
    offset += 8;
    put_u32(&response->payload[offset], value->sequence);
    offset += 4;
    response->payload_size = offset;
    return true;
}

/* Dispatches one point runtime read and keeps unavailable data explicit. */
static void handle_get_point_value(controller_protocol_t *protocol, const controller_protocol_message_t *request)
{
    char point_id[CONTROLLER_PROTOCOL_POINT_ID_CAPACITY];
    if (!get_requested_point_id(request, point_id, sizeof(point_id)))
    {
        send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
        return;
    }
    if (protocol->config.point_provider.get_value == NULL)
    {
        send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_NOT_READY);
        return;
    }
    controller_protocol_point_value_t value = {0};
    const controller_protocol_provider_result_t result =
        protocol->config.point_provider.get_value(protocol->config.point_provider.context, point_id, &value);
    if (result != CONTROLLER_PROTOCOL_PROVIDER_OK)
    {
        protocol->health.provider_error_count++;
        send_error(protocol, request, get_provider_error(result));
        return;
    }
    controller_protocol_message_t response = get_response(protocol, request);
    if (!append_point_value(&response, &value))
    {
        protocol->health.provider_error_count++;
        send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INTERNAL);
        return;
    }
    send_response(protocol, &response);
}

/* Builds the bounded device identity payload shared by discovery and direct information reads. */
static bool set_device_info_payload(const controller_protocol_t *protocol, controller_protocol_message_t *response)
{
    size_t offset = 0;
    if (sizeof(response->payload) < sizeof(uint16_t))
    {
        return false;
    }
    put_u16(response->payload, protocol->config.address);
    offset = 2;
    if (!append_string8(response->payload, sizeof(response->payload), &offset, protocol->config.device_id, UINT8_MAX) ||
        !append_string8(response->payload, sizeof(response->payload), &offset, protocol->config.hardware_model, UINT8_MAX) ||
        !append_string8(response->payload, sizeof(response->payload), &offset, protocol->config.firmware_version, UINT8_MAX))
    {
        return false;
    }
    response->payload_size = offset;
    return true;
}

/* Dispatches one trusted non-discovery request synchronously into the response queue. */
static void dispatch_request(controller_protocol_t *protocol, const controller_protocol_message_t *request)
{
    controller_protocol_message_t response = get_response(protocol, request);
    switch (request->operation)
    {
        case CONTROLLER_PROTOCOL_OPERATION_ECHO:
            response.payload_size = request->payload_size;
            (void)memcpy(response.payload, request->payload, request->payload_size);
            send_response(protocol, &response);
            break;
        case CONTROLLER_PROTOCOL_OPERATION_GET_CAPABILITIES:
            if (request->payload_size != 0)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            response.payload_size = 10;
            response.payload[0]   = PROTOCOL_CAPABILITY_MINOR;
            put_u16(&response.payload[1], CONTROLLER_PROTOCOL_FRAME_CAPACITY);
            put_u16(&response.payload[3], CONTROLLER_PROTOCOL_PAYLOAD_CAPACITY);
            response.payload[5] = PROTOCOL_OPERATION_BITMAP_SIZE;
            response.payload[6] = UINT8_C(0x3e);
            response.payload[7] = 0;
            response.payload[8] = UINT8_C(0x07);
            response.payload[9] = PROTOCOL_POINT_TYPE_MASK;
            send_response(protocol, &response);
            break;
        case CONTROLLER_PROTOCOL_OPERATION_GET_DEVICE_INFO:
            if (request->payload_size != 0 || !set_device_info_payload(protocol, &response))
            {
                send_error(protocol, request,
                           request->payload_size == 0 ? CONTROLLER_PROTOCOL_ERROR_INTERNAL
                                                      : CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
            }
            else
            {
                send_response(protocol, &response);
            }
            break;
        case CONTROLLER_PROTOCOL_OPERATION_GET_HEALTH:
            if (request->payload_size != 0)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            const uint32_t counters[] = {
                protocol->health.accepted_frame_count,       protocol->health.magic_error_count,
                protocol->health.version_error_count,        protocol->health.flag_error_count,
                protocol->health.length_error_count,         protocol->health.crc_error_count,
                protocol->health.address_miss_count,         protocol->health.unsupported_operation_count,
                protocol->health.provider_error_count,       protocol->health.response_drop_count,
                protocol->health.duplicate_transaction_count};
            response.payload_size = sizeof(counters);
            for (size_t index = 0; index < sizeof(counters) / sizeof(counters[0]); index++)
            {
                put_u32(&response.payload[index * sizeof(uint32_t)], counters[index]);
            }
            send_response(protocol, &response);
            break;
        case CONTROLLER_PROTOCOL_OPERATION_LIST_POINTS:
            handle_list_points(protocol, request);
            break;
        case CONTROLLER_PROTOCOL_OPERATION_GET_POINT_DEFINITION:
            handle_get_point_definition(protocol, request);
            break;
        case CONTROLLER_PROTOCOL_OPERATION_GET_POINT_VALUE:
            handle_get_point_value(protocol, request);
            break;
        default:
            protocol->health.unsupported_operation_count++;
            send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_UNSUPPORTED_OPERATION);
            break;
    }
}

/* Records one silent decode rejection in its reason-specific health counter. */
static void record_decode_error(controller_protocol_t *protocol, controller_protocol_decode_result_t result)
{
    switch (result)
    {
        case CONTROLLER_PROTOCOL_DECODE_BAD_MAGIC:
            protocol->health.magic_error_count++;
            break;
        case CONTROLLER_PROTOCOL_DECODE_BAD_VERSION:
            protocol->health.version_error_count++;
            break;
        case CONTROLLER_PROTOCOL_DECODE_BAD_FLAGS:
            protocol->health.flag_error_count++;
            break;
        case CONTROLLER_PROTOCOL_DECODE_BAD_LENGTH:
            protocol->health.length_error_count++;
            break;
        case CONTROLLER_PROTOCOL_DECODE_BAD_CRC:
            protocol->health.crc_error_count++;
            break;
        case CONTROLLER_PROTOCOL_DECODE_OK:
            break;
    }
}

/* Validates and dispatches one owned transport frame without blocking. */
void controller_protocol_receive(controller_protocol_t *protocol, const uint8_t *frame, size_t size, uint64_t now_ms)
{
    if (protocol == NULL)
    {
        return;
    }
    controller_protocol_message_t request;
    const controller_protocol_decode_result_t result = controller_protocol_decode(frame, size, &request);
    if (result != CONTROLLER_PROTOCOL_DECODE_OK)
    {
        record_decode_error(protocol, result);
        return;
    }
    if ((request.flags & PROTOCOL_FLAG_RESPONSE) != 0U)
    {
        protocol->health.flag_error_count++;
        return;
    }
    if (request.destination != protocol->config.address && request.destination != PROTOCOL_BROADCAST_ADDRESS)
    {
        protocol->health.address_miss_count++;
        return;
    }
    protocol->health.accepted_frame_count++;
    if (request.destination == PROTOCOL_BROADCAST_ADDRESS)
    {
        if (request.operation != CONTROLLER_PROTOCOL_OPERATION_DISCOVER || request.payload_size != 7)
        {
            return;
        }
        const unsigned slot_count   = request.payload[4];
        const uint16_t slot_time_ms = get_u16(&request.payload[5]);
        if (slot_count == 0 || slot_count > PROTOCOL_MAXIMUM_SLOTS || slot_time_ms == 0 ||
            slot_time_ms > PROTOCOL_MAXIMUM_SLOT_TIME_MS)
        {
            return;
        }
        uint8_t discovery_seed[PROTOCOL_DISCOVERY_SEED_CAPACITY];
        const size_t identity_size = strlen(protocol->config.device_id);
        /* Hash identity and nonce as one byte sequence exactly as the wire contract requires. */
        (void)memcpy(discovery_seed, protocol->config.device_id, identity_size);
        (void)memcpy(&discovery_seed[identity_size], request.payload, PROTOCOL_DISCOVERY_NONCE_SIZE);
        const uint16_t slot_crc     = controller_protocol_get_crc(discovery_seed, identity_size + PROTOCOL_DISCOVERY_NONCE_SIZE);
        protocol->discovery_request = request;
        protocol->discovery_deadline_ms = now_ms + ((uint64_t)(slot_crc % slot_count) * slot_time_ms);
        protocol->is_discovery_pending  = true;
        return;
    }
    if (protocol->has_cached_response && request.source == protocol->cached_source &&
        request.transaction == protocol->cached_transaction)
    {
        if (size == protocol->cached_request_size && memcmp(frame, protocol->cached_request, size) == 0)
        {
            /* Exact retries receive the cached outcome without invoking a provider twice. */
            protocol->health.duplicate_transaction_count++;
            if (!protocol->send(protocol->send_context, protocol->cached_response, protocol->cached_response_size))
            {
                protocol->health.response_drop_count++;
            }
        }
        else
        {
            /* Reusing a live transaction for different content is ambiguous and invalid. */
            send_error(protocol, &request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
        }
        return;
    }
    protocol->is_request_active   = true;
    protocol->active_request_size = size;
    (void)memcpy(protocol->active_request, frame, size);
    dispatch_request(protocol, &request);
    protocol->is_request_active = false;
}

/* Sends a pending collision-delayed discovery response when its bounded slot expires. */
void controller_protocol_process(controller_protocol_t *protocol, uint64_t now_ms)
{
    if (protocol == NULL || !protocol->is_discovery_pending || now_ms < protocol->discovery_deadline_ms)
    {
        return;
    }
    controller_protocol_message_t response = get_response(protocol, &protocol->discovery_request);
    if (set_device_info_payload(protocol, &response))
    {
        send_response(protocol, &response);
    }
    else
    {
        protocol->health.response_drop_count++;
    }
    protocol->is_discovery_pending = false;
}

/* Gets a read-only snapshot of protocol validation and dispatch counters. */
controller_protocol_health_t controller_protocol_get_health(const controller_protocol_t *protocol)
{
    return protocol != NULL ? protocol->health : (controller_protocol_health_t){0};
}
