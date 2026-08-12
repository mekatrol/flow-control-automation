# Portable Flow VM host ABI version 1

## Boundary

This is the normative language-neutral ABI implemented by
`controllers/shared/flow/vm.h` and `vm.c`. The C
header exposes fixed-width arguments and bounded caller-owned storage; it
will not expose compiler graph types or platform handles. ABI version 1 operates
on Flow IL v2 and uses no callbacks while native VM code is on the stack.

All functions return a `flow_vm_result_t` containing a stable numeric code,
bounded UTF-8 path length, and caller-provided path buffer. Functions never
throw, retain transient pointers, allocate, log, call hardware, or invoke host
callbacks. Null, misaligned, overlapping, undersized, or excessive spans are
rejected before access. `clear` is idempotent.

## Required operations

```c
uint32_t flow_vm_get_abi_version(void);
size_t flow_vm_get_instance_size(void);
flow_vm_result_t flow_vm_get_requirements(bytes artifact, requirements* out);
flow_vm_result_t flow_vm_prepare(bytes artifact, target target,
                                 bytes instance_storage, instance* out);
flow_vm_result_t flow_vm_initialize(instance, bytes retained_state);
flow_vm_result_t flow_vm_begin_tick(instance, input_frame, debug_mode);
flow_vm_result_t flow_vm_step_instruction(instance, execution_view* out);
flow_vm_result_t flow_vm_commit_tick(instance, command_buffer, snapshot_buffer);
flow_vm_result_t flow_vm_abort_tick(instance);
flow_vm_result_t flow_vm_reset(instance, reset_kind);
flow_vm_result_t flow_vm_export_retained_state(instance, bytes output);
flow_vm_result_t flow_vm_get_snapshot(instance, bytes output);
flow_vm_result_t flow_vm_clear(instance);
```

Here `bytes` is pointer plus `size_t`; every output adds capacity and written
length. `target` contains only version, capability, and capacity values plus a
logical point-contract table. `input_frame` is an immutable array of typed
values/quality captured at one monotonic instant. Commands and snapshots are
serialized into host-owned buffers.

## Lifecycle

The ABI operations expose the three phases of the PLC Scan Cycle from
`plc-scan-cycle.md`. `begin_tick` performs Read Inputs by capturing the supplied
immutable frame; instruction stepping performs Execute Logic; `commit_tick`
performs Write Outputs. Compatibility retains `tick` in ABI names, where one
tick always means one complete PLC scan.

Storage progresses `empty -> prepared -> initialized -> executing ->
initialized`; reset returns to initialized and clear returns to empty. Prepare
validates the complete artifact before constructing state. Begin captures the
frame into bounded private storage. Instruction stepping mutates only that
frame. Commit is legal only at the encoded commit instruction. Abort discards
the frame. No operation is concurrent for one instance; hosts provide external
serialization and cancellation by requesting abort at an operation boundary.

## Ownership and compatibility

The host allocates instance, paused-frame, command, retained-state, diagnostic,
and snapshot buffers according to requirements returned from validated metadata
and checked against target limits. The VM owns their contents only between
prepare and clear. Artifact and input spans need remain valid only for the call
unless requirements explicitly designate caller-owned lifetime storage.

The managed wrapper verifies `flow_vm_get_abi_version() == 1` before any other
call. Structure sizes, packing, Boolean representation, enum widths, and calling
convention are fixed in the public C header and verified by compile-time
assertions on every supported target. ABI additions use a new version or an
explicit size/version prefix; fields are never inserted into an existing layout.

## Failure and thread policy

Ordinary validation, capacity, lifecycle, adapter, and evaluation errors leave
the last committed state inspectable and never publish staged commands. A
process-corrupting native fault is outside the result-code contract and invokes
the host fail-fast/supervisor policy from `flow-il-security-boundaries.md`.
Different instances may execute on different threads when they share no buffers.
