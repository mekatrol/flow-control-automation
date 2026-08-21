#pragma once

/**
 * @file virtual_points.h
 * @brief Defines bounded instance-global virtual-point storage for controller flow programs.
 */

#include <stdbool.h>
#include <stddef.h>
#include <stdint.h>

/** Maximum UTF-8 bytes, including the terminator, in controller instance and deployment identities. */
enum
{
    FLOW_VIRTUAL_POINT_ID_CAPACITY      = 65,
    FLOW_VIRTUAL_POINT_CAPACITY         = 32,
    FLOW_VIRTUAL_POINT_COMMAND_CAPACITY = 16,
};

/** Identifies the typed value carried by one virtual point. */
typedef enum
{
    /** Boolean digital value. */
    FLOW_VIRTUAL_POINT_DIGITAL = 1,

    /** IEEE-754 binary64 analog value. */
    FLOW_VIRTUAL_POINT_ANALOG = 2,
} flow_virtual_point_type_t;

/** Identifies storage lifetime across controller restarts. */
typedef enum
{
    /** Value resets to its declared default or uninitialized quality. */
    FLOW_VIRTUAL_POINT_VOLATILE,

    /** Value participates in typed retained-image export and restore. */
    FLOW_VIRTUAL_POINT_RETAINED,
} flow_virtual_point_persistence_t;

/** Stable outcomes suitable for protocol diagnostic mapping. */
typedef enum
{
    /** Operation completed atomically. */
    FLOW_VIRTUAL_POINT_OK,

    /** Pointer, identity, type, capacity, or command content was invalid. */
    FLOW_VIRTUAL_POINT_INVALID_ARGUMENT,

    /** Point key is not allocated on this execution instance. */
    FLOW_VIRTUAL_POINT_NOT_FOUND,

    /** Contract differs from the existing instance-global contract. */
    FLOW_VIRTUAL_POINT_CONTRACT_CONFLICT,

    /** Another active deployment owns the writer lease. */
    FLOW_VIRTUAL_POINT_WRITER_CONFLICT,

    /** Caller identity does not match this controller instance. */
    FLOW_VIRTUAL_POINT_INSTANCE_MISMATCH,

    /** Retained image is malformed or incompatible with allocated contracts. */
    FLOW_VIRTUAL_POINT_RETAINED_INCOMPATIBLE,

    /** Fixed controller capacity is exhausted. */
    FLOW_VIRTUAL_POINT_STORAGE_FULL,
} flow_virtual_point_result_t;

/** Portable declaration resolved before a program is activated. */
typedef struct
{
    /** Stable non-empty key, terminated within FLOW_VIRTUAL_POINT_ID_CAPACITY. */
    char key[FLOW_VIRTUAL_POINT_ID_CAPACITY];

    /** Analog or digital value contract. */
    flow_virtual_point_type_t type;

    /** Volatile or retained lifecycle. */
    flow_virtual_point_persistence_t persistence;

    /** Whether this declaration requests the unique writer lease. */
    bool is_writer;

    /** Whether a typed initial value is present. */
    bool has_default;

    /** Digital initial value when type is FLOW_VIRTUAL_POINT_DIGITAL. */
    bool digital_default;

    /** Analog initial value when type is FLOW_VIRTUAL_POINT_ANALOG. */
    double analog_default;
} flow_virtual_point_declaration_t;

/** Immutable logical record returned in a scan snapshot. */
typedef struct
{
    /** Stable point key. */
    char key[FLOW_VIRTUAL_POINT_ID_CAPACITY];

    /** Declared value type. */
    flow_virtual_point_type_t type;

    /** False until a default, retained restore, or successful command initializes the cell. */
    bool is_initialized;

    /** Digital committed value. */
    bool digital_value;

    /** Analog committed value. */
    double analog_value;

    /** Monotonic timestamp supplied by the successful commit. */
    uint64_t timestamp_ms;

    /** Monotonic per-cell version, starting at zero before the first commit. */
    uint64_t version;
} flow_virtual_point_snapshot_t;

/** One proposed write in an all-or-nothing program scan transaction. */
typedef struct
{
    /** Allocated point key owned by the committing deployment. */
    char key[FLOW_VIRTUAL_POINT_ID_CAPACITY];

    /** Type that must exactly match the allocated contract. */
    flow_virtual_point_type_t type;

    /** Digital proposed value. */
    bool digital_value;

    /** Analog proposed value. */
    double analog_value;
} flow_virtual_point_command_t;

/** Private fixed cell storage exposed only for caller-owned allocation, never directly to a VM. */
typedef struct
{
    flow_virtual_point_declaration_t declaration;
    flow_virtual_point_snapshot_t value;
    char writer_deployment_id[FLOW_VIRTUAL_POINT_ID_CAPACITY];
    bool is_used;
} flow_virtual_point_cell_t;

/** Caller-owned instance-global store; one runtime task must serialize all API calls. */
typedef struct
{
    /** Stable concrete controller identity checked on every mutating operation. */
    char execution_instance_id[FLOW_VIRTUAL_POINT_ID_CAPACITY];

    /** Protocol contract version advertised during capability negotiation. */
    uint32_t protocol_version;

    /** Complete transaction generation incremented once per successful non-empty commit. */
    uint64_t generation;

    /** Fixed cell pool shared by every active program on this instance. */
    flow_virtual_point_cell_t cells[FLOW_VIRTUAL_POINT_CAPACITY];
} flow_virtual_point_store_t;

/**
 * Initializes an empty instance-global store.
 * @param store Non-NULL caller-owned storage with single-task access.
 * @param execution_instance_id Non-empty stable identity terminated within capacity.
 * @param protocol_version Non-zero virtual-point protocol contract version.
 * @return true on success; false leaves store cleared.
 */
bool flow_virtual_points_init(flow_virtual_point_store_t *store, const char *execution_instance_id, uint32_t protocol_version);

/**
 * Allocates compatible declarations and atomically acquires requested writer leases.
 * @param store Non-NULL initialized store.
 * @param execution_instance_id Identity that must equal the initialized concrete instance.
 * @param deployment_id Non-empty active deployment identity retained as writer owner.
 * @param declarations Array of declaration_count contracts; may be NULL only when count is zero.
 * @param declaration_count Number of declarations from zero through FLOW_VIRTUAL_POINT_CAPACITY.
 * @return Stable result; failure leaves every cell and lease unchanged.
 */
flow_virtual_point_result_t flow_virtual_points_activate(flow_virtual_point_store_t *store, const char *execution_instance_id,
                                                         const char *deployment_id,
                                                         const flow_virtual_point_declaration_t *declarations,
                                                         size_t declaration_count);

/**
 * Releases every writer lease owned by one deployment without deleting shared values.
 * @param store Non-NULL initialized store.
 * @param execution_instance_id Identity that must equal the initialized concrete instance.
 * @param deployment_id Non-empty deployment identity to release.
 * @return Stable result; mismatch leaves ownership unchanged.
 */
flow_virtual_point_result_t flow_virtual_points_deactivate(flow_virtual_point_store_t *store, const char *execution_instance_id,
                                                           const char *deployment_id);

/**
 * Copies a coherent immutable snapshot of requested keys.
 * @param store Non-NULL initialized store with serialized access.
 * @param keys Array of key_count non-NULL point keys.
 * @param key_count Number of keys from zero through FLOW_VIRTUAL_POINT_CAPACITY.
 * @param output Writable array with at least key_count elements.
 * @return Stable result; failure does not promise output contents.
 */
flow_virtual_point_result_t flow_virtual_points_snapshot(const flow_virtual_point_store_t *store, const char *const *keys,
                                                         size_t key_count, flow_virtual_point_snapshot_t *output);

/**
 * Validates and atomically commits one program's complete proposed output set.
 * @param store Non-NULL initialized store with serialized access.
 * @param execution_instance_id Identity that must equal the initialized concrete instance.
 * @param deployment_id Active deployment that must own every commanded point.
 * @param commands Array of command_count unique commands; may be NULL only when count is zero.
 * @param command_count Number of commands from zero through FLOW_VIRTUAL_POINT_COMMAND_CAPACITY.
 * @param timestamp_ms Monotonic commit timestamp applied to every command.
 * @return Stable result; any failure leaves every value and version unchanged.
 */
flow_virtual_point_result_t flow_virtual_points_commit(flow_virtual_point_store_t *store, const char *execution_instance_id,
                                                       const char *deployment_id, const flow_virtual_point_command_t *commands,
                                                       size_t command_count, uint64_t timestamp_ms);

/**
 * Exports retained initialized cells into a versioned typed image.
 * @param store Non-NULL initialized store with serialized access.
 * @param output Writable byte buffer, or NULL only when capacity is zero.
 * @param capacity Available output bytes.
 * @param size Non-NULL destination for exact encoded byte count.
 * @return Stable result; STORAGE_FULL reports insufficient output capacity.
 */
flow_virtual_point_result_t flow_virtual_points_export_retained(const flow_virtual_point_store_t *store, uint8_t *output,
                                                                size_t capacity, size_t *size);

/**
 * Restores a complete versioned typed image only into compatible retained cells.
 * @param store Non-NULL initialized store with serialized access.
 * @param image Non-NULL exact image bytes produced by export.
 * @param size Exact image size greater than zero.
 * @return Stable result; malformed or incompatible images leave every cell unchanged.
 */
flow_virtual_point_result_t flow_virtual_points_restore_retained(flow_virtual_point_store_t *store, const uint8_t *image,
                                                                 size_t size);
