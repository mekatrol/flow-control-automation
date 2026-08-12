# Portable flow IL architecture and implementation plan

## Decision

Flow graphs are authored and persisted on the server, compiled once by the
server into a target-neutral intermediate language (Flow IL), and executed by
the same portable virtual machine on every target. The ASP.NET Core server is
the first production host. Hardware controllers load and validate Flow IL; they
do not parse designer graphs, discover graph topology, or run a graph compiler.

The existing executable-flow schema 1 and portable C evaluator prove the core
approach, but schema 1 is a graph-shaped debug artifact: the controller still
validates ports and connections and constructs a Kahn schedule. It remains
frozen for compatibility. Flow IL v2 is the production contract and encodes an
already scheduled instruction stream, typed storage layout, point bindings,
state layout, and resource manifest.

## Goals

- Run the flow designer and deployed flows with only `Server.Api`; no physical
  controller is required for authoring, compilation, execution, or debugging.
- Compile each immutable deployment snapshot once on the backend.
- Produce deterministic, byte-identical IL from identical resolved inputs.
- Use one normative VM implementation for server, host tests, and firmware.
- Provide the same debugger semantics when the VM is hosted by the server, a
  controller, or a server-side controller emulator.
- Keep target integration outside VM semantics through bounded adapters for
  points, clocks, scheduling, persistence, commands, and diagnostics.
- Allow constrained controllers to reject unsupported opcodes, types, limits,
  or execution profiles before activation.
- Preserve stable flow/node/point IDs for diagnostics and UI correlation.

## Non-goals

- Flow IL is not .NET IL and is not loaded by the CLR. It is a small,
  versioned automation bytecode inspired by the separation between a managed
  compiler and runtime.
- Firmware generation is not required for the first release. A controller may
  interpret prepared IL directly. An optional target backend may later lower
  the same IL to native code or a firmware image without changing flow source.
- The browser does not compile IL, and targets do not accept designer JSON.
- Schema 1 is not rewritten in place. Incompatible semantics require a new IL
  version and explicit capability negotiation.

## Layered model

```text
Vue designer graph
        |
        v
server source adapter and dependency resolver
        |  immutable flow + point + template revisions
        v
server compiler
  validate types/units/capabilities
  detect cycles and run deterministic Kahn scheduling
  allocate typed slots and state
  emit canonical Flow IL + symbols + resource manifest
        |
        +--------------------------+
        |                          |
        v                          v
server VM host                 controller VM host
portable VM core              portable VM core
server point adapters         hardware point adapters
        |                          |
        v                          v
runtime snapshots             runtime snapshots
        +-------------> backend/API/SSE -> Vue overlay
```

Only the compiler understands editable graph topology. The VM understands an
ordered instruction stream and explicit state transitions. Target hosts own
lifecycle and I/O, but not opcode meaning.

## Flow IL v2 contract

The v2 artifact is a canonical, little-endian envelope containing independently
versioned sections. Every section has checked lengths, counts, reserved fields,
and a digest.

1. **Identity:** flow ID/revision, compiler contract version, requested
   execution profile, and source dependency revisions.
2. **Requirements:** opcode/type capability bitmap, memory and snapshot bounds,
   maximum work per tick, point counts, and minimum host ABI version.
3. **Typed constants:** canonical Boolean, integer, number, string, and future
   domain constants; schema evolution defines exact encodings.
4. **Point bindings:** stable point ID, direction, value type, units contract,
   quality policy, and command policy reference. No driver credentials or
   physical addresses are embedded.
5. **Slot layout:** typed immutable inputs, transient registers, output slots,
   and current/next state slots. Slots have fixed bounds and no tick-time
   allocation.
6. **Instructions:** compiler-ordered opcodes with typed slot operands and
   normalized configuration. Connections and ports are not runtime records.
7. **Commit plan:** explicit next-state and proposed-command records applied
   only after the entire tick succeeds.
8. **Symbols:** stable instruction-to-node mapping and bounded diagnostic paths.
   A production stripping profile may remove nonessential labels, never stable
   IDs needed for correlation.
9. **Debug map:** optional canonical mappings from instruction offsets and typed
   slots to source node, connector, and state identities. Debug-capable targets
   advertise bounds for debug-map bytes, breakpoints, paused-frame storage, and
   inspectable slots.

The compiler performs structural graph validation, connector and unit checking,
point/template resolution, combinational-cycle rejection, deterministic Kahn
ordering, slot allocation, constant normalization, and resource estimation.
The VM loader verifies framing, canonical encoding, supported requirements,
operand bounds and types, point compatibility, instruction ordering invariants,
and resource limits. It does not reconstruct a graph or choose an execution
order.

## Runtime and host ABI

Keep the normative VM in portable C under `controllers/shared/flow/`, but split
it from controller-specific lifecycle code. Build the same sources as:

- a native library loaded by `Server.Services` through a narrow managed wrapper;
- the existing portable host-test target; and
- a firmware library linked by each controller target.

The ABI uses caller-owned buffers and explicit result codes. It exposes prepare,
initialize, tick, reset, snapshot, and dispose/clear operations. Hosts provide:

- a coherent typed point-input frame;
- monotonic time and execution scheduling;
- batched proposed point commands and relinquish callbacks;
- retained-state loading/saving when a profile permits it; and
- bounded diagnostic and snapshot publication.

One tick remains atomic: capture inputs, evaluate instructions into private
working storage, stage state and commands, validate completion, then publish
state, commands, and one immutable snapshot together. Missing or bad required
input never becomes a default scalar. Hosts never overlap ticks for one runtime.

## Portable debugger

Flow IL v2 defines debugger behavior as part of the VM contract rather than as
a controller-specific feature. A debug session may select one of three hosts:

- **server:** the portable VM uses configured server point adapters;
- **controller:** the artifact and debug commands travel through the controller
  transport and use real controller inputs, with shadow outputs by default; or
- **emulator:** the portable VM uses a controller profile and simulated I/O on
  the server, with no physical device required.

The backend presents one application debugger API across these hosts. Host-only
fields such as a controller lease or emulator clock remain explicit extensions;
the compiler artifact, breakpoint identities, execution frame, and snapshot
semantics stay common.

### Commands and breakpoints

The v2 debugger supports:

- start/load, prepare, stop, restart, and detach;
- **Step tick**, which preserves schema-1 behavior and commits one complete tick;
- **Step instruction/node**, which executes the next scheduled instruction and
  pauses before commit;
- continue/run and asynchronous pause;
- unconditional breakpoints before or after a stable node/instruction;
- run to node/instruction and run to tick boundary as temporary breakpoints;
- inspection of inputs, typed slots, current state, staged next state, proposed
  outputs, instruction pointer, call/frame identity for future functions, and
  the last completed snapshot; and
- bounded conditional/data breakpoints as a later capability, negotiated
  separately so small controllers need not implement them.

Breakpoints are expressed by stable source node ID plus an optional compiler
instruction discriminator, never by a raw byte offset supplied by the browser.
The backend resolves them through the artifact debug map and rejects stale
flow revisions. When one source node lowers to several instructions, ordinary
node stepping stops after the node's final instruction; instruction stepping is
available for lower-level diagnosis.

### Paused execution and safety

Instruction-level debugging creates a bounded **debug execution frame**. At tick
start the VM captures one coherent input image and committed current state. The
frame then holds the instruction pointer, working slots, staged next state, and
proposed commands while paused. Inputs do not change underneath the frame.

Pausing inside a tick never publishes a normal runtime snapshot, advances
memory, or sends an output command. Continue resumes the same captured frame.
Abort, stop, lease expiry, replacement, debugger fault, or controller reboot
discards the entire uncommitted frame and relinquishes debug-owned outputs. Only
successful execution through the tick commit instruction atomically publishes
state, commands, and the immutable completed-tick snapshot.

Live-output debugging therefore remains tick-commit-only. A breakpoint cannot
leave hardware reflecting an intermediate node result. Controllers may support
only a bounded number of breakpoints and inspectable values, and must advertise
those limits before a session starts. A target that lacks instruction debugging
may still advertise tick-step debugging; the UI must show the difference rather
than silently emulate controller stepping on the backend.

## Server-side controller emulator

The emulator is a VM host and I/O model, not a separate flow evaluator. It loads
the same Flow IL and uses the same portable VM while applying a selected
controller template's opcode, type, memory, timing, and capacity limits.

Its device model provides:

- typed virtual inputs that can be set manually, scripted, replayed from a
  timestamped trace, or driven by deterministic test scenarios;
- captured output terminals showing proposed/effective value, quality,
  arbitration owner, priority, expiry, and write history without touching
  hardware;
- configurable input quality, communication loss, bounce, latency, stale data,
  output failure, reset, and power-cycle fault injection;
- a deterministic virtual monotonic clock with advance-to-next-event, alongside
  optional wall-clock mode;
- resettable retained/volatile memory and controller lifecycle state; and
- import/export of bounded scenario and expected-snapshot fixtures for CI.

The first emulator models the controller capability contract and point I/O, not
electrical waveforms, CPU instruction timing, network stacks, or vendor firmware.
Those may be added as explicit device-model extensions. A flow passing the
emulator proves IL/VM and modeled-I/O behavior, but does not replace on-target
commissioning for physical timing or output safety.

## Deployment model

Deployment resolves an immutable source snapshot, compiles it, validates the IL
against the selected execution target, prepares a replacement VM, and swaps it
into service only after preparation succeeds. A failed compile or prepare leaves
the prior deployment running.

The built-in `default` target means the server VM. Hardware templates advertise
the same vocabulary plus limits and supported Flow IL/ABI versions. A compiled
artifact may be reused only when all dependency revisions and target
requirements match. Artifact persistence stores compiler version, source digest,
IL digest, dependency revisions, and optional symbols separately from editable
flow JSON.

Server deployment is the baseline path behind the existing
`POST /api/flows/{flowId}/deploy` and runtime endpoint. Controller transfer and
debug sessions become alternate hosts for the same compilation result rather
than a separate compiler pipeline.

## Phased implementation

### Phase 0 - Freeze baseline and architecture decisions

Status: complete on 12 August 2026. The frozen hashes and resource measurements
are recorded in `schema-1-resource-baseline.md`; accepted decisions are in
`decisions/0001-portable-c-normative-flow-vm.md` through
`decisions/0004-flow-il-v2-scheduled-instructions.md`; native and artifact
threat boundaries are in `flow-il-security-boundaries.md`.

- Record schema-1 fixture hashes and keep all v1 decoder/compiler tests green.
- Add architecture decision records for portable-C VM reuse, server native
  loading, v1 compatibility, and v2 scheduled instruction format.
- Measure current v1 artifact, prepare, tick, and snapshot resource usage.
- Define threat boundaries for untrusted IL and native-library failure.

Exit: the v1 contract is unchanged and v2 design choices have reviewed ADRs.

### Phase 1 - Specify Flow IL v2 and host ABI

Status: next.

- Write the normative binary/semantic contract before implementation.
- Define initial Boolean opcodes equivalent to schema 1, typed slots, commit
  plan, stable diagnostics, feature negotiation, and all capacity limits.
- Specify debug maps, breakpoint identity, paused execution frames, tick/node/
  instruction stepping, run-to, inspection, abort, and commit safety semantics.
- Put Kahn ordering and slot allocation in a reference fixture generator.
- Add valid, invalid, source-order-permuted, and maximum-bound golden fixtures.

Exit: C and .NET can independently decode fixture metadata and agree on every
stable result; source permutations produce byte-identical IL.

### Phase 2 - Refactor the portable VM

- Separate schema-1 compatibility loading from the VM execution core.
- Add the v2 loader, typed slot storage, instruction evaluator, and host ABI.
- Add the bounded debugger ABI and resumable pre-commit execution frame without
  weakening normal atomic tick execution.
- Remove topology and Kahn scheduling from the v2 target preparation path.
- Retain fixed-capacity/no-tick-allocation behavior and atomic commits.
- Build static firmware and shared server-library variants from the same source.

Exit: the portable host suite executes v2 fixtures and proves v1 behavior did
not regress.

### Phase 3 - Make the server compiler authoritative

- Move editable-graph adaptation from the browser into a backend-owned adapter;
  browser validation remains advisory.
- Resolve flow, points, target, and revisions transactionally.
- Implement deterministic Kahn scheduling, typed slot allocation, resource
  calculation, v2 emission, symbols, and structured diagnostics.
- Keep `IFlowCompiler` free of persistence and transport, but support explicit
  source/IL versions rather than a debug-only schema.
- Cross-test every compiler artifact with the portable v2 loader and VM.

Exit: identical resolved deployment inputs compile byte-for-byte identically,
and the target performs no graph scheduling.

### Phase 4 - Add the production server VM host

- Wrap the portable library behind `IFlowVirtualMachine` and safe managed
  handles; validate all lengths and copy ownership at the native boundary.
- Replace the snapshot-only `FlowRuntimeService` with isolated per-flow runtime
  instances managed by a hosted service.
- Add coherent server point adapters, interval/manual scheduling, cancellation,
  bounded shutdown, all-or-nothing redeployment, and latest immutable snapshots.
- Contain native/VM errors to the affected flow and expose structured status.
- Host local debug sessions with breakpoints, node/instruction stepping, run-to,
  inspection, and safe discard of partial ticks.

Exit: a supported flow can compile, deploy, execute, update, stop, and restart
using only `Server.Api`, with no configured controller.

### Phase 5 - Add the controller emulator and unified debugger

- Define emulator instances from controller templates and explicit point maps.
- Add deterministic virtual time, manual/scripted/replayed inputs, captured
  outputs, quality and lifecycle fault injection, reset, and scenario fixtures.
- Introduce one backend debugger coordinator for server, emulator, and controller
  hosts with capability negotiation and common session/command/snapshot models.
- Add APIs for breakpoint replacement, continue, pause, step tick, step node,
  step instruction, run-to, frame inspection, restart, stop, and event streaming.
- Prove emulator and portable host produce identical snapshots for shared
  IL/input/time/state fixtures.

Exit: a user can select a hardware profile, emulate its I/O, and use a normal
breakpoint/step/run-to workflow entirely on the server.

### Phase 6 - Connect the existing UI to server execution and debugging

- Make the built-in server target the default Load/Run/Step target.
- Unify deployment and debug/runtime snapshot models where semantics match;
  retain controller lease/live-output fields only for remote hardware sessions.
- Stream or poll server snapshots through the backend and render them by stable
  node ID with revision checks.
- Add breakpoint markers, current-node highlighting, call/frame and typed-value
  inspection, Step tick, Step node, Step instruction, Continue, Pause, Run to,
  Restart, and Stop controls gated by target capabilities.
- Add an emulator panel for input manipulation, scripts/traces, virtual time,
  output capture, reset, and fault injection.
- Add a no-controller Playwright journey covering author, save, compile, deploy,
  break, step/run/run-to, inspect, pause, redeploy, and stop, plus an emulated
  controller I/O journey.

Exit: the complete flow UI execution loop works against the backend server alone.

### Phase 7 - Migrate controller debugging and deployment to v2

- Advertise supported IL envelope/body and VM ABI versions through FCP.
- Transfer the same artifact produced for the server when target capabilities
  permit it; validate requirements before mutation.
- Retain schema-1 debug loading during a documented compatibility window.
- Update durable activation to prepare v2 IL transactionally and execute it
  through the same VM core and controller adapters.
- Preserve shadow/live-output commissioning and lease safety behavior.
- Add bounded on-device breakpoints and paused frames where advertised. Tick-only
  devices remain valid targets and report that finer stepping is unsupported.

Exit: server and controller pass the same tick fixtures for the same IL and
inputs, and firmware contains no v2 graph scheduler/compiler.

### Phase 8 - Expand the language deliberately

- Add numeric, comparison, level-shifter, point, quality, timer, and event
  opcodes in small versioned slices.
- For every opcode define types, units, quality propagation, state bytes,
  determinism, overflow behavior, worst-case work, and snapshot representation.
- Extend compiler, VM, target capabilities, fixtures, and UI together.
- Add optional AOT/native target backends only after the interpreter contract is
  stable; generated firmware must pass the same semantic fixtures.

Exit: each advertised node kind has one compiler lowering and one normative VM
semantic implementation across all hosts.

## Verification gates

- Fixture generation is deterministic and checked without rewriting files.
- Portable C, .NET boundary, firmware, and browser suites consume shared fixtures.
- Differential tests execute identical IL/input/prior-state tuples on server and
  controller hosts and compare snapshots exactly.
- Debugger conformance tests stop at every legal instruction, resume to the same
  completed snapshot, and prove aborting at every stop point commits nothing.
- Emulator conformance tests replay identical scenarios across emulator and
  controller recordings, with documented exclusions for unmodeled hardware.
- Fuzzing covers artifact framing, operands, counts, symbols, and native ABI
  calls; malformed IL cannot cause out-of-bounds access or partial activation.
- Maximum-size and long-running tests prove bounded memory, work, scheduling,
  cancellation, and snapshot backpressure.
- Redeploy, shutdown, VM fault, input-quality fault, output failure, and device
  loss prove command relinquish and prior-runtime preservation.
- The existing repository format, build, unit, E2E, controller host, source
  policy, firmware build, and commissioning gates remain required.

## Migration rules

- Never reinterpret schema-1 bytes as v2 or change v1 reason-code meanings.
- Store source and compiled artifacts separately; source remains the authority
  for recompilation and migration.
- Never send an artifact to a target before version/capability/limit negotiation.
- Do not claim cross-target equivalence until shared fixture snapshots match.
- Do not add a C# evaluator as a production fallback. If the native server VM
  cannot load, deployment fails visibly while the previous runtime remains.
- A compiler upgrade does not silently replace running artifacts. Adoption is
  explicit redeployment with a recorded compiler and IL version.
