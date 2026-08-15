# Flow simulator contract

Status: Accepted. This document records the normative simulator schemas,
diagnostics, limits, and endpoint behavior that complement the system design in
[`portable-flow-runtime-architecture.md`](portable-flow-runtime-architecture.md). Changes
to these contracts must be made deliberately across producers, consumers,
fixtures, and tests.

## 1. Current executable-language inventory

The designer registers 38 node kinds. The compiler accepts the complete
palette. Every accepted node produces a stable node-index/symbol entry and
debug-map instruction range. The portable C VM is the only implementation of
the listed opcode semantics.

| Designer kind | Flow IL lowering | State/commit behaviour | Principal coverage |
| --- | --- | --- | --- |
| `digitalInput`, `analogInput` | `READ_POINT` (1) | freezes the point input image for the scan | compiler golden/analog fixtures; VM scan tests |
| `digitalConstant` | `CONSTANT` (2) | stateless | compiler golden fixtures; VM loader/scan tests |
| `not` | `NOT` (3) | stateless | compiler golden fixtures; VM scan tests |
| `and` | `AND` (4) | stateless | compiler golden fixture; VM scan tests |
| `or` | `OR` (5) | stateless | memory-feedback fixture; VM scan tests |
| `nand` | `NAND` (9) | stateless | compiler opcode parameter test; expanded-Boolean decompiler fixture |
| `nor` | `NOR` (10) | stateless | compiler opcode parameter test; expanded-Boolean decompiler fixture |
| `xor` | `XOR` (11) | stateless | compiler opcode parameter test; expanded-Boolean decompiler fixture |
| `xnor` | `XNOR` (12) | stateless | compiler opcode parameter test; expanded-Boolean decompiler fixture |
| `numericConstant` | `NUMERIC_CONSTANT` (13) | stateless | numeric-language compiler/decompiler fixture |
| `add` | `ADD` (14) | stateless | numeric-language compiler/decompiler fixture |
| `comparator` | `COMPARE` (15), operator operand 1-6 | stateless | numeric-language compiler/decompiler fixture |
| `levelShifter` | `LEVEL_SHIFTER` (16) | stateless | numeric-language compiler/decompiler fixture |
| `qualityGood` | `QUALITY_GOOD` (17) | stateless quality projection | compiler validation and portable VM semantics |
| `onDelay` | `ON_DELAY` (18) plus state metadata | stages timer state; publishes only at `COMMIT` | compiler state metadata and portable VM transactional tests |
| `risingEdge` | `RISING_EDGE` (19) plus state metadata | stages previous-input state; publishes only at `COMMIT` | compiler state metadata and portable VM transactional tests |
| `average`, `calculator`, `split`, `override` | `COPY` (24) | stateless canonical single-value profiles | compiler and registry tests |
| `min`, `max` | `MIN` (20), `MAX` (21) | stateless; propagates worst input quality | compiler and portable VM tests |
| `clamp` | `CLAMP` (22) | stateless; finite ordered bounds | compiler and portable VM tests |
| `line` | `LEVEL_SHIFTER` (16) | stateless affine transform | compiler and portable VM tests |
| `if`, `selector` | `SELECT` (23) | stateless typed selection | compiler and portable VM tests |
| `sequence` | `AND` (4) | stateless ordered Boolean gate | compiler and portable VM tests |
| `delay`, `timer` | `ON_DELAY` (18) plus state metadata | staged timer state, atomic commit | timer transactional tests |
| `pulse` | `RISING_EDGE` (19) plus state metadata | one-scan pulse, atomic prior-input commit | event transactional tests |
| `schedule`, `calendar` | `CONSTANT` (2) | deterministic enabled-state source | compiler and registry tests |
| `memory` | `LOAD_STATE` (6), then `STAGE_STATE` (8) | reads current state and stages next state for `COMMIT` | memory golden fixture; debugger abort test |
| `digitalOutput`, `analogOutput` | `PROPOSE_OUTPUT` (7) | proposed command publishes only at `COMMIT` | compiler point fixtures; VM transactional tests |

All programs end in `COMMIT` (255). Debug records associate both the primary
instruction and any secondary state-staging instruction with the source node.
The cross-cutting tests are `FlowCompilerTests`, `FlowDecompilerTests`,
`FlowDebugServiceTests`, `LocalFlowDebuggerTests`,
`FlowEmulatorServiceTests`, and `controllers/tests/test_flow_vm.c`.

Canonical portable profiles cover the former 16 authoring-only kinds. All 38
registered kinds are advertised as executable.

## 2. Flow interface decision

The current flow source schema has one required, versioned `interface` object
with `inputs` and `outputs` arrays. Entries use stable IDs and the types
`boolean`, `number`, `string`, or `event`. Names are user-facing and unique
within their direction. Units and defaults are definition data; live values are
session data and are never persisted in a flow.

`flowInput` and `flowOutput` are canonical portable boundary nodes. Each stores
only an `interfaceId`; its label, units, connector direction, and connector type
are derived from the referenced entry. Interface terminals do not resolve to
automation points and interface outputs never enter the physical point-command
path. Point nodes remain the explicit integration boundary for virtual,
external, and physical I/O.

Deleting a referenced interface entry is rejected until the referencing nodes
are removed or rebound. Invalid and missing references remain visible to the
author and produce path- and node-addressed diagnostics.

## 3. Simulation and live-output boundary

A simulator session is always a server-owned shadow session in a namespace
separate from deployed runtimes and controller debug sessions. Starting,
replacing, stopping, expiring, disconnecting, or shutting down a simulator must
abort an in-progress scan, discard its staged state and outputs, and dispose its
VM. No simulator API may enable controller live output.

Controller debugging is a distinct workflow. Its existing `live-output`
operation requires an exact confirmation of every affected point and uses the
controller lease/priority/hold contract. Simulator terminology and UI must not
present shadow output as deployed, commanded, or physical output.

The backend compiles the submitted draft and returns the authoritative source
revision/digest. A later executable edit makes the session stale; a browser
hash may be used only to notice local edits early, never as correlation
authority.

## 4. Diagnostic and error contract

Simulator endpoints use one JSON error envelope:

```json
{
  "code": "simulator_session_not_found",
  "message": "The simulator session was not found.",
  "path": "/sessionId",
  "nodeId": null,
  "details": {}
}
```

`code` and `message` are required. `path`, `nodeId`, and `details` are optional
and omitted when irrelevant. Messages are presentation-safe but clients branch
only on codes. Compilation failures use the same envelope in a `diagnostics`
array so multiple graph errors can be reported together. No new endpoint may
return a bare string, empty not-found body, or a transport exception message.

Stable code families and initial codes are:

| Family | Codes |
| --- | --- |
| Compile/source | `compile_invalid_source`, `compile_unsupported_node`, `compile_capability_missing`, `compile_limit_exceeded` |
| Session | `simulator_session_not_found`, `simulator_session_conflict`, `simulator_session_expired`, `simulator_invalid_state`, `simulator_limit_exceeded` |
| Input | `simulator_input_missing`, `simulator_input_unknown`, `simulator_input_type_mismatch`, `simulator_input_invalid_value`, `simulator_input_quality_unsupported` |
| Correlation/capability | `simulator_stale_revision`, `simulator_capability_unsupported` |
| Runtime | `simulator_vm_fault`, `simulator_cancelled`, `simulator_unavailable` |

Existing compiler diagnostic codes remain stable inside the compile envelope;
the outer `compile_invalid_source` code does not rename them. HTTP mapping is:
400 malformed transport/value, 404 missing resource, 409 conflict/stale
revision, 410 expired session, 422 valid request with compile/expectation
diagnostics, 429 bounded-resource exhaustion, and 503 unavailable host.

## 5. Shared limits

These are the current server profile and apply before allocation or execution.
A target may advertise a smaller limit; the effective limit is the minimum.

| Resource | Limit |
| --- | ---: |
| Interface inputs | 64 |
| Interface outputs | 64 |
| Active simulator sessions per server | 32 |
| Active simulator sessions per flow | 1 |
| Simulator lease / maximum idle time | 15 minutes |
| Breakpoints per session | 32 |
| Output-history samples per session | 1,024 |
| Inspectable values/slots | 256 |
| Flow IL artifact | 16,384 bytes |
| VM work/instructions per scan | 256 |

The current implementation enforces these bounds before or during allocation
and execution. Controller-template node and connection limits continue to
apply. Limits are returned as capabilities where the UI needs to prevent
invalid work, but server validation remains authoritative.

## 6. Endpoint and lifecycle inventory

Debug endpoints are rooted at
`/api/flows/{flowId}/debug-sessions`: create, get, tick/node/instruction step,
run-to, replace breakpoints, inspect frame, restart, run, pause, explicitly
confirmed controller live output, stop, and status events. The service supports
server, controller-emulator, and controller hosts. It allows one registered
debug session per flow, replaces only on explicit request, disposes local VMs
on replacement/stop, and asks remote transports to stop. Controller sessions
have renewable dead-man leases; server-local sessions currently have no expiry.

Emulator endpoints are rooted at `/api/emulators`: create, get, set typed point
or flow-interface inputs, atomically apply inputs and step, advance virtual
time/optionally scan, inject a supported fault, reset persisted input defaults,
reset/power-cycle VM state, and delete. Instances
are held by a singleton service, dispose their VM on delete or service shutdown,
and cap output samples at 1,024. Instances share the 32-session server bound,
expire after 15 minutes of inactivity, and are disposed on deletion or server
shutdown. Inputs carry typed values, stable binding IDs,
interface identity, and quality. Output history distinguishes proposed and
committed simulator values and includes units, quality, last-change scan, and
interface identity.

Application-level simulator endpoints are rooted at
`/api/flows/{flowId}/simulator-sessions`. They provide the consistent envelope
from section 4 while composing the existing compiler, debugger, and emulator
services without changing the portable execution path. Lower-level debug and
emulator endpoints retain their established response contracts.

Debugger sessions expose the compiler schedule as execution order and paused
frames expose stable node-to-slot typed values. Canvas consumers may project
those values onto declared output connectors and connected inputs, but must
label committed snapshot values separately from uncommitted paused-frame
values. Before-node and after-node breakpoints remain distinct stable records;
their UI must communicate position with text as well as colour. Diagnostic
paths identify affected node IDs and are navigation targets, while raw slots
and instruction pointers remain advanced inspection details.
