#include "controller/protocol.h"

#include <math.h>
#include <string.h>

/* Wire constants define the fixed version-one framing and bounded discovery policy. */
enum
{
    PROTOCOL_MAGIC_FIRST               = 0x46,
    PROTOCOL_MAGIC_SECOND              = 0x43,
    PROTOCOL_VERSION                   = 1,
    PROTOCOL_FLAG_RESPONSE             = 1,
    PROTOCOL_FLAG_ERROR                = 2,
    PROTOCOL_FLAG_AUTHENTICATED        = 4,
    PROTOCOL_FLAG_MORE                 = 8,
    PROTOCOL_ALLOWED_FLAGS             = 0x0f,
    PROTOCOL_BROADCAST_ADDRESS         = 0xffff,
    PROTOCOL_CAPABILITY_MINOR          = 0,
    PROTOCOL_MAXIMUM_SLOTS             = 64,
    PROTOCOL_MAXIMUM_SLOT_TIME_MS      = 1000,
    PROTOCOL_POINT_TYPE_MASK           = 0x1f,
    PROTOCOL_OPERATION_BITMAP_SIZE     = 12,
    PROTOCOL_MAXIMUM_POINT_COUNT       = 1024,
    PROTOCOL_DISCOVERY_NONCE_SIZE      = 4,
    PROTOCOL_DISCOVERY_SEED_CAPACITY   = UINT8_MAX + PROTOCOL_DISCOVERY_NONCE_SIZE,
    PROTOCOL_AUTH_ENVELOPE_PREFIX_SIZE = sizeof(uint32_t) + sizeof(uint64_t),
    PROTOCOL_AUTH_ENVELOPE_SIZE        = PROTOCOL_AUTH_ENVELOPE_PREFIX_SIZE + CONTROLLER_AUTH_TAG_SIZE,
    PROTOCOL_AUTH_BODY_CAPACITY        = CONTROLLER_PROTOCOL_PAYLOAD_CAPACITY - PROTOCOL_AUTH_ENVELOPE_SIZE,
};

/* Reads an unsigned 16-bit little-endian field without assuming buffer alignment. */
static uint16_t get_u16(const uint8_t *data)
{
    return (uint16_t)data[0] | ((uint16_t)data[1] << 8U);
}

/* Reads an unsigned 32-bit little-endian field without assuming buffer alignment. */
static uint32_t get_u32(const uint8_t *data)
{
    return (uint32_t)data[0] | ((uint32_t)data[1] << 8U) | ((uint32_t)data[2] << 16U) | ((uint32_t)data[3] << 24U);
}

/* Reads an unsigned 64-bit little-endian field without assuming buffer alignment. */
static uint64_t get_u64(const uint8_t *data)
{
    uint64_t value = 0;
    for (size_t index = 0; index < sizeof(value); index++)
    {
        value |= (uint64_t)data[index] << (index * 8U);
    }
    return value;
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
    controller_protocol_message_t authenticated_response;
    if (protocol->is_authenticated_dispatch)
    {
        if (response->payload_size + PROTOCOL_AUTH_ENVELOPE_SIZE > CONTROLLER_PROTOCOL_PAYLOAD_CAPACITY)
        {
            protocol->health.response_drop_count++;
            return;
        }
        authenticated_response = *response;
        (void)memmove(&authenticated_response.payload[PROTOCOL_AUTH_ENVELOPE_PREFIX_SIZE], response->payload,
                      response->payload_size);
        put_u32(authenticated_response.payload, protocol->authenticated_session_id);
        uint64_t sequence = 0;
        uint8_t tag[CONTROLLER_AUTH_TAG_SIZE];
        if (!controller_auth_sign_response(protocol->config.auth, response->destination, protocol->authenticated_session_id,
                                           response->operation, response->payload, response->payload_size,
                                           protocol->authenticated_now_ms, &sequence, tag))
        {
            protocol->health.response_drop_count++;
            return;
        }
        put_u64(&authenticated_response.payload[sizeof(uint32_t)], sequence);
        (void)memcpy(&authenticated_response.payload[PROTOCOL_AUTH_ENVELOPE_PREFIX_SIZE + response->payload_size], tag,
                     sizeof(tag));
        authenticated_response.payload_size = response->payload_size + PROTOCOL_AUTH_ENVELOPE_SIZE;
        authenticated_response.flags |= PROTOCOL_FLAG_AUTHENTICATED;
        response = &authenticated_response;
    }
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
    if (protocol->is_session_close_pending)
    {
        /* Close only after signing and queuing the response so the peer can verify completion. */
        controller_auth_close(protocol->config.auth, response->destination, protocol->authenticated_session_id);
        protocol->is_session_close_pending = false;
    }
}

/* Tests whether an operation requires an authenticated envelope on every transport. */
static bool is_authentication_required(uint8_t operation)
{
    return operation == CONTROLLER_PROTOCOL_OPERATION_COMMAND_POINT ||
           operation == CONTROLLER_PROTOCOL_OPERATION_RELINQUISH_COMMAND ||
           operation == CONTROLLER_PROTOCOL_OPERATION_COMMAND_OUTPUT_BLOCK ||
           operation == CONTROLLER_PROTOCOL_OPERATION_CLOSE_SESSION ||
           (operation >= CONTROLLER_PROTOCOL_OPERATION_LIST_FLOWS &&
            operation <= CONTROLLER_PROTOCOL_OPERATION_GET_FLOW_RUNTIME) ||
           (operation >= CONTROLLER_PROTOCOL_OPERATION_DEBUG_LOAD_BEGIN && operation <= CONTROLLER_PROTOCOL_OPERATION_DEBUG_STOP);
}

/* Verifies and removes one authenticated envelope before semantic dispatch. */
static bool unwrap_authenticated_request(controller_protocol_t *protocol, controller_protocol_message_t *request, uint64_t now_ms)
{
    if (protocol->config.auth == NULL || request->payload_size < PROTOCOL_AUTH_ENVELOPE_SIZE)
    {
        return false;
    }
    const uint32_t session_id = get_u32(request->payload);
    uint64_t sequence         = 0;
    for (size_t index = 0; index < sizeof(sequence); index++)
    {
        sequence |= (uint64_t)request->payload[sizeof(session_id) + index] << (index * 8U);
    }
    const size_t body_size = request->payload_size - PROTOCOL_AUTH_ENVELOPE_SIZE;
    const uint8_t *body    = &request->payload[PROTOCOL_AUTH_ENVELOPE_PREFIX_SIZE];
    const uint8_t *tag     = &body[body_size];
    if (!controller_auth_verify_request(protocol->config.auth, request->source, session_id, sequence, request->operation, body,
                                        body_size, tag, now_ms))
    {
        return false;
    }
    (void)memmove(request->payload, body, body_size);
    request->payload_size               = body_size;
    request->flags                      = 0;
    protocol->is_authenticated_dispatch = true;
    protocol->authenticated_session_id  = session_id;
    protocol->authenticated_now_ms      = now_ms;
    return true;
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

/* Decodes a bounded point ID followed by one strict Boolean output value. */
static bool get_output_command(const controller_protocol_message_t *request, char *point_id, size_t capacity, bool *value)
{
    if (request->payload_size < 3)
    {
        return false;
    }
    const size_t id_size = request->payload[0];
    if (id_size == 0 || id_size >= capacity || request->payload_size != id_size + 2U || request->payload[id_size + 1U] > 1U)
    {
        return false;
    }
    (void)memcpy(point_id, &request->payload[1], id_size);
    point_id[id_size] = '\0';
    *value            = request->payload[id_size + 1U] != 0U;
    return true;
}

/* Parses one authenticated output command with source, priority, correlation, and lifetime metadata. */
static bool get_arbitrated_command(const controller_protocol_message_t *request, controller_point_command_t *command)
{
    if (request->payload_size < 5)
    {
        return false;
    }
    size_t offset   = 0;
    command->output = request->payload[offset++];
    if (request->payload[offset] > 1U)
    {
        return false;
    }
    command->value           = request->payload[offset++] != 0U;
    const size_t source_size = request->payload[offset++];
    if (source_size == 0 || source_size >= sizeof(command->source_id) || offset + source_size + 3U > request->payload_size)
    {
        return false;
    }
    (void)memcpy(command->source_id, &request->payload[offset], source_size);
    command->source_id[source_size] = '\0';
    offset += source_size;
    command->command_class        = request->payload[offset++];
    command->priority             = request->payload[offset++];
    const size_t correlation_size = request->payload[offset++];
    if (correlation_size == 0 || correlation_size >= sizeof(command->correlation_id) ||
        offset + correlation_size + 16U != request->payload_size)
    {
        return false;
    }
    (void)memcpy(command->correlation_id, &request->payload[offset], correlation_size);
    command->correlation_id[correlation_size] = '\0';
    offset += correlation_size;
    command->issued_at_ms = (int64_t)get_u64(&request->payload[offset]);
    offset += 8;
    command->expires_at_ms = (int64_t)get_u64(&request->payload[offset]);
    return true;
}

/* Maps point arbitration results into stable protocol errors. */
static controller_protocol_error_t get_point_error(controller_point_result_t result)
{
    switch (result)
    {
        case CONTROLLER_POINT_INVALID_ARGUMENT:
            return CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT;
        case CONTROLLER_POINT_NOT_FOUND:
            return CONTROLLER_PROTOCOL_ERROR_NOT_FOUND;
        case CONTROLLER_POINT_QUEUE_FULL:
            return CONTROLLER_PROTOCOL_ERROR_QUEUE_FULL;
        case CONTROLLER_POINT_NOT_READY:
            return CONTROLLER_PROTOCOL_ERROR_NOT_READY;
        case CONTROLLER_POINT_FAILED:
            return CONTROLLER_PROTOCOL_ERROR_INTERNAL;
        case CONTROLLER_POINT_OK:
            return CONTROLLER_PROTOCOL_ERROR_INTERNAL;
    }
    return CONTROLLER_PROTOCOL_ERROR_INTERNAL;
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

/* Maps flow-service outcomes into the stable protocol error vocabulary. */
static controller_protocol_error_t get_flow_error(controller_flow_result_t result)
{
    switch (result)
    {
        case CONTROLLER_FLOW_INVALID_ARGUMENT:
            return CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT;
        case CONTROLLER_FLOW_WRONG_STATE:
            return CONTROLLER_PROTOCOL_ERROR_WRONG_STATE;
        case CONTROLLER_FLOW_REVISION_CONFLICT:
            return CONTROLLER_PROTOCOL_ERROR_REVISION_CONFLICT;
        case CONTROLLER_FLOW_STORAGE_UNAVAILABLE:
            return CONTROLLER_PROTOCOL_ERROR_STORAGE_UNAVAILABLE;
        case CONTROLLER_FLOW_STORAGE_FULL:
            return CONTROLLER_PROTOCOL_ERROR_STORAGE_FULL;
        case CONTROLLER_FLOW_DIGEST_MISMATCH:
            return CONTROLLER_PROTOCOL_ERROR_DIGEST_MISMATCH;
        case CONTROLLER_FLOW_VALIDATION_FAILED:
            return CONTROLLER_PROTOCOL_ERROR_VALIDATION_FAILED;
        case CONTROLLER_FLOW_NOT_FOUND:
            return CONTROLLER_PROTOCOL_ERROR_NOT_FOUND;
        case CONTROLLER_FLOW_OK:
            return CONTROLLER_PROTOCOL_ERROR_INTERNAL;
    }
    return CONTROLLER_PROTOCOL_ERROR_INTERNAL;
}

/* Encodes one bounded committed-flow metadata record. */
static bool append_flow_metadata(controller_protocol_message_t *response, const controller_flow_metadata_t *metadata)
{
    size_t offset = 0;
    if (!append_string8(response->payload, sizeof(response->payload), &offset, metadata->id, sizeof(metadata->id)) ||
        offset + 45U > sizeof(response->payload))
    {
        return false;
    }
    put_u32(&response->payload[offset], metadata->revision);
    offset += 4;
    put_u32(&response->payload[offset], metadata->artifact_schema);
    offset += 4;
    put_u32(&response->payload[offset], (uint32_t)metadata->size);
    offset += 4;
    (void)memcpy(&response->payload[offset], metadata->digest, CONTROLLER_FLOW_DIGEST_SIZE);
    offset += CONTROLLER_FLOW_DIGEST_SIZE;
    response->payload[offset++] = metadata->is_active ? 1U : 0U;
    response->payload_size      = offset;
    return true;
}

/* Parses upload-begin metadata and optimistic revision fields from one authenticated body. */
static bool get_upload_metadata(const controller_protocol_message_t *request, controller_flow_metadata_t *metadata,
                                bool *has_expected_revision, uint32_t *expected_revision)
{
    if (request->payload_size < 1)
    {
        return false;
    }
    const size_t id_size    = request->payload[0];
    const size_t fixed_size = 1U + id_size + 4U + 4U + 4U + CONTROLLER_FLOW_DIGEST_SIZE + 1U + 4U;
    if (id_size == 0 || id_size >= sizeof(metadata->id) || request->payload_size != fixed_size)
    {
        return false;
    }
    size_t offset = 1;
    (void)memcpy(metadata->id, &request->payload[offset], id_size);
    metadata->id[id_size] = '\0';
    offset += id_size;
    metadata->revision = get_u32(&request->payload[offset]);
    offset += 4;
    metadata->artifact_schema = get_u32(&request->payload[offset]);
    offset += 4;
    metadata->size = get_u32(&request->payload[offset]);
    offset += 4;
    (void)memcpy(metadata->digest, &request->payload[offset], CONTROLLER_FLOW_DIGEST_SIZE);
    offset += CONTROLLER_FLOW_DIGEST_SIZE;
    if (request->payload[offset] > 1U)
    {
        return false;
    }
    *has_expected_revision = request->payload[offset++] != 0U;
    *expected_revision     = get_u32(&request->payload[offset]);
    return true;
}

/* Sends a stable mapped error or reports a successful flow operation to its caller. */
static bool is_flow_result_success(controller_protocol_t *protocol, const controller_protocol_message_t *request,
                                   controller_flow_result_t result)
{
    if (result == CONTROLLER_FLOW_OK)
    {
        return true;
    }
    send_error(protocol, request, get_flow_error(result));
    return false;
}

/* Maps volatile debug service outcomes into stable protocol errors. */
static controller_protocol_error_t get_debug_error(flow_debug_result_t result)
{
    switch (result)
    {
        case FLOW_DEBUG_INVALID_ARGUMENT:
            return CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT;
        case FLOW_DEBUG_WRONG_STATE:
            return CONTROLLER_PROTOCOL_ERROR_WRONG_STATE;
        case FLOW_DEBUG_NOT_FOUND:
            return CONTROLLER_PROTOCOL_ERROR_NOT_FOUND;
        case FLOW_DEBUG_FORBIDDEN:
            return CONTROLLER_PROTOCOL_ERROR_UNAUTHORIZED;
        case FLOW_DEBUG_CONFLICT:
            return CONTROLLER_PROTOCOL_ERROR_BUSY;
        case FLOW_DEBUG_DIGEST_MISMATCH:
            return CONTROLLER_PROTOCOL_ERROR_DIGEST_MISMATCH;
        case FLOW_DEBUG_VALIDATION_FAILED:
            return CONTROLLER_PROTOCOL_ERROR_VALIDATION_FAILED;
        case FLOW_DEBUG_OK:
            return CONTROLLER_PROTOCOL_ERROR_INTERNAL;
    }
    return CONTROLLER_PROTOCOL_ERROR_INTERNAL;
}

/* Sends a mapped debug failure or permits success encoding to continue. */
static bool is_debug_result_success(controller_protocol_t *protocol, const controller_protocol_message_t *request,
                                    flow_debug_result_t result)
{
    if (result == FLOW_DEBUG_OK)
    {
        return true;
    }
    send_error(protocol, request, get_debug_error(result));
    return false;
}

/* Encodes the shared bounded debug status response. */
static bool set_debug_status_payload(controller_protocol_message_t *response, const flow_debug_status_t *status)
{
    size_t offset = 0;
    put_u64(&response->payload[offset], status->session_id);
    offset += sizeof(uint64_t);
    response->payload[offset++] = (uint8_t)status->state;
    put_u32(&response->payload[offset], status->covered_bytes);
    offset += sizeof(uint32_t);
    put_u32(&response->payload[offset], status->artifact_length);
    offset += sizeof(uint32_t);
    put_u32(&response->payload[offset], status->flow_revision);
    offset += sizeof(uint32_t);
    put_u64(&response->payload[offset], status->tick_number);
    offset += sizeof(uint64_t);
    put_u32(&response->payload[offset], status->lease_remaining_ms);
    offset += sizeof(uint32_t);
    put_u16(&response->payload[offset], (uint16_t)status->last_result.code);
    offset += sizeof(uint16_t);
    if (!append_string8(response->payload, PROTOCOL_AUTH_BODY_CAPACITY, &offset, status->last_result.path,
                        sizeof(status->last_result.path)))
    {
        return false;
    }
    response->payload_size = offset;
    return true;
}

/* Dispatches one trusted non-discovery request synchronously into the response queue. */
static void dispatch_request(controller_protocol_t *protocol, const controller_protocol_message_t *request, uint64_t now_ms)
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
            response.payload_size = 19;
            response.payload[0]   = PROTOCOL_CAPABILITY_MINOR;
            put_u16(&response.payload[1], CONTROLLER_PROTOCOL_FRAME_CAPACITY);
            put_u16(&response.payload[3], CONTROLLER_PROTOCOL_PAYLOAD_CAPACITY);
            response.payload[5]  = PROTOCOL_OPERATION_BITMAP_SIZE;
            response.payload[6]  = UINT8_C(0x3e);
            response.payload[7]  = 0;
            response.payload[8]  = UINT8_C(0x3f);
            response.payload[9]  = UINT8_C(0x07);
            response.payload[10] = 0;
            response.payload[11] = 0;
            response.payload[12] = UINT8_C(0x07);
            response.payload[13] = 0;
            response.payload[14] = UINT8_MAX;
            response.payload[15] = UINT8_C(0x3f);
            response.payload[16] = UINT8_MAX;
            response.payload[17] = UINT8_C(0x01);
            response.payload[18] = PROTOCOL_POINT_TYPE_MASK;
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
        case CONTROLLER_PROTOCOL_OPERATION_GET_IO_BLOCK:
            if (request->payload_size != 0)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            if (protocol->config.get_io_block == NULL)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_NOT_READY);
                break;
            }
            controller_protocol_io_block_t block;
            const controller_protocol_provider_result_t block_result =
                protocol->config.get_io_block(protocol->config.io_context, &block);
            if (block_result != CONTROLLER_PROTOCOL_PROVIDER_OK)
            {
                protocol->health.provider_error_count++;
                send_error(protocol, request, get_provider_error(block_result));
                break;
            }
            put_u16(&response.payload[0], block.inputs);
            put_u16(&response.payload[2], block.outputs);
            response.payload[4] = block.validity_flags;
            put_u64(&response.payload[5], (uint64_t)block.sampled_at_ms);
            put_u32(&response.payload[13], block.sequence);
            response.payload_size = 17;
            send_response(protocol, &response);
            break;
        case CONTROLLER_PROTOCOL_OPERATION_COMMAND_POINT: {
            if (protocol->is_authenticated_dispatch)
            {
                controller_point_command_t command = {0};
                if (protocol->config.points == NULL || !get_arbitrated_command(request, &command))
                {
                    send_error(protocol, request,
                               protocol->config.points == NULL ? CONTROLLER_PROTOCOL_ERROR_NOT_READY
                                                               : CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                    break;
                }
                const controller_point_result_t result =
                    controller_points_command(protocol->config.points, &command, (int64_t)now_ms);
                if (result != CONTROLLER_POINT_OK)
                {
                    send_error(protocol, request, get_point_error(result));
                    break;
                }
                response.payload_size = request->payload_size;
                (void)memcpy(response.payload, request->payload, request->payload_size);
                send_response(protocol, &response);
                break;
            }
            char output_id[CONTROLLER_PROTOCOL_POINT_ID_CAPACITY];
            bool output_value = false;
            if (!get_output_command(request, output_id, sizeof(output_id), &output_value))
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            if (protocol->config.set_output == NULL)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_NOT_READY);
                break;
            }
            const controller_protocol_provider_result_t output_result =
                protocol->config.set_output(protocol->config.io_context, output_id, output_value);
            if (output_result != CONTROLLER_PROTOCOL_PROVIDER_OK)
            {
                protocol->health.provider_error_count++;
                send_error(protocol, request, get_provider_error(output_result));
                break;
            }
            response.payload_size = request->payload_size;
            (void)memcpy(response.payload, request->payload, request->payload_size);
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_RELINQUISH_COMMAND: {
            if (protocol->config.points == NULL || request->payload_size < 3)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            const uint8_t output     = request->payload[0];
            const size_t source_size = request->payload[1];
            if (source_size == 0 || source_size >= CONTROLLER_POINT_SOURCE_ID_CAPACITY ||
                request->payload_size != source_size + 2U)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            char source_id[CONTROLLER_POINT_SOURCE_ID_CAPACITY];
            (void)memcpy(source_id, &request->payload[2], source_size);
            source_id[source_size] = '\0';
            const controller_point_result_t result =
                controller_points_relinquish(protocol->config.points, output, source_id, (int64_t)now_ms);
            if (result != CONTROLLER_POINT_OK)
            {
                send_error(protocol, request, get_point_error(result));
                break;
            }
            response.payload_size = request->payload_size;
            (void)memcpy(response.payload, request->payload, request->payload_size);
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_SUBSCRIBE_CHANGES: {
            if (protocol->config.points == NULL || request->payload_size != sizeof(uint16_t))
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            const controller_point_result_t result =
                controller_points_subscribe(protocol->config.points, request->source, get_u16(request->payload));
            if (result != CONTROLLER_POINT_OK)
            {
                send_error(protocol, request, get_point_error(result));
                break;
            }
            response.payload_size = request->payload_size;
            (void)memcpy(response.payload, request->payload, request->payload_size);
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_POINT_CHANGE_EVENT: {
            if (protocol->config.points == NULL || request->payload_size != 0)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            uint16_t changed  = 0;
            uint16_t values   = 0;
            uint32_t sequence = 0;
            bool has_gap      = false;
            const controller_point_result_t result =
                controller_points_get_event(protocol->config.points, request->source, &changed, &values, &sequence, &has_gap);
            if (result != CONTROLLER_POINT_OK)
            {
                send_error(protocol, request, get_point_error(result));
                break;
            }
            put_u16(response.payload, changed);
            put_u16(&response.payload[2], values);
            put_u32(&response.payload[4], sequence);
            response.payload[8]   = has_gap ? 1U : 0U;
            response.payload_size = 9;
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_COMMAND_OUTPUT_BLOCK: {
            if (request->payload_size != sizeof(uint16_t))
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            if (protocol->config.set_output_block == NULL)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_NOT_READY);
                break;
            }
            const uint16_t requested_outputs = get_u16(request->payload);
            const controller_protocol_provider_result_t output_block_result =
                protocol->config.set_output_block(protocol->config.io_context, requested_outputs);
            if (output_block_result != CONTROLLER_PROTOCOL_PROVIDER_OK)
            {
                protocol->health.provider_error_count++;
                send_error(protocol, request, get_provider_error(output_block_result));
                break;
            }
            put_u16(response.payload, requested_outputs);
            response.payload_size = sizeof(uint16_t);
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_AUTH_CHALLENGE: {
            if (request->payload_size != CONTROLLER_AUTH_NONCE_SIZE || protocol->config.auth == NULL)
            {
                send_error(protocol, request,
                           request->payload_size == CONTROLLER_AUTH_NONCE_SIZE ? CONTROLLER_PROTOCOL_ERROR_NOT_READY
                                                                               : CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            uint32_t session_id = 0;
            uint8_t device_nonce[CONTROLLER_AUTH_NONCE_SIZE];
            if (!controller_auth_create_challenge(protocol->config.auth, request->source, request->payload, now_ms, &session_id,
                                                  device_nonce))
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_BUSY);
                break;
            }
            put_u32(response.payload, session_id);
            (void)memcpy(&response.payload[sizeof(session_id)], device_nonce, sizeof(device_nonce));
            response.payload_size = sizeof(session_id) + sizeof(device_nonce);
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_AUTH_PROVE: {
            const size_t proof_payload_size = sizeof(uint32_t) + CONTROLLER_AUTH_TAG_SIZE;
            if (request->payload_size != proof_payload_size || protocol->config.auth == NULL)
            {
                send_error(protocol, request,
                           request->payload_size == proof_payload_size ? CONTROLLER_PROTOCOL_ERROR_NOT_READY
                                                                       : CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            const uint32_t session_id = get_u32(request->payload);
            if (!controller_auth_verify_proof(protocol->config.auth, request->source, session_id,
                                              &request->payload[sizeof(session_id)], now_ms))
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_UNAUTHORIZED);
                break;
            }
            put_u32(response.payload, session_id);
            response.payload_size = sizeof(session_id);
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_CLOSE_SESSION: {
            if (request->payload_size != sizeof(uint32_t) || protocol->config.auth == NULL)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            if (get_u32(request->payload) != protocol->authenticated_session_id)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            /* Defer invalidation until send_response has signed the final authenticated response. */
            protocol->is_session_close_pending = true;
            response.payload_size              = 0;
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_LIST_FLOWS: {
            if (request->payload_size != 0 || protocol->config.flow == NULL)
            {
                send_error(protocol, request,
                           protocol->config.flow == NULL ? CONTROLLER_PROTOCOL_ERROR_NOT_READY
                                                         : CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            controller_flow_metadata_t metadata;
            const controller_flow_result_t result = controller_flow_get_metadata(protocol->config.flow, &metadata);
            response.payload[0]                   = result == CONTROLLER_FLOW_OK ? 1U : 0U;
            response.payload_size                 = 1;
            if (result == CONTROLLER_FLOW_OK)
            {
                controller_protocol_message_t encoded = response;
                if (!append_flow_metadata(&encoded, &metadata) || encoded.payload_size + 1U > PROTOCOL_AUTH_BODY_CAPACITY)
                {
                    send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INTERNAL);
                    break;
                }
                (void)memmove(&response.payload[1], encoded.payload, encoded.payload_size);
                response.payload_size = encoded.payload_size + 1U;
            }
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_GET_FLOW_METADATA:
        case CONTROLLER_PROTOCOL_OPERATION_DOWNLOAD_BEGIN:
        case CONTROLLER_PROTOCOL_OPERATION_GET_FLOW_RUNTIME: {
            if (request->payload_size != 0 || protocol->config.flow == NULL)
            {
                send_error(protocol, request,
                           protocol->config.flow == NULL ? CONTROLLER_PROTOCOL_ERROR_NOT_READY
                                                         : CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            controller_flow_metadata_t metadata;
            if (!is_flow_result_success(protocol, request, controller_flow_get_metadata(protocol->config.flow, &metadata)))
            {
                break;
            }
            if (!append_flow_metadata(&response, &metadata) || response.payload_size > PROTOCOL_AUTH_BODY_CAPACITY)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INTERNAL);
                break;
            }
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_UPLOAD_BEGIN: {
            if (protocol->config.flow == NULL)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_NOT_READY);
                break;
            }
            controller_flow_metadata_t metadata = {0};
            bool has_expected_revision          = false;
            uint32_t expected_revision          = 0;
            if (!get_upload_metadata(request, &metadata, &has_expected_revision, &expected_revision))
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            uint32_t transfer_id = ((uint32_t)request->source << 16U) | request->transaction;
            if (transfer_id == 0)
            {
                transfer_id = 1;
            }
            if (!is_flow_result_success(protocol, request,
                                        controller_flow_begin(protocol->config.flow, &metadata, has_expected_revision,
                                                              expected_revision, transfer_id)))
            {
                break;
            }
            put_u32(response.payload, transfer_id);
            put_u16(&response.payload[4], (uint16_t)(PROTOCOL_AUTH_BODY_CAPACITY - 8U));
            response.payload_size = 6;
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_UPLOAD_STATUS: {
            if (request->payload_size != sizeof(uint32_t) || protocol->config.flow == NULL ||
                !protocol->config.flow->is_transfer_open || protocol->config.flow->transfer_id != get_u32(request->payload))
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_WRONG_STATE);
                break;
            }
            put_u32(response.payload, protocol->config.flow->transfer_id);
            put_u32(&response.payload[4], (uint32_t)protocol->config.flow->covered_bytes);
            put_u32(&response.payload[8], (uint32_t)protocol->config.flow->staging.size);
            response.payload[12]  = protocol->config.flow->is_validated ? 1U : 0U;
            response.payload_size = 13;
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_UPLOAD_CHUNK: {
            if (request->payload_size <= 8 || protocol->config.flow == NULL)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            const uint32_t transfer_id = get_u32(request->payload);
            const uint32_t offset      = get_u32(&request->payload[4]);
            if (!is_flow_result_success(protocol, request,
                                        controller_flow_write(protocol->config.flow, transfer_id, offset, &request->payload[8],
                                                              request->payload_size - 8U)))
            {
                break;
            }
            put_u32(response.payload, transfer_id);
            put_u32(&response.payload[4], (uint32_t)protocol->config.flow->covered_bytes);
            response.payload_size = 8;
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_UPLOAD_VALIDATE:
        case CONTROLLER_PROTOCOL_OPERATION_UPLOAD_COMMIT:
        case CONTROLLER_PROTOCOL_OPERATION_UPLOAD_ABORT: {
            if (request->payload_size != sizeof(uint32_t) || protocol->config.flow == NULL)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            const uint32_t transfer_id      = get_u32(request->payload);
            controller_flow_result_t result = CONTROLLER_FLOW_WRONG_STATE;
            if (request->operation == CONTROLLER_PROTOCOL_OPERATION_UPLOAD_VALIDATE)
            {
                result = controller_flow_validate(protocol->config.flow, transfer_id);
            }
            else if (request->operation == CONTROLLER_PROTOCOL_OPERATION_UPLOAD_COMMIT)
            {
                result = controller_flow_commit(protocol->config.flow, transfer_id);
            }
            else
            {
                result = controller_flow_abort(protocol->config.flow, transfer_id);
            }
            if (!is_flow_result_success(protocol, request, result))
            {
                break;
            }
            put_u32(response.payload, transfer_id);
            response.payload_size = sizeof(transfer_id);
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_DOWNLOAD_CHUNK: {
            if (request->payload_size != 5 || protocol->config.flow == NULL || request->payload[4] == 0)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            const uint32_t offset = get_u32(request->payload);
            const size_t maximum =
                request->payload[4] < PROTOCOL_AUTH_BODY_CAPACITY - 4U ? request->payload[4] : PROTOCOL_AUTH_BODY_CAPACITY - 4U;
            size_t downloaded_size = 0;
            const controller_flow_result_t result =
                controller_flow_read(protocol->config.flow, offset, &response.payload[4], maximum, &downloaded_size);
            if (!is_flow_result_success(protocol, request, result))
            {
                break;
            }
            put_u32(response.payload, offset);
            response.payload_size = 4U + downloaded_size;
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_ACTIVATE_FLOW:
        case CONTROLLER_PROTOCOL_OPERATION_DEACTIVATE_FLOW: {
            if (request->payload_size != 0 || protocol->config.flow == NULL)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            if (!is_flow_result_success(
                    protocol, request,
                    controller_flow_set_active(protocol->config.flow,
                                               request->operation == CONTROLLER_PROTOCOL_OPERATION_ACTIVATE_FLOW)))
            {
                break;
            }
            response.payload_size = 0;
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_REMOVE_FLOW: {
            if (request->payload_size != 0 || protocol->config.flow == NULL)
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            if (!is_flow_result_success(protocol, request, controller_flow_remove(protocol->config.flow)))
            {
                break;
            }
            response.payload_size = 0;
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_DEBUG_LOAD_BEGIN: {
            const size_t expected_size = sizeof(uint32_t) + 1U + sizeof(uint32_t) + FLOW_DEBUG_DIGEST_BYTES;
            if (protocol->config.debug == NULL || request->payload_size != expected_size || request->payload[4] > 1U)
            {
                send_error(protocol, request,
                           protocol->config.debug == NULL ? CONTROLLER_PROTOCOL_ERROR_NOT_READY
                                                          : CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            uint64_t session_id = 0;
            const flow_debug_result_t result =
                flow_debug_begin(protocol->config.debug, protocol->authenticated_session_id, request->payload[4] != 0U,
                                 get_u32(&request->payload[5]), &request->payload[9], now_ms, &session_id);
            if (!is_debug_result_success(protocol, request, result))
            {
                break;
            }
            put_u64(response.payload, session_id);
            put_u16(&response.payload[8], FLOW_DEBUG_CHUNK_LIMIT);
            put_u32(&response.payload[10], FLOW_DEBUG_LEASE_MS);
            response.payload_size = 14;
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_DEBUG_LOAD_CHUNK: {
            const size_t prefix_size = sizeof(uint64_t) + sizeof(uint32_t);
            if (protocol->config.debug == NULL || request->payload_size <= prefix_size ||
                request->payload_size - prefix_size > FLOW_DEBUG_CHUNK_LIMIT)
            {
                send_error(protocol, request,
                           protocol->config.debug == NULL ? CONTROLLER_PROTOCOL_ERROR_NOT_READY
                                                          : CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            const size_t data_size = request->payload_size - prefix_size;
            const uint32_t offset  = get_u32(&request->payload[sizeof(uint64_t)]);
            const flow_debug_result_t result =
                flow_debug_write(protocol->config.debug, protocol->authenticated_session_id, get_u64(request->payload), offset,
                                 &request->payload[prefix_size], data_size, now_ms);
            if (!is_debug_result_success(protocol, request, result))
            {
                break;
            }
            put_u32(response.payload, offset);
            put_u16(&response.payload[4], (uint16_t)data_size);
            response.payload_size = 6;
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_DEBUG_PREPARE:
        case CONTROLLER_PROTOCOL_OPERATION_DEBUG_STATUS: {
            if (protocol->config.debug == NULL || request->payload_size != sizeof(uint64_t))
            {
                send_error(protocol, request,
                           protocol->config.debug == NULL ? CONTROLLER_PROTOCOL_ERROR_NOT_READY
                                                          : CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            const uint64_t session_id  = get_u64(request->payload);
            flow_debug_result_t result = FLOW_DEBUG_OK;
            if (request->operation == CONTROLLER_PROTOCOL_OPERATION_DEBUG_PREPARE)
            {
                result = flow_debug_prepare(protocol->config.debug, protocol->authenticated_session_id, session_id, now_ms);
            }
            flow_debug_status_t status;
            if (result == FLOW_DEBUG_OK)
            {
                result = flow_debug_get_status(protocol->config.debug, protocol->authenticated_session_id, session_id, now_ms,
                                               &status);
            }
            if (!is_debug_result_success(protocol, request, result))
            {
                break;
            }
            if (!set_debug_status_payload(&response, &status))
            {
                send_error(protocol, request, CONTROLLER_PROTOCOL_ERROR_INTERNAL);
                break;
            }
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_DEBUG_STEP: {
            if (protocol->config.debug == NULL || request->payload_size != sizeof(uint64_t))
            {
                send_error(protocol, request,
                           protocol->config.debug == NULL ? CONTROLLER_PROTOCOL_ERROR_NOT_READY
                                                          : CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            const uint64_t session_id = get_u64(request->payload);
            flow_debug_result_t result =
                flow_debug_step(protocol->config.debug, protocol->authenticated_session_id, session_id, now_ms);
            flow_debug_snapshot_header_t header;
            if (result == FLOW_DEBUG_OK)
            {
                result = flow_debug_get_snapshot_header(protocol->config.debug, protocol->authenticated_session_id, session_id,
                                                        protocol->config.debug->runtime.tick_number, now_ms, &header);
            }
            if (!is_debug_result_success(protocol, request, result))
            {
                break;
            }
            put_u64(response.payload, header.tick_number);
            put_u32(&response.payload[8], header.total_length);
            (void)memcpy(&response.payload[12], header.digest, sizeof(header.digest));
            response.payload_size = 12U + sizeof(header.digest);
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_DEBUG_SNAPSHOT_HEADER: {
            if (protocol->config.debug == NULL || request->payload_size != 2U * sizeof(uint64_t))
            {
                send_error(protocol, request,
                           protocol->config.debug == NULL ? CONTROLLER_PROTOCOL_ERROR_NOT_READY
                                                          : CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            flow_debug_snapshot_header_t header;
            const flow_debug_result_t result = flow_debug_get_snapshot_header(
                protocol->config.debug, protocol->authenticated_session_id, get_u64(request->payload),
                get_u64(&request->payload[sizeof(uint64_t)]), now_ms, &header);
            if (!is_debug_result_success(protocol, request, result))
            {
                break;
            }
            put_u64(response.payload, header.session_id);
            put_u64(&response.payload[8], header.tick_number);
            put_u32(&response.payload[16], header.total_length);
            put_u16(&response.payload[20], header.chunk_count);
            put_u16(&response.payload[22], header.chunk_data_limit);
            (void)memcpy(&response.payload[24], header.digest, sizeof(header.digest));
            response.payload_size = 24U + sizeof(header.digest);
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_DEBUG_SNAPSHOT_CHUNK: {
            const size_t expected_size = 2U * sizeof(uint64_t) + sizeof(uint16_t);
            if (protocol->config.debug == NULL || request->payload_size != expected_size)
            {
                send_error(protocol, request,
                           protocol->config.debug == NULL ? CONTROLLER_PROTOCOL_ERROR_NOT_READY
                                                          : CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            const uint64_t session_id        = get_u64(request->payload);
            const uint64_t tick_number       = get_u64(&request->payload[8]);
            const uint16_t chunk_index       = get_u16(&request->payload[16]);
            uint32_t offset                  = 0;
            size_t chunk_size                = 0;
            const flow_debug_result_t result = flow_debug_read_snapshot_chunk(
                protocol->config.debug, protocol->authenticated_session_id, session_id, tick_number, chunk_index, now_ms,
                &response.payload[24], PROTOCOL_AUTH_BODY_CAPACITY - 24U, &offset, &chunk_size);
            if (!is_debug_result_success(protocol, request, result))
            {
                break;
            }
            flow_debug_snapshot_header_t header;
            if (!is_debug_result_success(protocol, request,
                                         flow_debug_get_snapshot_header(protocol->config.debug,
                                                                        protocol->authenticated_session_id, session_id,
                                                                        tick_number, now_ms, &header)))
            {
                break;
            }
            put_u64(response.payload, session_id);
            put_u64(&response.payload[8], tick_number);
            put_u16(&response.payload[16], chunk_index);
            put_u16(&response.payload[18], header.chunk_count);
            put_u32(&response.payload[20], offset);
            response.payload_size = 24U + chunk_size;
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_DEBUG_RENEW: {
            if (protocol->config.debug == NULL || request->payload_size != sizeof(uint64_t))
            {
                send_error(protocol, request,
                           protocol->config.debug == NULL ? CONTROLLER_PROTOCOL_ERROR_NOT_READY
                                                          : CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            if (!is_debug_result_success(protocol, request,
                                         flow_debug_renew(protocol->config.debug, protocol->authenticated_session_id,
                                                          get_u64(request->payload), now_ms)))
            {
                break;
            }
            put_u32(response.payload, FLOW_DEBUG_LEASE_MS);
            response.payload_size = sizeof(uint32_t);
            send_response(protocol, &response);
            break;
        }
        case CONTROLLER_PROTOCOL_OPERATION_DEBUG_STOP: {
            if (protocol->config.debug == NULL || request->payload_size != sizeof(uint64_t))
            {
                send_error(protocol, request,
                           protocol->config.debug == NULL ? CONTROLLER_PROTOCOL_ERROR_NOT_READY
                                                          : CONTROLLER_PROTOCOL_ERROR_INVALID_ARGUMENT);
                break;
            }
            const uint64_t session_id = get_u64(request->payload);
            if (!is_debug_result_success(protocol, request,
                                         flow_debug_stop(protocol->config.debug, protocol->authenticated_session_id, session_id)))
            {
                break;
            }
            put_u64(response.payload, session_id);
            response.payload_size = sizeof(uint64_t);
            send_response(protocol, &response);
            break;
        }
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
    if ((request.flags & PROTOCOL_FLAG_AUTHENTICATED) != 0U)
    {
        if (!unwrap_authenticated_request(protocol, &request, now_ms))
        {
            send_error(protocol, &request, CONTROLLER_PROTOCOL_ERROR_UNAUTHORIZED);
            protocol->is_request_active = false;
            return;
        }
    }
    else if (is_authentication_required(request.operation))
    {
        send_error(protocol, &request, CONTROLLER_PROTOCOL_ERROR_UNAUTHORIZED);
        protocol->is_request_active = false;
        return;
    }
    dispatch_request(protocol, &request, now_ms);
    protocol->is_authenticated_dispatch = false;
    protocol->is_session_close_pending  = false;
    protocol->is_request_active         = false;
}

/* Sends a pending collision-delayed discovery response when its bounded slot expires. */
void controller_protocol_process(controller_protocol_t *protocol, uint64_t now_ms)
{
    if (protocol == NULL)
    {
        return;
    }
    if (protocol->config.debug != NULL)
    {
        flow_debug_process(protocol->config.debug, now_ms);
    }
    if (!protocol->is_discovery_pending || now_ms < protocol->discovery_deadline_ms)
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
