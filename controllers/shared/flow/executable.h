#ifndef CONTROLLER_FLOW_EXECUTABLE_H
#define CONTROLLER_FLOW_EXECUTABLE_H

/*
 * Purpose: Define the portable schema-1 executable contract shared by artifact
 * preparation, tick evaluation, firmware adapters, and host contract tests.
 * The declarations describe validated nodes, ports, connections, target points,
 * stable diagnostics, capacity limits, and the prepared execution schedule.
 *
 * Why this contract exists: The backend compiler and controller must agree on
 * one deterministic execution model without introducing a second evaluator or
 * relying on platform-specific structure layout. Explicit bounds also prevent
 * untrusted artifacts from causing allocation or unbounded embedded work.
 *
 * How callers use it: A platform supplies a flow_target_t describing compatible
 * points and limits, then flow_executable_prepare() decodes canonical bytes into
 * caller-owned flow_executable_t storage. runtime.h consumes only that prepared
 * representation, so evaluation never parses wire data, resolves hardware, or
 * discovers a structural graph error while a tick is running.
 */

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

enum
{
    /* These capacities bound hostile artifacts and make preparation memory use predictable on embedded targets. */
    FLOW_EXECUTABLE_MAX_ARTIFACT_BYTES = 8192,
    FLOW_EXECUTABLE_MAX_NODES          = 128,
    FLOW_EXECUTABLE_MAX_PORTS          = 384,
    FLOW_EXECUTABLE_MAX_CONNECTIONS    = 384,
    FLOW_EXECUTABLE_MAX_POINTS         = 64,
    FLOW_EXECUTABLE_MAX_OUTPUTS        = 64,
    FLOW_EXECUTABLE_MAX_ID_BYTES       = 63,
    FLOW_EXECUTABLE_MAX_PATH_BYTES     = 63,
};

typedef enum
{
    /* Stable reason codes cross the controller/backend/UI boundary; their numeric values form part of the protocol contract. */
    FLOW_REASON_OK                       = 0,
    FLOW_REASON_MALFORMED                = 1,
    FLOW_REASON_UNSUPPORTED_SCHEMA       = 2,
    FLOW_REASON_LENGTH_MISMATCH          = 3,
    FLOW_REASON_DIGEST_MISMATCH          = 4,
    FLOW_REASON_LIMIT_EXCEEDED           = 5,
    FLOW_REASON_INVALID_IDENTIFIER       = 6,
    FLOW_REASON_NON_CANONICAL_ORDER      = 7,
    FLOW_REASON_UNKNOWN_NODE_KIND        = 8,
    FLOW_REASON_INVALID_CONFIGURATION    = 9,
    FLOW_REASON_INVALID_PORT_SHAPE       = 10,
    FLOW_REASON_MISSING_CONNECTION       = 11,
    FLOW_REASON_DUPLICATE_DRIVER         = 12,
    FLOW_REASON_INCOMPATIBLE_TYPE        = 13,
    FLOW_REASON_MISSING_POINT            = 14,
    FLOW_REASON_POINT_DIRECTION_MISMATCH = 15,
    FLOW_REASON_COMBINATIONAL_CYCLE      = 16,
    FLOW_REASON_UNSUPPORTED_MODE         = 17,
    FLOW_REASON_UNSUPPORTED_CAPABILITY   = 18,
    FLOW_REASON_SNAPSHOT_TOO_LARGE       = 19,
    FLOW_REASON_INPUT_QUALITY_REJECTED   = 20,
    FLOW_REASON_EVALUATION_FAILED        = 21,
} flow_reason_code_t;

typedef enum
{
    /* Schema-1 deliberately supports only deterministic digital primitives and explicit one-tick memory. */
    FLOW_NODE_DIGITAL_INPUT    = 1,
    FLOW_NODE_DIGITAL_CONSTANT = 2,
    FLOW_NODE_NOT              = 3,
    FLOW_NODE_AND              = 4,
    FLOW_NODE_OR               = 5,
    FLOW_NODE_MEMORY           = 6,
    FLOW_NODE_PROPOSED_OUTPUT  = 7,
} flow_node_kind_t;

typedef struct
{
    /* The path correlates a stable validation failure with the source graph without exposing parser internals. */
    flow_reason_code_t code;
    char path[FLOW_EXECUTABLE_MAX_PATH_BYTES + 1];
} flow_result_t;

typedef struct
{
    /* Target points are supplied by the platform adapter so portable validation remains independent of hardware drivers. */
    char id[FLOW_EXECUTABLE_MAX_ID_BYTES + 1];
    uint8_t direction;
    uint8_t value_type;
} flow_target_point_t;

typedef struct
{
    /* The target contract is resolved before decoding so point direction, capabilities, and limits cannot change mid-prepare. */
    const flow_target_point_t *points;
    size_t point_count;
    uint32_t supported_capabilities;
    uint32_t maximum_snapshot_bytes;
} flow_target_t;

typedef struct
{
    /* Nodes retain stable identity and only normalized execution configuration; schema byte layout is not exposed to runtime. */
    char id[FLOW_EXECUTABLE_MAX_ID_BYTES + 1];
    flow_node_kind_t kind;
    uint16_t point_index;
    bool initial_value;
} flow_node_t;

typedef struct
{
    uint16_t node_index;
    char id[FLOW_EXECUTABLE_MAX_ID_BYTES + 1];
    uint8_t direction;
    uint8_t value_type;
} flow_port_t;

typedef struct
{
    uint16_t source_node_index;
    uint16_t source_port_index;
    uint16_t target_node_index;
    uint16_t target_port_index;
} flow_connection_t;

typedef struct
{
    char id[FLOW_EXECUTABLE_MAX_ID_BYTES + 1];
    uint8_t direction;
    uint8_t value_type;
} flow_point_t;

typedef struct
{
    /* The schedule is canonical and independent of artifact record order, ensuring identical inputs produce identical ticks. */
    char flow_id[FLOW_EXECUTABLE_MAX_ID_BYTES + 1];
    uint32_t revision;
    uint16_t node_count;
    uint16_t port_count;
    uint16_t connection_count;
    uint16_t point_count;
    uint16_t output_count;
    flow_node_t nodes[FLOW_EXECUTABLE_MAX_NODES];
    flow_port_t ports[FLOW_EXECUTABLE_MAX_PORTS];
    flow_connection_t connections[FLOW_EXECUTABLE_MAX_CONNECTIONS];
    flow_point_t points[FLOW_EXECUTABLE_MAX_POINTS];
    uint16_t schedule[FLOW_EXECUTABLE_MAX_NODES];
} flow_executable_t;

/* What: Prepares one schema-1 executable. Why: Runtime may trust only canonical, compatible, acyclic artifacts. How: Decodes and validates against target limits, then builds a stable schedule in caller storage. */
flow_result_t flow_executable_prepare(const uint8_t *artifact, size_t artifact_size, const flow_target_t *target,
                                      flow_executable_t *executable);

#endif
