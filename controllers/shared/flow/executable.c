#include "flow/executable.h"

/*
 * Purpose: Implement the controller's portable decoder, validator, target
 * resolver, and deterministic scheduler for schema-1 executable artifacts.
 * This file accepts the canonical binary artifact produced by the backend and
 * turns it into the normalized flow_executable_t consumed by runtime.c.
 *
 * Why this file exists: The architecture defines the controller implementation
 * as the authority for executable semantics. Untrusted transfer bytes must not
 * reach tick evaluation until their structure, meaning, hardware compatibility,
 * and execution order have all been proven. Keeping that work in one portable
 * module also lets host fixture tests exercise the same behavior as firmware.
 *
 * How it works: Bounded readers decode the versioned envelope and tables;
 * semantic passes verify identifiers, canonical ordering, port shapes,
 * connections, point bindings, capabilities, and limits; a stable Kahn sort
 * then builds the fixed schedule. Failures return protocol-stable reason codes
 * and source-correlatable paths. Success leaves a caller-owned, allocation-free
 * execution plan that requires no parsing or hardware discovery during a tick.
 */

#include "flow/sha256.h"

#include <stdio.h>
#include <string.h>

enum
{
    /* Frozen wire values and capability masks are named here because changing any of them changes cross-stack compatibility. */
    ENVELOPE_BYTES                  = 192,
    DIRECTORY_BYTES                 = 24,
    FLOW_SCHEMA                     = 1,
    FLOW_MANUAL_MODE                = 1,
    FLOW_DIGITAL_TYPE               = 2,
    FLOW_INPUT_DIRECTION            = 1,
    FLOW_OUTPUT_DIRECTION           = 2,
    FLOW_REQUIRED_BASE_CAPABILITIES = 0x13,
    FLOW_KNOWN_CAPABILITIES         = 0x1f,
    FLOW_MEMORY_CAPABILITY          = 0x04,
    FLOW_PROPOSED_OUTPUT_CAPABILITY = 0x08,
    FLOW_MAXIMUM_SNAPSHOT_BYTES     = 16384,
};

typedef struct
{
    /* Offset advances only through checked helpers, preventing malformed lengths from producing out-of-bounds reads. */
    const uint8_t *bytes;
    size_t size;
    size_t offset;
} reader_t;

/*
 * What: Creates a validation result from a stable reason code and optional artifact path.
 * Why: The backend and designer need machine-readable failures that still identify the relevant source-graph location.
 * How: It copies the already bounded path into zero-initialized result storage and leaves the path empty when none applies.
 */
static flow_result_t get_result(flow_reason_code_t code, const char *path)
{
    flow_result_t result = {.code = code};

    if (path != NULL)
    {
        snprintf(result.path, sizeof(result.path), "%s", path);
    }

    return result;
}

/*
 * What: Builds a validation result whose path joins a table prefix to a stable artifact identifier.
 * Why: Identifier-based paths remain meaningful even when canonical table positions change between graph revisions.
 * How: It copies only the bytes that fit the frozen path capacity and always appends a terminator.
 */
static flow_result_t get_identifier_result(flow_reason_code_t code, const char *prefix, const char *identifier)
{
    flow_result_t result         = {.code = code};
    const size_t prefix_size     = strlen(prefix);
    const size_t available       = sizeof(result.path) - prefix_size - 1U;
    const size_t identifier_size = strlen(identifier) < available ? strlen(identifier) : available;
    memcpy(result.path, prefix, prefix_size);
    memcpy(&result.path[prefix_size], identifier, identifier_size);
    result.path[prefix_size + identifier_size] = '\0';

    return result;
}

/*
 * What: Reads one byte from the current bounded-reader position.
 * Why: Every decoder operation must reject truncated artifacts before touching memory outside the declared table.
 * How: It checks offset against size, copies the byte, advances exactly once on success, and leaves the reader unchanged on
 * failure.
 */
static bool get_u8(reader_t *reader, uint8_t *value)
{
    if (reader->offset >= reader->size)
    {
        return false;
    }

    *value = reader->bytes[reader->offset++];
    return true;
}

/*
 * What: Decodes one little-endian 16-bit wire value from two checked bytes.
 * Why: Canonical artifacts must decode identically on hosts and controllers regardless of alignment or native byte order.
 * How: It delegates bounds checking to get_u8() and combines the bytes explicitly.
 */
static bool get_u16(reader_t *reader, uint16_t *value)
{
    uint8_t low;
    uint8_t high;

    if (!get_u8(reader, &low) || !get_u8(reader, &high))
    {
        return false;
    }

    *value = (uint16_t)low | (uint16_t)((uint16_t)high << 8U);
    return true;
}

/*
 * What: Decodes one little-endian 32-bit wire value from two checked 16-bit halves.
 * Why: Explicit decoding prevents platform layout from becoming part of the cross-stack artifact contract.
 * How: It reuses get_u16(), fails without advancing past unavailable bytes, and shifts the high half into place.
 */
static bool get_u32(reader_t *reader, uint32_t *value)
{
    uint16_t low;
    uint16_t high;

    if (!get_u16(reader, &low) || !get_u16(reader, &high))
    {
        return false;
    }

    *value = (uint32_t)low | ((uint32_t)high << 16U);
    return true;
}

/*
 * What: Validates one length-delimited identifier against the schema-1 ASCII grammar and capacity.
 * Why: Stable restricted IDs must round-trip through artifacts, diagnostics, snapshots, and UI correlation without ambiguous
 * encoding. How: It checks length, permits alphanumerics everywhere, and permits the documented punctuation only after the first
 * byte.
 */
static bool is_identifier(const uint8_t *bytes, size_t size)
{
    if (size == 0U || size > FLOW_EXECUTABLE_MAX_ID_BYTES)
    {
        return false;
    }

    for (size_t index = 0; index < size; index++)
    {
        const uint8_t value = bytes[index];
        const bool is_alphanumeric =
            (value >= 'A' && value <= 'Z') || (value >= 'a' && value <= 'z') || (value >= '0' && value <= '9');

        if (!is_alphanumeric && (index == 0U || (value != '.' && value != '_' && value != ':' && value != '-')))
        {
            return false;
        }
    }

    return true;
}

/*
 * What: Decodes one schema string8 identifier into a C string owned by the prepared executable.
 * Why: Runtime and diagnostic code need safe stable IDs without retaining pointers into untrusted artifact bytes.
 * How: It validates the length and grammar, copies the exact payload, appends a terminator, and advances the reader only on
 * success.
 */
static bool get_id(reader_t *reader, char destination[FLOW_EXECUTABLE_MAX_ID_BYTES + 1])
{
    uint8_t length;

    if (!get_u8(reader, &length) || reader->size - reader->offset < length ||
        !is_identifier(&reader->bytes[reader->offset], length))
    {
        return false;
    }

    memcpy(destination, &reader->bytes[reader->offset], length);
    destination[length] = '\0';
    reader->offset += length;

    return true;
}

/*
 * What: Decodes every node record into normalized execution configuration and counts proposed outputs.
 * Why: Runtime must receive a canonical, supported node set rather than interpreting versioned payload bytes during a tick.
 * How: It verifies the declared count and lexical order, then validates each kind's exact payload shape and frozen policy fields.
 */
static flow_result_t get_nodes(reader_t *reader, flow_executable_t *flow)
{
    uint16_t count;

    if (!get_u16(reader, &count) || count != flow->node_count)
    {
        return get_result(FLOW_REASON_LENGTH_MISMATCH, "/nodes");
    }

    /* Canonical ID order removes source-encoding order as an input to scheduling and diagnostics. */
    for (uint16_t index = 0; index < count; index++)
    {
        flow_node_t *node = &flow->nodes[index];
        uint8_t kind;
        uint16_t config_size;

        if (!get_id(reader, node->id))
        {
            return get_result(FLOW_REASON_INVALID_IDENTIFIER, "/nodes");
        }

        if (index > 0U && strcmp(flow->nodes[index - 1U].id, node->id) >= 0)
        {
            char path[FLOW_EXECUTABLE_MAX_PATH_BYTES + 1];
            snprintf(path, sizeof(path), "/nodes/%u", index);

            return get_result(FLOW_REASON_NON_CANONICAL_ORDER, path);
        }

        if (!get_u8(reader, &kind) || !get_u16(reader, &config_size) || reader->size - reader->offset < config_size)
        {
            return get_result(FLOW_REASON_MALFORMED, "/nodes");
        }

        if (kind < FLOW_NODE_DIGITAL_INPUT || kind > FLOW_NODE_PROPOSED_OUTPUT)
        {
            return get_result(FLOW_REASON_UNKNOWN_NODE_KIND, "/nodes");
        }

        node->kind                = (flow_node_kind_t)kind;
        const size_t config_start = reader->offset;

        /* Decode each kind's frozen payload shape here so runtime sees normalized fields rather than schema bytes. */
        if ((kind == FLOW_NODE_DIGITAL_INPUT && config_size == 2U) || (kind == FLOW_NODE_PROPOSED_OUTPUT && config_size == 8U))
        {
            get_u16(reader, &node->point_index);

            if (kind == FLOW_NODE_PROPOSED_OUTPUT)
            {
                uint8_t source;
                uint8_t priority;
                uint32_t expiry;
                get_u8(reader, &source);
                get_u8(reader, &priority);
                get_u32(reader, &expiry);

                if (source != 1U || priority != 8U || expiry != 0U)
                {
                    return get_result(FLOW_REASON_INVALID_CONFIGURATION, "/nodes");
                }

                flow->output_count++;
            }
        }

        else if ((kind == FLOW_NODE_DIGITAL_CONSTANT || kind == FLOW_NODE_MEMORY) && config_size == 1U)
        {
            uint8_t initial;
            get_u8(reader, &initial);

            if (initial > 1U)
            {
                return get_result(FLOW_REASON_INVALID_CONFIGURATION, "/nodes");
            }

            node->initial_value = initial != 0U;
        }

        else if ((kind == FLOW_NODE_NOT || kind == FLOW_NODE_AND || kind == FLOW_NODE_OR) && config_size == 0U)
        {
            /* These operators have no configuration in schema 1. */
        }

        else
        {
            return get_result(FLOW_REASON_INVALID_CONFIGURATION, "/nodes");
        }

        reader->offset = config_start + config_size;
    }

    return get_result(FLOW_REASON_OK, "");
}

/*
 * What: Decodes the canonical port table while preserving artifact indices used by connections.
 * Why: Port identity and shape support diagnostics, but runtime connection lookup requires unambiguous validated indices.
 * How: It checks node bounds, direction, type, arity, reserved bytes, and canonical node/direction/ID ordering.
 */
static flow_result_t get_ports(reader_t *reader, flow_executable_t *flow)
{
    uint16_t count;

    if (!get_u16(reader, &count) || count != flow->port_count)
    {
        return get_result(FLOW_REASON_LENGTH_MISMATCH, "/ports");
    }

    /* Preserve complete table indices because connections use indices, while canonical ordering keeps artifacts reproducible. */
    for (uint16_t index = 0; index < count; index++)
    {
        flow_port_t *port = &flow->ports[index];
        uint8_t arity;
        uint8_t reserved;

        if (!get_u16(reader, &port->node_index) || !get_id(reader, port->id) || !get_u8(reader, &port->direction) ||
            !get_u8(reader, &port->value_type) || !get_u8(reader, &arity) || !get_u8(reader, &reserved))
        {
            return get_result(FLOW_REASON_MALFORMED, "/ports");
        }

        if (port->node_index >= flow->node_count ||
            (port->direction != FLOW_INPUT_DIRECTION && port->direction != FLOW_OUTPUT_DIRECTION) || arity != 1U ||
            reserved != 0U)
        {
            return get_result(FLOW_REASON_INVALID_PORT_SHAPE, "/ports");
        }

        if (index > 0U)
        {
            const flow_port_t *previous = &flow->ports[index - 1U];

            if (previous->node_index > port->node_index ||
                (previous->node_index == port->node_index &&
                 (previous->direction > port->direction ||
                  (previous->direction == port->direction && strcmp(previous->id, port->id) >= 0))))
            {
                return get_result(FLOW_REASON_NON_CANONICAL_ORDER, "/ports");
            }
        }
    }

    return get_result(FLOW_REASON_OK, "");
}

/*
 * What: Decodes graph edges and validates that they connect one output port to one compatible input port.
 * Why: Prepared evaluation assumes index consistency, digital type safety, and a single driver for every input.
 * How: It bounds all indices, cross-checks port ownership and direction, compares types, and scans earlier edges for duplicates.
 */
static flow_result_t get_connections(reader_t *reader, flow_executable_t *flow)
{
    uint16_t count;

    if (!get_u16(reader, &count) || count != flow->connection_count)
    {
        return get_result(FLOW_REASON_LENGTH_MISMATCH, "/connections");
    }

    for (uint16_t index = 0; index < count; index++)
    {
        flow_connection_t *connection = &flow->connections[index];

        if (!get_u16(reader, &connection->source_node_index) || !get_u16(reader, &connection->source_port_index) ||
            !get_u16(reader, &connection->target_node_index) || !get_u16(reader, &connection->target_port_index))
        {
            return get_result(FLOW_REASON_MALFORMED, "/connections");
        }

        if (connection->source_node_index >= flow->node_count || connection->target_node_index >= flow->node_count ||
            connection->source_port_index >= flow->port_count || connection->target_port_index >= flow->port_count)
        {
            return get_result(FLOW_REASON_MALFORMED, "/connections");
        }

        const flow_port_t *source = &flow->ports[connection->source_port_index];
        const flow_port_t *target = &flow->ports[connection->target_port_index];

        if (source->node_index != connection->source_node_index || target->node_index != connection->target_node_index ||
            source->direction != FLOW_OUTPUT_DIRECTION || target->direction != FLOW_INPUT_DIRECTION)
        {
            return get_result(FLOW_REASON_INVALID_PORT_SHAPE, "/connections");
        }

        if (source->value_type != target->value_type || source->value_type != FLOW_DIGITAL_TYPE)
        {
            char path[FLOW_EXECUTABLE_MAX_PATH_BYTES + 1];
            snprintf(path, sizeof(path), "/connections/%u", index);

            return get_result(FLOW_REASON_INCOMPATIBLE_TYPE, path);
        }

        /* Single-driver enforcement makes every runtime input lookup unambiguous and bounded. */
        for (uint16_t earlier = 0; earlier < index; earlier++)
        {
            if (flow->connections[earlier].target_port_index == connection->target_port_index)
            {
                return get_result(FLOW_REASON_DUPLICATE_DRIVER, "/connections");
            }
        }
    }

    return get_result(FLOW_REASON_OK, "");
}

/*
 * What: Decodes artifact point references and proves each one matches the selected controller target.
 * Why: A flow compiled for missing, differently directed, or differently typed hardware must fail before any physical sampling or
 * output. How: It validates the point policy and searches the bounded target description, retaining only normalized portable
 * point data.
 */
static flow_result_t get_points(reader_t *reader, flow_executable_t *flow, const flow_target_t *target)
{
    uint16_t count;

    if (!get_u16(reader, &count) || count != flow->point_count)
    {
        return get_result(FLOW_REASON_LENGTH_MISMATCH, "/points");
    }

    for (uint16_t index = 0; index < count; index++)
    {
        flow_point_t *point = &flow->points[index];
        uint8_t policy;
        uint8_t reserved;

        if (!get_id(reader, point->id) || !get_u8(reader, &point->direction) || !get_u8(reader, &point->value_type) ||
            !get_u8(reader, &policy) || !get_u8(reader, &reserved))
        {
            return get_result(FLOW_REASON_MALFORMED, "/points");
        }

        if (point->value_type != FLOW_DIGITAL_TYPE || policy != 1U || reserved != 0U)
        {
            return get_result(FLOW_REASON_INCOMPATIBLE_TYPE, "/points");
        }

        bool is_found = false;

        /* Resolve against the target now; ticks must not search hardware metadata or accept stale point bindings. */
        for (size_t target_index = 0; target_index < target->point_count; target_index++)
        {
            if (strcmp(target->points[target_index].id, point->id) == 0)
            {
                is_found = true;

                if (target->points[target_index].direction != point->direction ||
                    target->points[target_index].value_type != point->value_type)
                {
                    return get_result(FLOW_REASON_POINT_DIRECTION_MISMATCH, "/points");
                }

                break;
            }
        }

        if (!is_found)
        {
            return get_identifier_result(FLOW_REASON_MISSING_POINT, "/points/", point->id);
        }
    }

    return get_result(FLOW_REASON_OK, "");
}

/*
 * What: Validates each node kind's complete digital port shape and required input connectivity.
 * Why: The evaluator uses named inputs and cannot safely infer missing ports or values while executing an atomic tick.
 * How: It compares observed port counts with the schema table, verifies digital types, and proves every input has a decoded
 * driver.
 */
static flow_result_t is_shape_valid(const flow_executable_t *flow)
{
    /* Array positions are schema node kinds, making the supported port contract explicit and exhaustive. */
    static const uint8_t INPUT_COUNTS[]  = {0, 0, 0, 1, 2, 2, 1, 1};
    static const uint8_t OUTPUT_COUNTS[] = {0, 1, 1, 1, 1, 1, 1, 0};

    for (uint16_t node_index = 0; node_index < flow->node_count; node_index++)
    {
        const flow_node_t *node = &flow->nodes[node_index];
        uint8_t inputs          = 0;
        uint8_t outputs         = 0;

        for (uint16_t port_index = 0; port_index < flow->port_count; port_index++)
        {
            const flow_port_t *port = &flow->ports[port_index];

            if (port->node_index != node_index)
            {
                continue;
            }

            inputs += port->direction == FLOW_INPUT_DIRECTION ? 1U : 0U;
            outputs += port->direction == FLOW_OUTPUT_DIRECTION ? 1U : 0U;

            if (port->value_type != FLOW_DIGITAL_TYPE)
            {
                return get_result(FLOW_REASON_INCOMPATIBLE_TYPE, "/ports");
            }
        }

        if (inputs != INPUT_COUNTS[node->kind] || outputs != OUTPUT_COUNTS[node->kind])
        {
            return get_result(FLOW_REASON_INVALID_PORT_SHAPE, "/nodes");
        }

        for (uint16_t port_index = 0; port_index < flow->port_count; port_index++)
        {
            if (flow->ports[port_index].node_index == node_index && flow->ports[port_index].direction == FLOW_INPUT_DIRECTION)
            {
                bool is_driven = false;

                for (uint16_t connection_index = 0; connection_index < flow->connection_count; connection_index++)
                {
                    is_driven = is_driven || flow->connections[connection_index].target_port_index == port_index;
                }

                if (!is_driven)
                {
                    return get_result(FLOW_REASON_MISSING_CONNECTION, "/ports");
                }
            }
        }
    }

    return get_result(FLOW_REASON_OK, "");
}

/*
 * What: Builds the deterministic node schedule and rejects combinational cycles.
 * Why: Every tick needs a fixed dependency order independent of artifact record order, while explicit memory must permit feedback
 * across ticks. How: A bounded Kahn sort ignores edges entering memory, selects ready nodes by lexical stable ID, and reports the
 * first unscheduled node on a cycle.
 */
static flow_result_t get_schedule(flow_executable_t *flow)
{
    uint16_t degrees[FLOW_EXECUTABLE_MAX_NODES] = {0};
    bool selected[FLOW_EXECUTABLE_MAX_NODES]    = {false};

    /* Memory reads expose the previous tick, so edges into memory cannot form same-tick dependency cycles. */
    for (uint16_t index = 0; index < flow->connection_count; index++)
    {
        const flow_connection_t *connection = &flow->connections[index];

        if (flow->nodes[connection->target_node_index].kind != FLOW_NODE_MEMORY)
        {
            degrees[connection->target_node_index]++;
        }
    }

    for (uint16_t position = 0; position < flow->node_count; position++)
    {
        uint16_t candidate = flow->node_count;

        /* Lexical stable-ID tie breaking makes the schedule independent of otherwise valid table permutations. */
        for (uint16_t index = 0; index < flow->node_count; index++)
        {
            if (!selected[index] && degrees[index] == 0U &&
                (candidate == flow->node_count || strcmp(flow->nodes[index].id, flow->nodes[candidate].id) < 0))
            {
                candidate = index;
            }
        }

        if (candidate == flow->node_count)
        {
            for (uint16_t index = 0; index < flow->node_count; index++)
            {
                if (!selected[index])
                {
                    return get_identifier_result(FLOW_REASON_COMBINATIONAL_CYCLE, "/nodes/", flow->nodes[index].id);
                }
            }
        }

        selected[candidate]      = true;
        flow->schedule[position] = candidate;

        for (uint16_t edge = 0; edge < flow->connection_count; edge++)
        {
            const flow_connection_t *connection = &flow->connections[edge];

            if (connection->source_node_index == candidate && flow->nodes[connection->target_node_index].kind != FLOW_NODE_MEMORY)
            {
                degrees[connection->target_node_index]--;
            }
        }
    }

    return get_result(FLOW_REASON_OK, "");
}

/*
 * What: Converts one complete canonical artifact and target description into a prepared executable.
 * Why: This is the single trust boundary between transferred compiler output and deterministic controller evaluation.
 * How: It verifies envelope/body integrity and limits, decodes all tables, runs semantic and scheduling passes, and returns
 * stable failure detail.
 */
flow_result_t flow_executable_prepare(const uint8_t *artifact, size_t artifact_size, const flow_target_t *target,
                                      flow_executable_t *flow)
{
    static const uint8_t MAGIC[] = {'F', 'C', 'E', 'X'};

    if (artifact == NULL || target == NULL || flow == NULL || artifact_size < ENVELOPE_BYTES ||
        artifact_size > FLOW_EXECUTABLE_MAX_ARTIFACT_BYTES)
    {
        return get_result(FLOW_REASON_MALFORMED, "/artifact");
    }

    /* Clear the destination before parsing so every failure leaves no partially reusable prepared state. */
    *flow             = (flow_executable_t){0};
    reader_t envelope = {.bytes = artifact, .size = ENVELOPE_BYTES};
    envelope.offset   = 4U;
    uint16_t envelope_schema;
    uint16_t body_schema;
    uint16_t envelope_size;
    uint16_t flags;
    uint32_t declared_size;

    if (memcmp(artifact, MAGIC, sizeof(MAGIC)) != 0 || !get_u16(&envelope, &envelope_schema) ||
        !get_u16(&envelope, &body_schema) || !get_u16(&envelope, &envelope_size) || !get_u16(&envelope, &flags) ||
        !get_u32(&envelope, &declared_size))
    {
        return get_result(FLOW_REASON_MALFORMED, "/magic");
    }

    if (declared_size != artifact_size)
    {
        return get_result(FLOW_REASON_LENGTH_MISMATCH, "/artifactLength");
    }

    if (envelope_schema != FLOW_SCHEMA || body_schema != FLOW_SCHEMA)
    {
        return get_result(FLOW_REASON_UNSUPPORTED_SCHEMA, "/envelopeSchema");
    }

    if (envelope_size != ENVELOPE_BYTES || flags != 0U)
    {
        return get_result(FLOW_REASON_MALFORMED, "/envelopeLength");
    }

    uint32_t template_revision;
    uint8_t mode;
    uint8_t quality_policy;
    uint16_t reserved;
    uint32_t interval;
    uint32_t capabilities;
    uint32_t snapshot_bytes;
    get_u32(&envelope, &flow->revision);
    get_u32(&envelope, &template_revision);
    get_u8(&envelope, &mode);
    get_u8(&envelope, &quality_policy);
    get_u16(&envelope, &reserved);
    get_u32(&envelope, &interval);
    get_u16(&envelope, &flow->node_count);
    get_u16(&envelope, &flow->port_count);
    get_u16(&envelope, &flow->connection_count);
    get_u16(&envelope, &flow->point_count);
    get_u32(&envelope, &capabilities);
    get_u32(&envelope, &snapshot_bytes);

    if (flow->node_count == 0U || flow->node_count > FLOW_EXECUTABLE_MAX_NODES || flow->port_count == 0U ||
        flow->port_count > FLOW_EXECUTABLE_MAX_PORTS || flow->connection_count > FLOW_EXECUTABLE_MAX_CONNECTIONS ||
        flow->point_count > FLOW_EXECUTABLE_MAX_POINTS)
    {
        return get_result(FLOW_REASON_LIMIT_EXCEEDED, "/nodeCount");
    }

    if (mode != FLOW_MANUAL_MODE || interval != 0U)
    {
        return get_result(FLOW_REASON_UNSUPPORTED_MODE, "/executionMode");
    }

    if (quality_policy != 1U || reserved != 0U || flow->revision == 0U || template_revision == 0U)
    {
        return get_result(FLOW_REASON_MALFORMED, "/inputQualityPolicy");
    }

    if ((capabilities & ~FLOW_KNOWN_CAPABILITIES) != 0U ||
        (capabilities & FLOW_REQUIRED_BASE_CAPABILITIES) != FLOW_REQUIRED_BASE_CAPABILITIES ||
        (capabilities & ~target->supported_capabilities) != 0U)
    {
        return get_result(FLOW_REASON_UNSUPPORTED_CAPABILITY, "/requiredCapabilities");
    }

    if (snapshot_bytes == 0U || snapshot_bytes > FLOW_MAXIMUM_SNAPSHOT_BYTES || snapshot_bytes > target->maximum_snapshot_bytes)
    {
        return get_result(FLOW_REASON_SNAPSHOT_TOO_LARGE, "/maximumSnapshotBytes");
    }

    envelope.offset = 48U;

    if (!get_id(&envelope, flow->flow_id))
    {
        return get_result(FLOW_REASON_INVALID_IDENTIFIER, "/flowId");
    }

    uint8_t digest[32];
    flow_sha256(&artifact[ENVELOPE_BYTES], artifact_size - ENVELOPE_BYTES, digest);

    if (memcmp(digest, &artifact[160], sizeof(digest)) != 0)
    {
        return get_result(FLOW_REASON_DIGEST_MISMATCH, "/bodySha256");
    }

    reader_t directory = {.bytes = &artifact[ENVELOPE_BYTES], .size = artifact_size - ENVELOPE_BYTES};
    uint32_t body_size;
    uint32_t offsets[4];
    uint32_t directory_reserved;

    if (!get_u32(&directory, &body_size) || !get_u32(&directory, &offsets[0]) || !get_u32(&directory, &offsets[1]) ||
        !get_u32(&directory, &offsets[2]) || !get_u32(&directory, &offsets[3]) || !get_u32(&directory, &directory_reserved) ||
        body_size != directory.size || directory_reserved != 0U || offsets[0] < DIRECTORY_BYTES || offsets[0] >= offsets[1] ||
        offsets[1] >= offsets[2] || offsets[2] >= offsets[3] || offsets[3] >= body_size)
    {
        return get_result(FLOW_REASON_LENGTH_MISMATCH, "/bodyLength");
    }

    flow_result_t result;
    reader_t table = {.bytes = directory.bytes, .size = offsets[1], .offset = offsets[0]};

    if ((result = get_nodes(&table, flow)).code != FLOW_REASON_OK || table.offset != offsets[1])
    {
        return result.code == FLOW_REASON_OK ? get_result(FLOW_REASON_LENGTH_MISMATCH, "/nodes") : result;
    }

    table = (reader_t){.bytes = directory.bytes, .size = offsets[2], .offset = offsets[1]};

    if ((result = get_ports(&table, flow)).code != FLOW_REASON_OK || table.offset != offsets[2])
    {
        return result.code == FLOW_REASON_OK ? get_result(FLOW_REASON_LENGTH_MISMATCH, "/ports") : result;
    }

    table = (reader_t){.bytes = directory.bytes, .size = offsets[3], .offset = offsets[2]};

    if ((result = get_connections(&table, flow)).code != FLOW_REASON_OK || table.offset != offsets[3])
    {
        return result.code == FLOW_REASON_OK ? get_result(FLOW_REASON_LENGTH_MISMATCH, "/connections") : result;
    }

    table = (reader_t){.bytes = directory.bytes, .size = body_size, .offset = offsets[3]};

    if ((result = get_points(&table, flow, target)).code != FLOW_REASON_OK || table.offset != body_size)
    {
        return result.code == FLOW_REASON_OK ? get_result(FLOW_REASON_LENGTH_MISMATCH, "/points") : result;
    }

    if (flow->output_count > FLOW_EXECUTABLE_MAX_OUTPUTS)
    {
        return get_result(FLOW_REASON_LIMIT_EXCEEDED, "/nodes");
    }

    for (uint16_t index = 0; index < flow->node_count; index++)
    {
        const flow_node_t *node = &flow->nodes[index];

        if ((node->kind == FLOW_NODE_MEMORY && (capabilities & FLOW_MEMORY_CAPABILITY) == 0U) ||
            (node->kind == FLOW_NODE_PROPOSED_OUTPUT && (capabilities & FLOW_PROPOSED_OUTPUT_CAPABILITY) == 0U))
        {
            return get_result(FLOW_REASON_UNSUPPORTED_CAPABILITY, "/requiredCapabilities");
        }

        if ((node->kind == FLOW_NODE_DIGITAL_INPUT || node->kind == FLOW_NODE_PROPOSED_OUTPUT) &&
            (node->point_index >= flow->point_count ||
             flow->points[node->point_index].direction !=
                 (node->kind == FLOW_NODE_DIGITAL_INPUT ? FLOW_INPUT_DIRECTION : FLOW_OUTPUT_DIRECTION)))
        {
            return get_result(FLOW_REASON_POINT_DIRECTION_MISMATCH, "/nodes");
        }
    }

    if ((result = is_shape_valid(flow)).code != FLOW_REASON_OK)
    {
        return result;
    }

    return get_schedule(flow);
}
