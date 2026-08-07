# Controller flow debugging implementation plan

## Purpose

This plan defines the cross-stack work required to compile a flow on the host,
load it into a volatile debug session on a hardware controller, execute it
safely, and inspect coherent runtime state in the graphical flow designer. It
coordinates the frontend, backend, controller protocol, portable evaluator,
and ESP-IDF integration.

The first deliverable is a narrow end-to-end debugging slice, not production
deployment. A user must be able to select a small supported flow, load it into
controller memory, single-step one deterministic tick using physical input
samples, and see node values and proposed outputs in the UI without energising
physical outputs.

This plan complements
[`controllers/IMPLEMENTATION_PLAN.md`](../controllers/IMPLEMENTATION_PLAN.md),
which remains the detailed authority for deterministic controller execution,
artifact validation, lifecycle integration, and production commissioning.

## Current state

- The frontend can edit flows and display per-node runtime states and optional
  values.
- The backend exposes deploy and runtime endpoints, but its runtime service
  currently creates synthetic running/stopped snapshots rather than executing
  flows or communicating with a controller.
- FCP can upload, validate, commit, activate, and inspect metadata for one
  opaque artifact. Artifact schema 1 has no executable body specification, and
  activation does not execute it.
- The controller has coherent point sampling and output arbitration, but no
  artifact decoder, evaluator, per-node snapshot, manual-step operation, or
  volatile debug-session lifecycle.

## Architectural decisions

### One execution contract

The artifact schema, validation rules, evaluation order, and tick semantics are
defined once. The controller's portable C decoder and evaluator are the
reference implementation and must run in host tests as well as firmware. The
backend compiler may be implemented in .NET, but it must be verified against
shared golden artifact fixtures and expected tick results. Do not create an
independent C# evaluator with subtly different semantics.

### Debugging is separate from deployment

A debug session is volatile and must not commit, replace, activate, deactivate,
or remove the durable production generation. It has its own session identifier,
artifact revision, lifecycle state, timeout, counters, and protocol operations.
At most one controller debug session is supported initially.

### Shadow mode is the safe default

The first hardware debugger samples real inputs but never writes physical
outputs. Output nodes publish proposed commands in the debug snapshot. Live
output debugging is deferred until ownership, arbitration, expiry, disconnect,
pause, and fault behaviour have explicit safety tests and a deliberate UI
confirmation.

### A step is one complete tick

Manual step samples one coherent input image, evaluates the entire fixed
schedule, and atomically commits next memory state and the debug snapshot. It
does not step through individual nodes. Node-level breakpoints and partial-tick
state would violate the normal two-buffer execution model and are deferred.

### The backend owns device connectivity

The browser never speaks FCP directly. The backend discovers and authenticates
controllers, compiles artifacts, manages debug sessions, translates bounded
wire snapshots into application contracts, and streams updates to the UI.

## Initial scope

The first executable artifact supports only:

- Digital input points
- Digital constants
- Boolean NOT, AND, and OR
- Explicit one-tick digital memory with an encoded initial value
- Digital output commands represented as proposed outputs in shadow mode
- Manual-step execution
- Stable flow, node, port, and point identifiers required for diagnostics and
  UI correlation

Fixed-interval run mode may follow single-step execution after the snapshot and
flow-control behaviour is proven. Analog values, timers, event-driven flows,
user-defined functions, multiple concurrent flows, persistent live state,
node-level breakpoints, and live physical outputs are outside the initial
scope.

## Cross-stack contracts

### Executable artifact

Specify a canonical binary artifact with independently versioned envelope and
body schemas. The contract must define:

- Magic, schema versions, flow ID, revision, and controller-template revision
- Execution mode and interval fields, even when manual step is initially the
  only accepted debug mode
- Typed node records with stable IDs and bounded configuration
- Typed ports and connections
- Point references and required input-quality policy
- Memory initial values
- Output source, priority, and expiry policy fields
- Declared counts, lengths, capabilities, and limits
- Canonical ordering and encoding rules
- SHA-256 coverage

Add versioned golden fixtures under `testdata/contracts/` containing source
flow JSON, compiled artifact bytes, decoded structure, expected validation
result, input frames, and expected tick snapshots. Include valid examples,
malformed artifacts, incompatible types, missing points, combinational cycles,
and encoding-order permutations.

### Debug runtime snapshot

Define a shared conceptual snapshot that can be represented in FCP, backend
JSON, and frontend types:

```text
debug_session_id
flow_id
revision
lifecycle_state
mode
tick_number
sampled_at_ms
completed_at_ms
execution_duration_us
input_validity
nodes[]: node_id, state, quality, typed_value
proposed_outputs[]: point_id, state, quality
overrun_count
evaluation_failure_count
last_reason_code
last_reason_path
```

Snapshots are immutable and identify the tick that produced them. The
protocol must never return a mixture of nodes from different ticks. Every
collection and string is bounded. If a complete snapshot cannot fit one FCP
frame, use a snapshot header plus indexed chunks tied to the session ID and
tick number; do not silently truncate values.

### Debug lifecycle

The initial lifecycle is:

```text
empty -> loading -> ready -> stepping -> paused
                       |          |
                       +-> fault <-+
any non-empty state -> stopped -> empty
```

Loading, validation, and preparation must not affect the durable generation.
A session automatically stops and releases its memory after an authenticated
stop, controller reboot, replacement by an authorised new session, or bounded
idle timeout. A transport disconnect does not need to abort immediately, but
the session must expire without periodic authenticated lease renewal.

## Planned work

### Phase 1: Freeze contracts and fixtures

1. Assign executable artifact envelope and body schema versions.
2. Specify the initial node encodings, type rules, memory semantics, limits,
   deterministic scheduling rules, and validation reason codes.
3. Specify the debug lifecycle, authentication policy, lease timeout, snapshot
   consistency rules, and FCP payload/chunk encoding.
4. Specify backend JSON contracts and how typed values are rendered in the UI.
5. Add golden fixtures and expected results under `testdata/contracts/` before
   implementing either compiler or evaluator.

Exit criteria:

- The same fixtures can be consumed by .NET tests and portable C tests.
- Every field has a bound and byte order.
- Stable node IDs round-trip from editable flow to runtime snapshot.
- Malformed, incompatible, and cyclic examples have stable expected errors.

### Phase 2: Implement portable decode, validation, and evaluation

1. Add flow artifact modules under `controllers/shared/flow/` for bounded
   decoding, semantic validation, schedule construction, and prepared runtime
   state.
2. Add a platform-independent tick evaluator with separate current and next
   value/state buffers.
3. Provide adapters for coherent input snapshots and proposed output capture.
4. Implement manual initialization, one-tick step, snapshot capture, fault
   counters, and reset/stop.
5. Test every node truth table, explicit memory feedback, deterministic order,
   unavailable input policy, all-or-nothing failure, and capacity limits.

Exit criteria:

- Golden valid artifacts decode and execute on the host.
- Golden invalid artifacts fail with the specified reason and path.
- Permuted encodings produce identical schedules and tick results.
- No allocation or unbounded work occurs during a tick.

### Phase 3: Implement the backend compiler

1. Introduce a compiler boundary separate from flow persistence and runtime
   transport.
2. Resolve controller template, point definitions, capabilities, and revisions
   before compilation.
3. Validate the editable graph and lower supported nodes into the canonical
   artifact.
4. Produce a node-ID correlation map and deterministic artifact digest.
5. Run compiler output through golden fixture tests and, in integration tests,
   through the portable controller decoder.
6. Return structured compile errors with stable graph paths suitable for node
   highlighting.

Exit criteria:

- Compiling identical inputs produces byte-identical artifacts.
- Unsupported nodes and target limits fail before transport begins.
- Compiler artifacts pass the controller decoder and expected tick fixtures.

### Phase 4: Add volatile controller debug operations

1. Add authenticated, capability-negotiated FCP operations for debug load,
   status, prepare, step, snapshot header/chunks, lease renewal, and stop.
2. Reuse bounded transfer primitives where practical, but keep debug state
   distinct from durable staging and committed storage.
3. Integrate one debug-session owner into the controller runtime supervisor.
4. Sample the existing coherent controller input bitmap for each manual step.
5. Capture output-node results as proposed outputs without calling physical
   output arbitration.
6. Keep heartbeat, terminal, networking, MQTT, normal FCP requests, and durable
   flow metadata responsive throughout debug operations.

Exit criteria:

- A command-line client can load a golden artifact, step it repeatedly, fetch
  coherent snapshots, and stop it.
- Reboot and timeout discard the session without modifying durable flow state.
- Malformed, unauthorised, stale-session, and wrong-tick requests are rejected.
- Physical outputs remain unchanged throughout shadow debugging.

### Phase 5: Add the backend controller gateway

1. Define controller discovery, connection, authentication, capability, and
   transport interfaces independent of serial implementation details.
2. Implement the FCP serial/RS485 adapter with bounded retries, cancellation,
   transaction IDs, authentication sequencing, and snapshot chunk assembly.
3. Add backend debug-session orchestration: compile, load, prepare, step,
   inspect, renew lease, and stop.
4. Replace synthetic runtime snapshots for controller-targeted flows with real
   device snapshots while retaining a clear target abstraction for future host
   execution.
5. Expose HTTP commands and an SSE or WebSocket runtime stream. Commands remain
   request/response operations; snapshot delivery may be streamed.
6. Translate controller reason codes and paths without losing their stable
   machine-readable values.

Suggested application endpoints:

```text
POST   /api/flows/{flowId}/debug-sessions
GET    /api/flows/{flowId}/debug-sessions/{sessionId}
POST   /api/flows/{flowId}/debug-sessions/{sessionId}/step
POST   /api/flows/{flowId}/debug-sessions/{sessionId}/stop
GET    /api/flows/{flowId}/debug-sessions/{sessionId}/events
```

Exit criteria:

- Backend integration tests exercise the lifecycle against a fake FCP
  transport and the portable evaluator fixtures.
- Cancellation and controller loss cannot leave an unbounded backend task.
- The backend distinguishes compile, transport, authentication, validation,
  evaluation, timeout, and stale-session failures.

### Phase 6: Integrate the flow designer

1. Add a debug target selector with host and compatible hardware-controller
   targets; controller discovery UI may initially use configured targets.
2. Add Load, Step, Run, Pause, and Stop controls, enabling only operations
   supported by the current lifecycle state. Implement Load, Step, and Stop
   first; Run and Pause follow fixed-interval execution.
3. Extend frontend runtime contracts with session ID, revision, tick number,
   quality, typed node values, proposed outputs, timing, and structured faults.
4. Highlight node values and errors using stable node IDs. Clearly label
   snapshots as shadow mode and proposed outputs as non-physical.
5. Detect stale snapshots and mismatched flow revisions instead of displaying
   them over a changed graph.
6. Ensure navigation, refresh, browser disconnect, and explicit Stop have clear
   session behaviour; the backend lease remains the final cleanup guarantee.

Exit criteria:

- A user can load a supported flow, press a physical input, step one tick, and
  see the corresponding node and proposed output values.
- Memory-node values advance exactly one tick at a time.
- Validation and runtime failures select or identify the responsible node.
- Stale or disconnected state is visually distinct from current state.

### Phase 7: Add continuous shadow execution

1. Add fixed-interval run and pause operations using the evaluator's monotonic
   schedule and no-overlap policy.
2. Publish change-driven or rate-limited snapshots so RS485 and the UI cannot
   be flooded by every tick.
3. Add execution duration, high-water mark, missed-deadline, overrun, input
   quality, and evaluation-failure displays.
4. Define whether pause freezes memory state while inputs continue changing;
   the recommended behaviour is that pause freezes evaluator state and the
   next step samples fresh inputs.
5. Stress test debugging while other controller services are busy.

Exit criteria:

- Run/pause/step transitions preserve deterministic tick semantics.
- Snapshot backpressure does not delay evaluation or other controller work.
- Lost UI connectivity expires the debug session within the documented bound.

### Phase 8: Consider live-output debugging

Live output operation is a separate safety milestone and is not implied by
completion of shadow mode. Before enabling it:

1. Define a dedicated debug output owner, priority, expiry, and arbitration
   loss reporting.
2. Relinquish every debug-owned command on pause, stop, timeout, disconnect
   lease expiry, fault, replacement, and reboot.
3. Decide safe behaviour when stepping: short command expiry, explicit output
   hold, or forced safe state. Do not infer this from shadow semantics.
4. Require controller capability support and an explicit per-session UI
   confirmation that names the affected output points.
5. Add on-target fault-injection and emergency-recovery commissioning tests.

## Verification strategy

Use a test pyramid with shared fixtures:

- Portable C tests cover decoder, validator, schedule, evaluator, lifecycle,
  and bounded snapshot encoding.
- .NET unit tests cover compiler determinism, target resolution, gateway
  orchestration, error mapping, and transport cancellation.
- Frontend tests cover lifecycle controls, snapshot parsing, revision checks,
  stale state, node highlighting, and fault presentation.
- Cross-language contract tests compile source flows in .NET and validate and
  execute the resulting bytes with the portable C tooling.
- Backend integration tests use a fake serial/FCP controller before hardware is
  required.
- On-target tests verify real input sampling, shadow outputs, timeout cleanup,
  reboot cleanup, protocol coexistence, and bounded performance.

Every phase must retain the existing controller host tests, backend tests,
frontend tests, source-policy checks, and KC868-A16 firmware build.

## Recommended first demonstrator

Use a two-button Boolean flow that matches available hardware:

```text
input-01 ----\
              AND ---- proposed output-01
input-08 ----/
```

Demonstrate these steps:

1. Open the flow in the designer and select the hardware controller target.
2. Compile and load it into a volatile shadow debug session.
3. Read both physical buttons as released and step; all values are false.
4. Press only input 1 and step; input 1 is true and the proposed output is
   false.
5. Press both buttons and step; both inputs and the proposed output are true.
6. Stop debugging and prove the durable flow metadata and physical outputs did
   not change.

This slice exercises stable IDs, compilation, artifact transfer, validation,
physical input sampling, deterministic execution, snapshots, backend mapping,
and UI display without introducing live-output risk.

## Completion criteria

The initial controller debugging capability is complete when:

- The backend deterministically compiles the supported graph subset.
- The identical artifact contract is verified by shared golden fixtures.
- A hardware controller can validate and execute the artifact in volatile
  shadow mode without changing durable deployment or physical outputs.
- Manual step produces one coherent, immutable, bounded tick snapshot.
- The UI displays current node values, proposed outputs, tick identity, and
  structured failures against the correct flow revision.
- Stop, timeout, replacement, disconnect lease expiry, and reboot safely remove
  debug state.
- Host, backend, frontend, protocol, and on-target tests cover success and
  failure paths.
