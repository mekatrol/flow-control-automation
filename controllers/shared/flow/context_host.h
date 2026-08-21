#pragma once

/**
 * @file context_host.h
 * @brief Hosts a bounded set of Flow IL programs as one atomic controller execution context.
 */

#include "flow/host.h"

/** Controller capacity intentionally bounds concurrent program memory and scan work. */
enum
{
    FLOW_CONTEXT_MAX_PROGRAMS = 2,
};

/** One immutable program artifact in a context deployment generation. */
typedef struct
{
    /** Stable non-empty program identity terminated within FLOW_VM_MAX_ID_BYTES plus one. */
    char program_id[FLOW_VM_MAX_ID_BYTES + 1];

    /** Non-zero immutable program revision. */
    uint32_t revision;

    /** Non-NULL complete Flow IL bytes readable throughout the load call. */
    const uint8_t *artifact;

    /** Artifact byte count from one through FLOW_VM_MAX_ARTIFACT. */
    size_t artifact_size;
} flow_context_program_t;

/** Caller-owned context scheduler sharing one instance-global virtual-point store. */
typedef struct
{
    /** Program hosts scanned in stable deployment order. */
    flow_host_t programs[FLOW_CONTEXT_MAX_PROGRAMS];

    /** Number of active programs from zero through FLOW_CONTEXT_MAX_PROGRAMS. */
    size_t program_count;

    /** Shared concrete controller store, never owned by an individual program. */
    flow_virtual_point_store_t *virtual_points;

    /** Stable concrete instance identity. */
    char execution_instance_id[FLOW_VIRTUAL_POINT_ID_CAPACITY];

    /** Stable logical context deployment identity. */
    char deployment_id[FLOW_VIRTUAL_POINT_ID_CAPACITY];

    /** Physical I/O reader shared by each program scan. */
    flow_host_read_inputs_t read_inputs;

    /** Physical command publisher shared by each program scan. */
    flow_host_publish_commands_t publish_commands;

    /** Opaque physical adapter context; lifetime exceeds this host. */
    void *adapter_context;
} flow_context_host_t;

/**
 * Initializes an empty multi-program context host.
 * @param context Non-NULL caller-owned context with single-task access.
 * @param virtual_points Non-NULL instance-global store.
 * @param execution_instance_id Identity exactly matching virtual_points.
 * @param deployment_id Non-empty context deployment identity.
 * @param read_inputs Non-NULL coherent physical input adapter.
 * @param publish_commands Non-NULL physical output adapter.
 * @param adapter_context Opaque adapter context, which may be NULL when accepted by both callbacks.
 * @return true when all identities and callbacks are valid; false leaves no active programs.
 */
bool flow_context_host_init(flow_context_host_t *context, flow_virtual_point_store_t *virtual_points,
                            const char *execution_instance_id, const char *deployment_id, flow_host_read_inputs_t read_inputs,
                            flow_host_publish_commands_t publish_commands, void *adapter_context);

/**
 * Prepares every program before atomically switching the complete context generation.
 * @param context Non-NULL initialized context that is not concurrently scanning.
 * @param programs Array of program_count unique program artifacts.
 * @param program_count Number of programs from one through FLOW_CONTEXT_MAX_PROGRAMS.
 * @return true when all programs and writer contracts activate; false preserves the previous generation.
 */
bool flow_context_host_load(flow_context_host_t *context, const flow_context_program_t *programs, size_t program_count);

/**
 * Runs one deterministic context scan using one pre-scan virtual-point image for every program.
 * @param context Non-NULL active context with serialized runtime access.
 * @param now_ms Monotonic scan timestamp applied to committed virtual commands.
 * @param snapshots Writable array with capacity at least context program_count.
 * @return true when every program commits successfully; false stops at the first failed program.
 */
bool flow_context_host_scan(flow_context_host_t *context, uint64_t now_ms, flow_vm_snapshot_t *snapshots);

/**
 * Stops every program and releases its distinct writer identity.
 * @param context Non-NULL initialized context with serialized runtime access.
 */
void flow_context_host_stop(flow_context_host_t *context);
