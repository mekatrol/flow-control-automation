# Controller flow debugging architecture reference

## Purpose

This document explains the completed controller-flow debugger: what each layer
owns, how requests and data cross the system, which implementation patterns are
used, and why its safety and determinism boundaries exist. It is intended to be
the reference used when adding or reviewing comments in controller, backend,
and frontend code.

The debugger compiles a supported designer graph on the backend, transfers the
canonical artifact into a volatile controller session, evaluates deterministic
ticks against coherent physical inputs, and returns immutable runtime snapshots
to the designer. Shadow output is the default. Live physical output is an
explicit, separately confirmed mode governed by arbitration, short expiry, and
cleanup on every terminal lifecycle path.

## Terminology

- **FCP (Flow Control Protocol):** the repository's bounded binary protocol for
  communicating with controllers over transports such as serial/RS485. It
  defines framing, addressing, operation codes, authentication, errors,
  capabilities, artifact transfer, and debug-session operations. The detailed
  wire contract is [`controllers/PROTOCOL.md`](../controllers/PROTOCOL.md).
- **Artifact:** the canonical compiled binary representation of a flow that the
  portable controller decoder validates and prepares for execution.
- **Debug session:** volatile controller-owned execution state identified by a
  session ID and protected by authenticated ownership and a renewable lease.
- **Tick:** one atomic evaluation of the complete prepared schedule using one
  coherent input image.
- **Snapshot:** the immutable, bounded observation published after a complete
  successful tick.
- **Shadow output:** an evaluated output value reported in a snapshot without
  commanding physical hardware.
- **Live output:** an explicitly enabled, short-lived arbitrated command applied
  to physical hardware by the dedicated debug owner.

This reference complements
[`controllers/IMPLEMENTATION_PLAN.md`](../controllers/IMPLEMENTATION_PLAN.md),
which remains the detailed authority for deterministic controller execution,
artifact validation, lifecycle integration, and production commissioning.

## Implemented system

- Portable controller code decodes, validates, schedules, and evaluates schema-1
  artifacts without allocation during a tick.
- A volatile controller debug service owns load, prepare, step, run, pause,
  snapshot, lease, stop, and live-output state independently of durable flows.
- Authenticated Flow Control Protocol (FCP) operations expose the debug service through bounded frames
  and immutable snapshot chunks.
- The backend compiles designer graphs, owns controller connectivity, assembles
  and validates wire snapshots, and exposes application HTTP/SSE contracts.
- The designer selects compatible controller targets, controls the session,
  overlays values by stable node ID, detects stale revisions, and requires an
  explicit named-output confirmation before live output is enabled.

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
At most one controller debug session is supported.

### Shadow mode is the safe default

Normal debugging samples real inputs without writing physical outputs. Output
nodes publish proposed commands in the debug snapshot. Live output is never
inferred from shadow execution: it must be explicitly enabled for one prepared
session by confirming the exact affected point list. Commands use a dedicated
owner, fixed priority, bounded expiry, arbitration-loss reporting, and cleanup
on pause, stop, replacement, lease expiry, fault, and reboot.

### A step is one complete tick

Manual step samples one coherent input image, evaluates the entire fixed
schedule, and atomically commits next memory state and the debug snapshot. It
does not step through individual nodes. Node-level breakpoints and partial-tick
state would violate the normal two-buffer execution model and are deferred.

### The backend owns device connectivity

The browser never speaks FCP directly. The backend discovers and authenticates
controllers, compiles artifacts, manages debug sessions, translates bounded
wire snapshots into application contracts, and streams updates to the UI.

## Supported scope

Executable artifact schema 1 supports:

- Digital input points
- Digital constants
- Boolean NOT, AND, and OR
- Explicit one-tick digital memory with an encoded initial value
- Digital output commands represented as proposed outputs in shadow mode
- Manual-step execution
- Stable flow, node, port, and point identifiers required for diagnostics and
  UI correlation

Fixed-interval run mode and deliberately confirmed live digital output are also
implemented. Analog values, timers, event-driven flows, user-defined functions,
multiple concurrent flows, persistent live state, and node-level breakpoints
remain outside the scope.

## Cross-stack contracts

### Executable artifact

The canonical binary artifact uses independently versioned envelope and body
schemas. The contract defines:

- Magic, schema versions, flow ID, revision, and controller-template revision
- Execution mode and interval fields
- Typed node records with stable IDs and bounded configuration
- Typed ports and connections
- Point references and required input-quality policy
- Memory initial values
- Output source, priority, and expiry policy fields
- Declared counts, lengths, capabilities, and limits
- Canonical ordering and encoding rules
- SHA-256 coverage

Versioned golden fixtures under `testdata/contracts/` contain source flow JSON,
compiled artifact bytes, decoded structure, expected validation result, input
frames, and expected tick snapshots. They include valid examples,
malformed artifacts, incompatible types, missing points, combinational cycles,
and encoding-order permutations.

### Debug runtime snapshot

The shared conceptual snapshot is represented in FCP, backend JSON, and
frontend types:

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
execution_high_water_us
missed_deadline_count
arbitration_loss_count
last_reason_code
last_reason_path
```

Snapshots are immutable and identify the tick that produced them. The
protocol must never return a mixture of nodes from different ticks. Every
collection and string is bounded. If a complete snapshot cannot fit one FCP
frame, use a snapshot header plus indexed chunks tied to the session ID and
tick number; do not silently truncate values.

### Debug lifecycle

The lifecycle is:

```text
empty -> loading -> ready -> stepping -> paused
                       |             |  |
                       +-> running <-+  +-> fault
any non-empty state -> stopped -> empty
```

Loading, validation, and preparation must not affect the durable generation.
A session automatically stops and releases its memory after an authenticated
stop, controller reboot, replacement by an authorised new session, or bounded
idle timeout. A transport disconnect does not need to abort immediately, but
the session must expire without periodic authenticated lease renewal.

## End-to-end architecture

The system deliberately follows a one-way dependency and trust flow:

```text
designer graph
    -> backend source adapter and compiler
    -> canonical executable artifact
    -> authenticated FCP transfer
    -> volatile controller debug service
    -> portable evaluator using coherent physical inputs
    -> immutable bounded snapshot
    -> backend wire decoder and application contracts
    -> designer runtime overlay
```

Commands travel down this chain and observations travel back up it. The browser
does not compile binary artifacts or communicate with field buses. The backend
does not reproduce controller evaluation semantics. The evaluator does not own
transport, persistent storage, or UI concerns. These boundaries are important
commenting landmarks: comments should explain the contract at a boundary, not
repeat the mechanics within the next layer.

### Identity and correlation

Flow ID and revision identify the graph version. Stable node, port, and point
IDs correlate compiler diagnostics, evaluator records, snapshots, and rendered
nodes. A volatile debug-session ID identifies controller ownership and prevents
responses from an earlier session being accepted for a replacement session.
Tick number identifies one atomic evaluation result. Snapshot header, chunks,
session ID, tick number, total length, and digest must all agree before data is
published to the application.

The frontend additionally calculates the current graph revision. A snapshot is
rendered only when its flow and revision still match the graph being displayed.
This prevents correct data from being shown on the wrong graph, which would be
more misleading than showing no data.

### Control-plane and data-plane separation

Load, prepare, step, run, pause, renew, stop, and live-output enablement form the
control plane. Snapshot publication forms the data plane. Continuous evaluation
never waits for a consumer to retrieve a snapshot: the controller retains the
latest complete snapshot, while the backend and UI poll or stream at a bounded
rate. This latest-only pattern prevents slow RS485 or browser consumers from
introducing evaluator backpressure.

## Controller architecture

### Portable executable pipeline

Code under `controllers/shared/flow/` is platform-independent so the exact
decoder and evaluator used by firmware can run in host tests. Its responsibilities
are intentionally split:

- `executable.c/.h` decode the artifact, validate bounds and semantics, resolve
  target points, and build the deterministic schedule.
- `runtime.c/.h` own prepared evaluator state, two-buffer memory semantics, one
  complete tick, and immutable node/proposed-output snapshots.
- `debug.c/.h` own the volatile session lifecycle, artifact transfer coverage,
  leases, manual and continuous scheduling, snapshot encoding/chunking, and
  optional live-output policy.
- `sha256.c/.h` provide the digest primitive shared by artifact and snapshot
  integrity checks.

The decoder performs structural and semantic validation before runtime state is
created. Declared lengths, counts, identifiers, canonical ordering, port shapes,
connections, types, target-point directions, capabilities, and combinational
cycles are rejected with stable reason codes and bounded paths. Validation is
front-loaded so a tick performs bounded work over already prepared structures.

### Deterministic evaluation

Schedule construction is deterministic and independent of source encoding
order. Combinational nodes evaluate in the prepared fixed schedule. Memory nodes
read from current state and write to next state; next state becomes current only
after the entire tick succeeds. The same commit boundary publishes the snapshot.
Consequently, an input-quality or evaluation failure cannot leave a partially
advanced memory image or a snapshot containing nodes from different ticks.

Manual Step means one full tick, not one node. Node-level stepping would expose
partial graph state and contradict the two-buffer model used during normal run
mode. Comments around evaluator loops should therefore describe atomicity,
schedule invariants, or commit timing rather than presenting the loop as a
sequence of independently visible node operations.

### Coherent physical input adapter

`controllers/shared/controller/runtime.c` adapts the controller I/O cache to a
portable `flow_input_frame_t`. All input samples carry one sampled timestamp and
quality image. The evaluator rejects a frame when coherence or required quality
is absent. It never performs field-bus reads while evaluating because blocking
I/O would make tick duration and cross-point coherence unpredictable.

### Volatile debug service and ownership

The debug service supports one bounded owner and one session. Artifact bytes,
coverage bits, prepared executable state, evaluator state, encoded snapshot, and
counters live in caller-owned fixed-capacity storage. No tick-time allocation or
unbounded queue is introduced.

The authenticated FCP session ID becomes the debug owner. Requests with a wrong
session are reported as not found, while requests from another authenticated
owner are forbidden. Replacement is explicit. `clear_session` preserves only
installed platform adapters and the monotonic next-session counter; it clears
all session-owned bytes and relinquishes live commands first.

The lease is a dead-man mechanism rather than a connection-state signal. A
temporary transport interruption does not immediately destroy useful debug
state, but loss of authenticated renewal guarantees cleanup within the fixed
deadline. Reboot provides a stronger boundary because all debug state is
volatile and is never recovered from durable flow storage.

### Manual and continuous scheduling

Manual Step samples a fresh coherent input frame, evaluates once, publishes the
complete snapshot, and enters paused state. Run uses monotonic deadlines and
never overlaps ticks. If supervisor work is late, missed deadlines are skipped
and counted instead of executing a burst of stale catch-up ticks. Pause freezes
evaluator memory and relinquishes physical debug commands; the next Step or Run
samples fresh inputs.

Snapshot encoding is rate-limited independently of evaluation. Execution
duration, high-water duration, missed deadlines, overruns, evaluation failures,
input validity, and arbitration losses make timing and safety degradation
observable without changing evaluation results.

### FCP protocol boundary

`controllers/shared/controller/protocol.c/.h` authenticate and validate wire
requests, delegate to the debug service, map internal results to stable protocol
errors, and encode bounded responses. Operations `0x50` through `0x5b` cover
load, prepare, status, step, snapshot transfer, renewal, stop, run, pause, and
live-output enablement. The capabilities response advertises these operation
bits so a client need not probe by mutation.

Large artifacts are uploaded in idempotent offset chunks with byte-coverage
tracking. Large snapshots use an immutable header and indexed chunks protected
by SHA-256. Retry safety comes from authenticated sequence handling, transaction
IDs, exact offsets, session identity, and immutable content—not from assuming a
transport timeout means an operation did not happen.

### Live-output safety and arbitration

Shadow mode records proposed outputs and never calls the output adapter. Live
mode is enabled only after the caller supplies the exact canonical list of
affected point IDs. `runtime.c` installs an adapter that submits commands to
`controller_points_t`; the portable debug service remains independent of the
hardware arbitration implementation.

Live commands use source owner `flow-debug`, priority 8, and a 1000 ms expiry.
Continuous ticks refresh this short lease. Manual Step uses forced-safe
semantics: it submits the evaluated command and immediately relinquishes it.
Pause, stop, replacement, debug lease expiry, input fault, evaluator fault,
output-command failure, and session clearing relinquish every affected point.
Only the debug owner's commands are removed, allowing other owners and the
baseline output image to resume through normal arbitration.

An accepted command may still lose to a higher-priority owner. The point
arbitrator exposes whether `flow-debug` is effective, and schema-3 snapshots
increment `arbitration_loss_count` when it is not. Loss is diagnostic rather
than an evaluator failure because arbitration is working as designed.

## Backend architecture

### Compiler boundary

`IFlowCompiler` separates graph compilation from persistence, HTTP, and device
transport. `FlowCompilationTargetResolver` first resolves the controller
template revision, point definitions, supported functions, capabilities, and
limits. `FlowCompiler` then validates the canonical source subset and emits the
schema-1 artifact and stable node correlation information.

Canonical ordering and explicit little-endian encoding make compilation
reproducible: identical resolved inputs produce byte-identical artifacts and
digests. Shared golden fixtures are consumed by .NET and portable C so compiler
and controller do not drift. The backend intentionally has no second evaluator;
doing so would create two authorities for memory and tick semantics.

### Transport boundary

`IControllerDebugTransport` describes controller operations without exposing
serial framing to orchestration code. `FcpControllerDebugTransport` translates
those operations into authenticated FCP exchanges, applies bounded retries to
transient I/O failures, preserves cancellation, validates response lengths and
identities, and reassembles snapshot chunks.

Retries are bounded and appropriate only where FCP identity and idempotency make
them safe. Snapshot assembly verifies session, tick, chunk index/count, absolute
offset, total coverage, and digest before returning an envelope. Malformed or
inconsistent controller data becomes a protocol-category gateway error rather
than partially populated application data.

### Session orchestration

`FlowDebugService` is the application-level coordinator. Start resolves and
compiles the source, loads the artifact, prepares the controller, validates the
returned identity, and records the one active session. If preparation fails
after load, it makes a best-effort stop before propagating the original error.

`FlowDebugSessionRegistry` stores the active application session and a semaphore
that serializes lifecycle mutations. This matches the controller's single-owner
model and prevents overlapping Start, Step, Run, Pause, Stop, or live-enable
operations from racing in-process. Read/inspect refreshes controller status and
retrieves a new snapshot only when the published tick changes.

The registry keeps the compiled affected-output list and live policy alongside
controller state. Live enablement requires the submitted list to match exactly
before FCP is called, then the controller validates it again against the
prepared artifact. This defence in depth makes both the application contract
and the device safety boundary explicit.

### Wire-to-application translation

`DebugSnapshotDecoder` is strict by design. It accepts known snapshot schemas,
enforces every bound and enum, validates UTF-8 and JSON-safe integer ranges,
rejects duplicate node/output IDs, and requires complete consumption of the
byte stream. Schema evolution is additive: schema 1 remains readable, schema 2
adds timing diagnostics, and schema 3 adds arbitration-loss reporting.

Service contracts in `Server.Services/Contracts` use application names and
types rather than leaking byte offsets or controller structs. Stable controller
reason codes and paths are preserved while also being mapped to readable names.
Errors remain categorized as compile, validation, authentication, transport,
protocol, timeout, stale session, or missing session so the API and UI can
present the right failure class.

### HTTP and event API

`FlowDebugEndpointRouteBuilderExtensions` exposes session commands as ordinary
request/response endpoints. The SSE endpoint repeatedly inspects the active
session, emits bounded status events, and indirectly renews the device lease.
Commands are not tunneled through the event stream; keeping mutation and
observation separate makes cancellation and error handling predictable.

The backend owns device credentials and connectivity. The browser receives no
FCP key, serial address, wire frame, or retry responsibility. Navigation cleanup
from the browser is useful but not trusted as the final safety mechanism; the
controller lease remains authoritative when a browser or network disappears.

## Frontend architecture

### Target filtering and source adaptation

`debugTargets.ts` filters controller templates to the digital point directions,
Boolean connector types, bound-point runtime feature, and flow functions needed
by schema 1. This prevents presenting a target that the compiler will inevitably
reject.

`flowDebugSource.ts` adapts the editable designer model into the compiler's
canonical application source. It emits exact node kinds, configuration names,
and port IDs rather than passing generic canvas connector shapes through to the
backend. Unsupported nodes and missing point bindings fail locally with a node
ID suitable for selection or highlighting.

### API parsing

`flowDebugApi.ts` defines the browser contracts and validates every unknown JSON
response before the view consumes it. Parsing is centralized so rendering code
can rely on lifecycle names, non-negative integer fields, typed Boolean values,
and array shapes. Network, cancellation, HTTP, and malformed-contract failures
are normalized into explicit UI errors.

### Designer session state

`AppFlowDesignerView.vue` coordinates API calls and owns transient UI state:
selected target, lifecycle, session ID, compiled revision, latest snapshot,
polling timer, abort controller, live-output policy, and error text. It prevents
overlapping Run polling, aborts obsolete Load work, and stops sessions during
target changes, navigation, refresh/unload, or explicit Stop.

The view compares snapshot identity and revision before updating the overlay.
Node values are indexed by stable node ID and converted to the existing designer
runtime presentation. Memory progression is visible tick by tick because the UI
never fabricates intermediate state.

### Controls, backpressure, and safety presentation

`AppFlowDebugPanel.vue` derives enabled controls from lifecycle state rather
than allowing invalid commands and relying on server rejection. Step and Run
are disabled for stale snapshots; Pause is available only while running; Stop
remains available for active and busy states. Polling is rate-limited and
non-overlapping so the UI cannot flood the backend or serial link.

Shadow output is labelled as non-physical. Live enablement displays the exact
affected point names and requires a fresh checkbox confirmation before emitting
the enable command. Once enabled, the panel prominently shows the physical mode,
priority, and expiry. Timing, input-quality, evaluation-failure, overrun,
missed-deadline, and arbitration-loss diagnostics remain visible alongside the
latest tick.

## Patterns and commenting guidance

Use comments to preserve the architectural reasons that are not obvious from C,
C#, or TypeScript syntax:

- Document bounds, ownership, atomic commit points, lifecycle preconditions,
  retry/idempotency assumptions, and cleanup guarantees.
- Explain why a callback or interface exists when it prevents a platform,
  transport, or UI dependency from entering portable/domain code.
- Explain why validation happens before mutation and why identity checks are
  repeated across trust boundaries.
- Describe what state is intentionally preserved or cleared during pause,
  replacement, stop, and fault handling.
- Call out safety behaviour where a future simplification could energise an
  output, extend command lifetime, bypass arbitration, or display stale data.
- For tests, state the invariant or failure mode being protected, especially
  cross-language fixtures, all-or-nothing ticks, bounded retries, lease cleanup,
  and explicit live-output confirmation.

Avoid comments that merely translate a function name or assignment into prose.
Prefer a contract comment above a function and focused inline comments at the
few points where ordering, bounds, ownership, or failure cleanup are essential.

### Suggested commenting pass

Work in the same dependency order used to understand the system:

1. **Controller:** begin with executable and runtime contracts, then the debug
   lifecycle, point arbitration, protocol dispatch, and platform adapters. Trace
   one tick and every cleanup path before commenting individual helpers.
2. **Backend:** begin with compiler and transport interfaces, then concrete
   encoding/decoding, session orchestration, registry serialization, and HTTP
   endpoints. Trace identity and cancellation from HTTP request to FCP response.
3. **Frontend:** begin with target/source adapters and API parsers, then view
   session state, polling and cleanup, panel control rules, live confirmation,
   and runtime overlay rendering.

For each file, identify its owner, inputs, outputs, persistent versus volatile
state, bounds, concurrency model, failure categories, and cleanup obligations.
Use those facts for file/function contract comments. Add inline comments only
where the code relies on a non-obvious ordering or invariant described above.

## Implementation history and phase traceability

### Phase 1: Freeze contracts and fixtures

Status: complete. The frozen contracts are
[`controller-executable-flow-contract-v1.md`](controller-executable-flow-contract-v1.md)
and [`controller-debug-contract-v1.md`](controller-debug-contract-v1.md). Shared
binary artifacts, decoded forms, input frames, expected snapshots, and stable
validation failures live under `testdata/contracts/flow-executable-v1/`; the
backend snapshot schema is `testdata/contracts/debug-snapshot.schema.v1.json`.

Verified baseline before the Windows handoff on 2026-08-07:

- Portable controller host tests: 16 passed.
- Backend tests: 145 passed, including 3 shared-fixture contract tests.
- Deterministic fixture regeneration, controller source policy, and whitespace
  checks passed.

### Historical Windows verification handoff

From a PowerShell prompt at the repository root, verify the generated fixtures
and .NET consumer:

```powershell
node .\tools\generate-flow-contract-fixtures.mjs --check
dotnet test .\backend\Server\Server.slnx -m:1 -nodeReuse:false
```

The controller build invokes a Bash source-policy gate. Run its checks from Git
Bash or WSL with a C compiler and CMake available:

```bash
controllers/scripts/check-source-policy.sh controllers
cmake -S controllers/tests -B build/controller-host-tests
cmake --build build/controller-host-tests
ctest --test-dir build/controller-host-tests --output-on-failure
```

These commands remain the reproducible cross-platform verification route. The
decoder/validator, deterministic schedule construction, evaluator, and snapshot
capture remain portable C under `controllers/shared/flow/` so host and firmware
exercise the same implementation.

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

Status: complete. Portable schema-1 decoding, SHA-256 verification, semantic
validation, target-point resolution, deterministic schedule construction, and
the manual tick runtime live under `controllers/shared/flow/`. The runtime uses
fixed-capacity current/next state and publishes a snapshot only after a whole
tick succeeds. Shared-fixture host tests cover the valid artifacts, every
frozen invalid result, two-button truth-table ticks, memory feedback, and
all-or-nothing input-quality failure.

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

Status: complete. The transport- and persistence-independent compiler
boundary, resolved compilation input, artifact result, node correlation map,
and structured diagnostic contracts are defined in `Server.Services`. The
canonical compiler input and deterministic lowering rules are frozen in
[`controller-flow-source-contract-v1.md`](controller-flow-source-contract-v1.md).
The schema-1 compiler now validates and canonically encodes the supported graph
and reproduces the shared valid artifacts byte for byte.

1. **Complete:** Introduce a compiler boundary separate from flow persistence
   and runtime transport.
2. **Complete:** Resolve controller template, point definitions, capabilities,
   limits, and revisions before compilation.
3. **Complete:** Validate the canonical source graph and lower supported nodes
   into the executable schema-1 artifact.
4. **Complete:** Produce a node-ID correlation map and deterministic artifact
   digest.
5. **Complete:** Run compiler output through golden fixture tests consumed by
   the portable controller decoder. Byte-exact fixture reproduction makes the
   .NET output identical to the bytes exercised by portable C tests.
6. **Complete:** Return structured compile errors with stable graph paths
   suitable for node highlighting.

Exit criteria:

- Compiling identical inputs produces byte-identical artifacts.
- Unsupported nodes and target limits fail before transport begins.
- Compiler artifacts pass the controller decoder and expected tick fixtures.

### Phase 4: Add volatile controller debug operations

Status: complete. A portable single-owner debug service implements bounded
artifact loading, preparation, manual stepping, immutable snapshot encoding
and chunking, authenticated ownership, renewal, stop, replacement, and expiry.
FCP operations `0x50`-`0x58` delegate to it, the firmware adapter samples the
coherent input cache, and no physical-output callback enters the debug path.

1. **Complete:** Add authenticated, capability-negotiated FCP operations for debug load,
   status, prepare, step, snapshot header/chunks, lease renewal, and stop.
2. **Complete:** Reuse bounded transfer primitives where practical, but keep debug state
   distinct from durable staging and committed storage.
3. **Complete:** Integrate one debug-session owner into the controller runtime supervisor.
4. **Complete:** Sample the existing coherent controller input bitmap for each manual step.
5. **Complete:** Capture output-node results as proposed outputs without calling physical
   output arbitration.
6. **Complete:** Keep heartbeat, terminal, networking, MQTT, normal FCP requests, and durable
   flow metadata responsive throughout debug operations.

Exit criteria:

- A command-line client can load a golden artifact, step it repeatedly, fetch
  coherent snapshots, and stop it.
- Reboot and timeout discard the session without modifying durable flow state.
- Malformed, unauthorised, stale-session, and wrong-tick requests are rejected.
- Physical outputs remain unchanged throughout shadow debugging.

### Phase 5: Add the backend controller gateway

Status: complete for the initial configured-controller profile. The gateway
has discovery/capability/connection boundaries, a serial frame adapter, an
authenticated FCP client, bounded retrying debug transport, strict snapshot
assembly and decoding, one-session orchestration, HTTP commands, and an SSE
status stream that also renews the controller lease.

1. **Complete:** Define controller discovery, connection, authentication, capability, and
   transport interfaces independent of serial implementation details.
2. **Complete:** Implement the FCP serial/RS485 adapter with bounded retries, cancellation,
   transaction IDs, authentication sequencing, and snapshot chunk assembly.
3. **Complete:** Add backend debug-session orchestration: compile, load, prepare, step,
   inspect, renew lease, and stop.
4. **Complete:** Replace synthetic runtime snapshots for controller-targeted debug sessions with real
   device snapshots while retaining a clear target abstraction for future host
   execution.
5. **Complete:** Expose HTTP commands and an SSE runtime stream. Commands remain
   request/response operations; snapshot delivery may be streamed.
6. **Complete:** Translate controller reason codes and paths without losing their stable
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

Status: complete. The designer filters configured controller templates against
the schema-1 digital debugging capability set, drives the manual session API,
renders typed shadow snapshots over stable node IDs, and cleans up sessions on
explicit Stop, navigation, refresh, and disconnect. Run uses a no-overlap
fixed-interval Step loop; Pause freezes evaluator state until the next fresh
input sample. The palette exposes the schema-1 digital input, constant, memory,
logic, and proposed-output nodes, and a tested adapter emits the compiler's
exact node configuration and port identifiers rather than the legacy generic
designer connector shape.

1. **Complete:** Add a debug target selector with host and compatible
   hardware-controller targets; initial discovery uses configured templates.
2. **Complete:** Add Load, Step, Run, Pause, and Stop controls, enabling only operations
   supported by the current lifecycle state. Implement Load, Step, and Stop
   first; Run and Pause follow fixed-interval execution.
3. **Complete:** Extend frontend runtime contracts with session ID, revision, tick number,
   quality, typed node values, proposed outputs, timing, and structured faults.
4. **Complete:** Highlight node values and errors using stable node IDs. Clearly label
   snapshots as shadow mode and proposed outputs as non-physical.
5. **Complete:** Detect stale snapshots and mismatched flow revisions instead of displaying
   them over a changed graph.
6. **Complete:** Ensure navigation, refresh, browser disconnect, and explicit Stop have clear
   session behaviour; the backend lease remains the final cleanup guarantee.

Exit criteria:

- A user can load a supported flow, press a physical input, step one tick, and
  see the corresponding node and proposed output values.
- Memory-node values advance exactly one tick at a time.
- Validation and runtime failures select or identify the responsible node.
- Stale or disconnected state is visually distinct from current state.

### Phase 7: Add continuous shadow execution

Status: complete. The controller now owns fixed-interval monotonic scheduling,
skips missed deadlines without overlapping ticks, freezes evaluator memory on
pause, and samples fresh coherent inputs when execution resumes. Authenticated
run/pause operations extend FCP through `0x5a`; latest-only immutable snapshot
publication decouples evaluation from RS485 transfer. The backend exposes
run/pause commands and change-driven inspection, while the designer rate-limits
polling and displays duration, high-water, missed-deadline, overrun, input
quality, and evaluation-failure diagnostics. Portable lifecycle stress coverage
exercises delayed supervisor work alongside status/lease traffic.

1. **Complete:** Add fixed-interval run and pause operations using the evaluator's monotonic
   schedule and no-overlap policy.
2. **Complete:** Publish change-driven or rate-limited snapshots so RS485 and the UI cannot
   be flooded by every tick.
3. **Complete:** Add execution duration, high-water mark, missed-deadline, overrun, input
   quality, and evaluation-failure displays.
4. **Complete:** Define whether pause freezes memory state while inputs continue changing;
   the recommended behaviour is that pause freezes evaluator state and the
   next step samples fresh inputs.
5. **Complete:** Stress test debugging while other controller services are busy.

Exit criteria:

- Run/pause/step transitions preserve deterministic tick semantics.
- Snapshot backpressure does not delay evaluation or other controller work.
- Lost UI connectivity expires the debug session within the documented bound.

### Phase 8: Consider live-output debugging

Status: complete. Live output uses the dedicated
`flow-debug` arbitration owner at priority 8. Continuous ticks refresh commands
with a 1000 ms expiry; manual Step applies the evaluated command and immediately
relinquishes it (forced-safe stepping). Enabling requires an authenticated
operation with the exact canonical output-point list. Pause, stop, lease expiry,
replacement, evaluator/input/output fault, and reboot relinquish debug-owned
commands. Arbitration loss is reported in schema-3 snapshots. The backend
requires the exact compiled output list and returns the bounded controller
policy; the designer names every point and requires explicit per-session
confirmation. The authenticated commissioning client and
[`LIVE_OUTPUT_COMMISSIONING.md`](../controllers/LIVE_OUTPUT_COMMISSIONING.md)
cover on-target fault injection and emergency recovery. A controller model must
record a passing commissioning run before operators use live output; shadow
mode remains the default.

Live output operation is a separate safety milestone and is not implied by
completion of shadow mode. Before enabling it:

1. **Complete:** Define a dedicated debug output owner, priority, expiry, and arbitration
   loss reporting.
2. **Complete:** Relinquish every debug-owned command on pause, stop, timeout, disconnect
   lease expiry, fault, replacement, and reboot.
3. **Complete:** Decide safe behaviour when stepping: short command expiry, explicit output
   hold, or forced safe state. Do not infer this from shadow semantics.
4. **Complete:** Require controller capability support and an explicit per-session UI
   confirmation that names the affected output points.
5. **Complete:** Add on-target fault-injection and emergency-recovery commissioning tests.

## Verification architecture

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

## Reference demonstrator

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

## Implemented invariants

The completed controller debugging capability maintains these invariants:

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
- Live output requires exact named-point confirmation, uses bounded arbitration
  commands, reports arbitration loss, and relinquishes its owner on every safe
  lifecycle exit.
