# Portable Flow IL architecture

## Decision

Flow graphs are authored and persisted on the server, compiled once by the
server into a target-neutral intermediate language (Flow IL), and executed by
the same portable virtual machine on every target. The ASP.NET Core server is
the first production host. Hardware controllers load and validate Flow IL; they
do not parse designer graphs, discover graph topology, or run a graph compiler.

Flow IL v1 is the sole current production contract and encodes an
already scheduled instruction stream, typed storage layout, point bindings,
state layout, and resource manifest.

## Goals

- Run the flow designer and deployed flows with only `Server.Api`; no physical
  controller is required for authoring, compilation, execution, or debugging.
- Compile each immutable deployment snapshot once on the backend.
- Produce deterministic, byte-identical IL from identical resolved inputs.
- Decompile supported Flow IL into a valid, editable designer flow so an
  existing artifact can be inspected, recovered, modified, and recompiled.
- Use one normative VM implementation for server, host tests, and firmware.
- Provide the same debugger semantics when the VM is hosted by the server, a
  controller, or a server-side controller emulator.
- Keep target integration outside VM semantics through bounded adapters for
  points, clocks, scheduling, persistence, commands, and diagnostics.
- Allow constrained controllers to reject unsupported opcodes, types, limits,
  or execution profiles before activation.
- Preserve stable flow/node/point IDs for diagnostics and UI correlation.
- Use the strict PLC Scan Cycle—Read Inputs, Execute Logic, Write Outputs—as
  the identical execution model on the server, emulator, and every controller.

## Non-goals

- Flow IL is not .NET IL and is not loaded by the CLR. It is a small,
  versioned automation bytecode inspired by the separation between a managed
  compiler and runtime.
- Firmware generation is not required for the first release. A controller may
  interpret prepared IL directly. An optional target backend may later lower
  the same IL to native code or a firmware image without changing flow source.
- The browser does not compile IL, and targets do not accept designer JSON.
- Version fields remain explicit and unsupported versions are rejected. Before
  the first production release, format changes update all producers, consumers,
  and fixtures together and remove the superseded implementation.

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
        ^
        |  validate and decompile IL to a lossless designer graph
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

The backend decompiler is a tooling boundary, not part of the VM or a target.
It validates an artifact before translating its scheduled instructions, typed
storage, state, point bindings, symbols, and authoring metadata back into an
editable graph accepted by the current designer schema.

## Flow IL v1 contract

The v1 artifact is a canonical, little-endian envelope containing identity,
revision, execution policy, resource requirements, eight independently
versioned sections, and a digest for each section. The sections are typed
constants, point bindings, slot layout, scheduled instructions, commit plan,
symbols, debug map, and source dependencies. Exact encodings and semantic rules
are normative in [`flow-il-v1-contract.md`](flow-il-v1-contract.md).

Flow IL v1 has exactly these eight version-1 sections. The symbol section contains
bounded, digest-protected labels, group IDs, and finite canvas coordinates for
lossless recovery. It is the only accepted symbol-section format.

The compiler performs structural graph validation, connector and unit checking,
point/template resolution, combinational-cycle rejection, deterministic Kahn
ordering, slot allocation, constant normalization, and resource estimation.
The VM loader verifies framing, canonical encoding, supported requirements,
operand bounds and types, point compatibility, instruction ordering invariants,
and resource limits. It does not reconstruct a graph or choose an execution
order.

## Decompiler and designer round trip

The backend owns a versioned `IFlowDecompiler` alongside `IFlowCompiler`.
Decompilation is never performed by firmware or the portable VM. It must first
apply the same envelope, digest, bounds, capability, section, operand, and type
validation used for loading untrusted IL, then produce a current, valid designer
DTO without executing the artifact.

Decompilation is lossless for supported authoring data. Stable IDs, supported
node configuration, labels, groups, connector identities, and layout are
restored exactly.

Unknown required sections, unsupported opcodes or types, corrupt artifacts, and
constructs that cannot be represented by the current designer schema fail with
structured diagnostics. The decompiler must not silently drop instructions,
state, point bindings, or behavior. A recovered graph is required to pass
designer validation and recompilation. For canonical v1 artifacts,
`compile(decompile(artifact))` must reproduce the original artifact
byte-for-byte. Future artifacts produced with lossless metadata must reproduce
their canonical executable sections byte-for-byte; metadata/envelope
differences are allowed only where that later contract explicitly defines them.

## Runtime and host ABI

The normative runtime model is the
[`PLC Scan Cycle`](plc-scan-cycle.md). Each host triggers a scan, captures a
frozen input/current-state image, executes scheduled instructions into private
working storage, and atomically publishes only at Write Outputs. The existing
word `tick` means one complete PLC scan in existing APIs.

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

One scan remains atomic: Read Inputs captures inputs and committed state;
Execute Logic evaluates instructions into private working storage and stages
state/commands; Write Outputs validates and publishes state, commands, and one
immutable snapshot together. Missing or bad required input never becomes a
default scalar. Hosts never overlap scans for one runtime.

## Portable debugger

Flow IL v1 defines debugger behavior as part of the VM contract rather than as
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

The v1 debugger supports:

- start/load, prepare, stop, restart, and detach;
- **Step tick**, which commits one complete scan;
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

### End-to-end debugger boundaries

The browser edits source and presents debugger state; it never resolves byte
offsets or executes IL. The backend validates revisions, compiles source,
resolves stable node breakpoints through the debug map, owns server/emulator
sessions, and translates controller wire snapshots into the common application
model. Controller sessions receive the same artifact through authenticated FCP,
prepare a separate volatile VM, sample coherent physical inputs, and publish
only committed typed snapshots. Durable deployment and volatile debugging use
separate VM instances and storage.

Session ID, flow revision, scan number, target identity, and artifact digest
form the correlation boundary. Snapshot transfer is latest-only, chunked,
bounded, and digest protected; consumers reject mixed sessions or scans.
Control-plane status remains small while snapshot bytes travel through the data
plane. Backend and browser retain the latest immutable snapshot rather than
queuing an unbounded history.

Shadow output is the controller default. Live output requires an authenticated
session and exact confirmation of every affected point. Commands are issued by
the dedicated debug arbitration owner only after scan commit and are
relinquished on pause, stop, replacement, expiry, fault, disconnect, or reboot.

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

## Verification gates

- Fixture generation is deterministic and checked without rewriting files.
- Portable C, .NET boundary, firmware, and browser suites consume shared fixtures.
- Differential tests execute identical IL/input/prior-state tuples on server and
  controller hosts and compare snapshots exactly.
- Decompiler fixtures prove deterministic valid designer output, lossless
  authoring recovery and compile/decompile/compile executable-section
  equivalence.
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
- The repository format, build, unit, E2E, controller host, source
  policy, firmware build, and commissioning gates remain required.

## Pre-release version rules

- Support only the current source, IL, protocol, and ABI versions. Reject every
  other version explicitly; do not migrate, translate, or fall back.
- Store source and compiled artifacts separately; source remains the authority
  for recompilation.
- Never send an artifact to a target before version/capability/limit negotiation.
- Do not claim cross-target equivalence until shared fixture snapshots match.
- Do not add a C# evaluator as a production fallback. If the native server VM
  cannot load, deployment fails visibly while the previous runtime remains.
- There are no deployed flows to preserve before the production milestone. A
  pre-release compiler/format change updates or removes all fixtures and
  consumers in the same change.
- After the production milestone, a compiler upgrade does not silently replace running artifacts. Adoption is
  explicit redeployment with a recorded compiler and IL version.
