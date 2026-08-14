# Flow simulator contract and Phase 0 baseline

Status: Accepted for the pre-release simulator implementation. This document
records the contract decisions and repository baseline required by Phase 0 of
`flow-simulator-implementation-plan.md`. Later phases may replace current
implementation details, but must preserve the decisions in sections 2 through
5 or amend this record deliberately.

## 1. Current executable-language inventory

The designer registers 36 node kinds. The compiler currently accepts the 20
kinds below. Every accepted node produces a stable node-index/symbol entry and
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
| `memory` | `LOAD_STATE` (6), then `STAGE_STATE` (8) | reads current state and stages next state for `COMMIT` | memory golden fixture; debugger abort test |
| `digitalOutput`, `analogOutput` | `PROPOSE_OUTPUT` (7) | proposed command publishes only at `COMMIT` | compiler point fixtures; VM transactional tests |

All programs end in `COMMIT` (255). Debug records associate both the primary
instruction and any secondary state-staging instruction with the source node.
The cross-cutting tests are `FlowCompilerTests`, `FlowDecompilerTests`,
`FlowDebugServiceTests`, `LocalFlowDebuggerTests`,
`FlowEmulatorServiceTests`, and `controllers/tests/test_flow_vm.c`.

The 16 registered but non-executable kinds are `average`, `calculator`,
`calendar`, `clamp`, `delay`, `if`, `line`, `max`, `min`, `override`, `pulse`,
`schedule`, `selector`, `sequence`, `split`, and `timer`. They remain visible
authoring nodes and must not be advertised as simulatable until their complete
vertical slice passes.

## 2. Flow interface decision

The current flow source schema will gain one required, versioned `interface`
object with `inputs` and `outputs` arrays. Entries use stable IDs and the types
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

## 3. Scenario storage decision

Scenarios are separate resources, never embedded in flow definitions or Flow IL
artifacts. The current scenario schema has `schemaVersion`, stable scenario and
flow IDs, the backend-issued `flowRevision`, metadata, bounded ordered steps,
and bounded expectations. Inputs and outputs are addressed by stable interface
ID. Import and export use exactly this schema.

Replay uses virtual time and the ordinary compiler and portable VM. A revision
mismatch fails with `scenario_stale_revision`; it is never rebound by name.
Storage implementations must provide list, retrieve, create/update, delete,
import, and export while keeping simulator sessions volatile.

## 4. Simulation and live-output boundary

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

## 5. Diagnostic and error contract

New simulator endpoints use one JSON error envelope:

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
| Scenario | `scenario_invalid`, `scenario_not_found`, `scenario_stale_revision`, `scenario_limit_exceeded`, `scenario_expectation_failed`, `scenario_version_unsupported` |
| Correlation/capability | `simulator_stale_revision`, `simulator_capability_unsupported` |
| Runtime | `simulator_vm_fault`, `simulator_cancelled`, `simulator_unavailable` |

Existing compiler diagnostic codes remain stable inside the compile envelope;
the outer `compile_invalid_source` code does not rename them. HTTP mapping is:
400 malformed transport/value, 404 missing resource, 409 conflict/stale
revision, 410 expired session, 422 valid request with compile/expectation
diagnostics, 429 bounded-resource exhaustion, and 503 unavailable host.

## 6. Shared limits

These are the initial server profile and apply before allocation or execution.
A target may advertise a smaller limit; the effective limit is the minimum.

| Resource | Limit |
| --- | ---: |
| Interface inputs | 64 |
| Interface outputs | 64 |
| Active simulator sessions per server | 32 |
| Active simulator sessions per flow | 1 |
| Simulator lease / maximum idle time | 15 minutes |
| Scenario resources per flow | 100 |
| Steps per scenario | 1,000 |
| Expectations per scenario | 1,000 |
| Breakpoints per session | 32 |
| Output-history samples per session | 1,024 |
| Inspectable values/slots | 256 |
| Flow IL artifact | 16,384 bytes |
| VM work/instructions per scan | 256 |
| Scenario execution scans | 10,000 |
| Scenario execution wall time | 30 seconds |

The existing implementation already enforces the last five runtime-oriented
bounds except scenario scans/time, which do not exist yet. Controller template
node and connection limits continue to apply. Phase 1 must enforce session
count and lease cleanup; Phases 2 and 6 must enforce interface and scenario
limits respectively. Limits are returned as capabilities where the UI needs to
prevent invalid work, but server validation remains authoritative.

## 7. Existing endpoint and lifecycle inventory

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
reset/power-cycle VM state, export the in-memory input/output trace, and delete. Instances
are held by a singleton service, dispose their VM on delete or service shutdown,
and cap output samples at 1,024. They currently have no count limit, lease, or
automatic disconnect cleanup. Inputs carry typed values, stable binding IDs,
interface identity, and quality. Output history distinguishes proposed and
committed simulator values and includes units, quality, last-change scan, and
interface identity. The export is not the persisted scenario contract in section 3.

Current error responses are inconsistent across these endpoint groups (plain
`ErrorResponse`, compiler diagnostic arrays, and empty emulator 404s). Phase 1
must introduce application-level simulator endpoints using section 5 without
changing the portable execution path.
