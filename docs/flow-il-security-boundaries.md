# Flow IL and native VM security boundaries

## Assets and trust boundaries

Designer graphs, compiled artifacts, controller transfers, persisted artifacts,
point frames, retained state, and debugger commands are untrusted at the VM
boundary even when they originated inside this application. The backend
compiler is authoritative but is not a reason for native code to skip checks.
Physical output authority, credentials, device addresses, and process control
remain outside artifact contents.

The managed/native ABI, controller transport, persistence store, and host
adapters are separate trust boundaries. Each validates its own lengths,
identity, version, lifecycle state, and ownership rather than relying on an
earlier layer.

## Required containment

- Parse with checked offset and length arithmetic before retaining data.
- Reject unknown versions, flags, opcodes, types, non-canonical encodings,
  excessive counts, invalid operands, and unsupported requirements.
- Use fixed-capacity or caller-sized storage and perform no tick-time allocation.
- Validate all native pointers, lengths, alignments required by the ABI, enum
  ranges, and output capacities before access.
- Prepare replacement state privately and publish it only after complete
  validation and initialization; failure leaves the active runtime unchanged.
- Execute one tick atomically from a coherent input frame. Fault, cancellation,
  abort, or debugger stop discards staged state and proposed commands.
- Keep credentials, driver addresses, and direct hardware handles in host
  adapters. Point bindings contain stable logical identity only.
- Bound diagnostic strings, snapshots, debugger frames, work per tick, command
  batches, and publication queues. Diagnostics never include secrets or raw
  process addresses.
- Fuzz artifact framing and ABI calls, and test maximum capacities and every
  legal debugger stop point before production activation.

## Failure policy

Malformed or unsupported IL returns a stable error and cannot partially
activate. Expected native load, prepare, tick, or adapter failures are isolated
to the affected flow and relinquish that flow's command ownership according to
policy. A failed redeployment preserves the previous runtime.

Memory corruption, access violations, stack corruption, or an ABI contract
breach may make the process unsafe. The server must fail fast and rely on its
external supervisor; it must not catch such a fault and continue automation in
an unknown state. Controller watchdog/reboot handling follows the same safe-
output principle. Restart never silently adopts a new artifact or compiler
version.

## Out of scope for the VM

Authentication and authorization, network transport security, source
credential storage, command arbitration policy, physical interlocks, and
deployment approval are host responsibilities. The VM exposes bounded proposed
commands; it does not grant itself physical authority.
