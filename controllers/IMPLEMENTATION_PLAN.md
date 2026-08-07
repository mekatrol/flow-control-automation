# Flow execution implementation plan

## Purpose

This plan tracks the unfinished work required to execute compiled flows on the
controller. Implemented communications and deployment capabilities are
documented in [`FEATURES.md`](FEATURES.md), the FCP wire contract is defined in
[`PROTOCOL.md`](PROTOCOL.md), and build and commissioning instructions are in
[`README.md`](README.md).

The existing flow service transfers, validates, commits, activates, and
recovers one schema-1 artifact, but activation does not yet execute it. The
artifact representation is now frozen by
[`../docs/controller-executable-flow-contract-v1.md`](../docs/controller-executable-flow-contract-v1.md)
and shared golden fixtures. The remaining work adds a deterministic, bounded
evaluator without moving editable graph or backend concerns onto the controller.

## Architectural rules

- The platform entry point calls the shared `controller_main`, which performs
  bounded initialization, starts the controller runtime, and returns.
- Every communications subsystem is independently supervised. A failed
  subsystem reports degraded status and retries without blocking unrelated
  controller work.
- Queues, retries, payloads, topic lengths, diagnostic messages, sessions,
  transfers, flow graphs, evaluator work, and retained state are bounded.
- Credentials are provisioned through the authenticated terminal, loaded from
  typed persistent settings, and excluded from logs and committed defaults.
- Shared services depend only on platform and board contracts. Framework,
  processor, operating-system, and board headers remain in adaptation layers.
- State transitions expose read-only health information for diagnostics and
  flow-runtime consumers.
- Flow execution is deterministic for a given artifact, input snapshot, prior
  state, and tick number. Node declaration or connection insertion order must
  not change the result.
- Evaluation uses immutable current-tick inputs and state, computes next values
  in bounded working storage, then commits state and output commands once at
  the tick boundary. Nodes must not observe another node's partially committed
  next-tick state.
- All code follows [`AGENTS.md`](AGENTS.md) and [`.clang-format`](.clang-format).

## Feedback, initialization, and cycle policy

Feedback is stateful, not an instantaneous recursive evaluation. A flow that
needs feedback must contain an explicit one-tick `memory` node (also presented
to users as a delay or feedback node) with a compiler-supplied initial value.
On activation or reset, the node emits that initial value. During a tick it
captures its input, and it emits the captured value on the following tick.

Executable body schema 1 always encodes the initial digital memory value as one
strict Boolean byte; omission is invalid. Future stateful data types must
define their own valid initial-value encoding and cannot rely on zeroed memory
as an implicit semantic default.

The compiler and controller validator remove stateful-node input dependencies
when constructing the same-tick dependency graph. The remaining combinational
graph must be acyclic and is evaluated in a deterministic topological order.
An unbroken combinational cycle is rejected with a stable node or connection
path and reason code. The controller must not select an arbitrary feedback
edge as a virtual start because that makes behavior depend on graph encoding
order and hides a missing state/default decision from the user.

For an OR latch expressed as `output = set OR memory(output)`, memory starts at
`false`; the first tick with `set=true` computes `true`, the tick boundary
stores it, and later ticks remain `true` after `set` returns to `false`. A reset
input or separate resettable-memory node is required if the latch must turn
off without deactivating the flow.

Activation starts a fresh runtime instance from artifact-defined initial state.
Deactivation relinquishes flow-owned output commands and discards volatile
runtime state. A reboot follows the durable active flag but also starts from
the artifact-defined state; persistence of live memory across reboot is out of
scope until its safety and versioning contract is designed. Input values are
sampled before the first tick. Missing, stale, or bad-quality required inputs
follow an explicit per-node policy from the artifact rather than being coerced
to `false`.

## Planned work

### Phase 1: Specify executable artifact schema

Status: complete as part of controller debugging Phase 1. The normative schema
is `docs/controller-executable-flow-contract-v1.md`; shared exact binary,
decoded, validation, input, and tick fixtures are in
`testdata/contracts/flow-executable-v1/`. Portable C and .NET tests consume the
same fixture set.

- Replace opaque schema 1 with a separately versioned deterministic body
  specification covering typed constants, nodes, ports, connections, initial
  state, input-quality policy, execution mode and interval, and output source,
  priority, and expiry policy.
- Define the first supported bounded node set: digital input, digital constant,
  Boolean logic, explicit memory, and commanded digital output. Define exact
  truth tables, type rules, and error behavior before adding timing or numeric
  nodes.
- Set compile-time and advertised controller limits for artifact bytes, node
  and connection counts, ports, state bytes, fan-in, topological depth, and
  work per tick. Use fixed-capacity storage with no allocation during a tick.
- Publish canonical encoding rules and golden valid/invalid artifact fixtures
  shared by backend/compiler and firmware tests. Increment the artifact schema
  when evaluator semantics change incompatibly.

### Phase 2: Decode and validate before activation

Status: next implementation step. Start with bounded decoding and semantic
validation against every shared fixture, then construct the deterministic
schedule. Do not couple this code to ESP-IDF or durable activation.

- Add a portable decoder that performs checked offset/length arithmetic and
  rejects unknown schema versions, node kinds, types, flags, or non-canonical
  encodings without retaining pointers into untrusted bytes.
- Validate unique stable node and port identifiers, connection endpoints,
  point references, capability and limit declarations, compatible types,
  exactly-one-driver rules, required inputs, and legal initial values.
- Build the same-tick dependency graph, treating memory outputs as tick-start
  sources and memory inputs as next-state sinks. Produce a deterministic
  topological schedule and reject every remaining combinational cycle. Use a
  bounded Kahn implementation for scheduling; optionally use a bounded
  strongly-connected-component pass only to improve cycle diagnostics.
- Return bounded stable field/node/connection paths and reason codes through
  existing validation diagnostics. Validation failure must leave the currently
  committed and running generation untouched.

### Phase 3: Implement the portable tick evaluator

- Introduce an evaluator independent of ESP-IDF, physical I/O, MQTT, FCP, and
  storage types. Its adapters provide coherent input snapshots, monotonic time,
  and batched flow-owned output commands.
- Allocate separate current and next buffers for values and memory state.
  Execute each tick as: sample inputs, read current memory, evaluate the fixed
  schedule, stage outputs and next memory, validate completion, then atomically
  commit next memory and the output batch.
- Make tick failure all-or-nothing. Do not publish partially evaluated outputs
  or state; retain bounded failure counters and apply the documented safe-output
  policy after repeated or fatal faults.
- Give flow commands a stable owner derived from the flow ID and revision and
  integrate with existing output priority arbitration. Deactivation, replacement,
  evaluator failure, and shutdown must relinquish only that owner's commands.
- Define overrun behavior explicitly: never overlap ticks, count and diagnose
  missed deadlines, and use a monotonic next-deadline schedule that does not
  create an unbounded catch-up loop.

### Phase 4: Integrate lifecycle and supervision

- On boot, decode and validate a durably active artifact, initialize state,
  sample inputs, and start execution only after required platform services are
  ready. Failure keeps communications and authenticated recovery available.
- Make activation transactional at runtime: prepare and initialize the new
  evaluator before switching from the old instance, then persist the active
  selection consistently with the existing deployment contract. Specify and
  test rollback when preparation, persistence, or first execution fails.
- Make deactivation stop future ticks, relinquish flow-owned commands, discard
  state, and persist the inactive flag. Committing a new inactive revision must
  not disturb a running revision unless the storage profile explicitly cannot
  retain both and rejects the operation first.
- Run evaluation under the controller supervisor with a bounded per-loop/time
  budget so flow work cannot starve heartbeat, terminal, network, MQTT, RS485,
  or FCP processing.

### Phase 5: Runtime observability and protocol reporting

- Extend flow runtime status beyond committed metadata with lifecycle state,
  running revision, initialized state, tick count, last successful tick time,
  next deadline, execution duration/high-water mark, overruns, input-quality
  failures, evaluation failures, and last bounded reason/path.
- Expose read-only status through FCP `get flow runtime`, controller health,
  diagnostics, and the terminal without exposing the artifact's mutable state
  as a write surface.
- Emit rate-limited diagnostics for activation, initialization, deactivation,
  invalid artifacts, combinational cycles, unavailable inputs, overruns,
  output arbitration loss, and safe shutdown.

### Phase 6: Verification and commissioning

- Add portable unit tests for decoding, every node truth table, deterministic
  scheduling, two-buffer tick behavior, and state initialization.
- Include an OR-latch fixture proving `false` initialization, set, feedback
  retention, reset/deactivation behavior, and restart-from-initial-state.
- Test self-loops and multi-node combinational cycles are rejected while cycles
  broken by one or more memory nodes are accepted. Permute node and connection
  encoding order and verify identical schedules and results.
- Test malformed and maximum-sized artifacts, bad types/defaults/references,
  missing or stale inputs, arithmetic bounds, tick overruns, repeated faults,
  arbitration conflicts, activation rollback, deactivation, revision changes,
  and reboot recovery.
- Add integration tests proving evaluation remains bounded while protocol,
  MQTT, terminal, and link supervisors are busy, followed by on-target tests of
  physical input sampling, output arbitration, active-flow reboot, and recovery
  from an invalid active artifact.
- Document the compiler contract, supported nodes and limits, initial-state and
  feedback semantics, commissioning procedure, and expected diagnostics.

## Deferred decisions and extensions

- Retaining live memory across reboot, warm activation, or artifact revision
  requires an explicit migration, integrity, wear, and safe-default design.
- Event-driven execution, sub-tick propagation, timers, numeric values,
  user-defined functions, multiple concurrent flows, and distributed feedback
  are separate schema/runtime extensions. They must preserve deterministic
  ordering and bounded execution.
- Automatically breaking a cycle by selecting a virtual feedback connection is
  deliberately not planned. It may only be introduced as an explicit compiler
  transformation visible to the user and encoded with an initial value.

## Completion requirements

- Production code and focused automated tests are included together.
- Every supported board builds from the command line and VS Code tasks.
- On-target steps and expected diagnostics are documented.
- Failure paths are tested, not only successful startup.
- Configuration has safe empty defaults and contains no committed secret.
- Resource ownership, shutdown, retry, queue, graph, state, and tick-work limits
  are explicit.
- Artifact/compiler and firmware semantics have shared golden fixtures.
- Completed work is moved to [`FEATURES.md`](FEATURES.md).
