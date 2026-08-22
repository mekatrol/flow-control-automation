# Portable flow runtime architecture

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
- Keep flow programs portable: one immutable flow revision may participate in
  multiple logical execution contexts and be deployed to many compatible server
  or controller execution instances.
- Provide instance-global, thread-safe virtual points so independently compiled
  programs on one execution instance can exchange committed values by key.

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

## Programs, contexts, instances, and shared virtual points

The architecture distinguishes four identities that must not be collapsed:

- A **flow program** is portable source plus declared point requirements. It is
  not owned by a controller type, controller installation, or server VM.
- A **logical execution context** is a portable application composition. It
  selects immutable flow revisions, merges their virtual-point declarations,
  and defines target-independent scheduling and configuration.
- An **execution instance** is one concrete VM host: the server VM or one
  installed controller. A controller template describes capabilities and limits
  but is neither an instance nor a runtime namespace.
- A **context deployment** materializes one logical context on one execution
  instance. It supplies instance-specific physical-point bindings and records
  the exact program, context, template, mapping, compiler, and artifact revisions.

```text
flow program A -----+
                    +--> logical execution context --+--> server instance
flow program B -----+                                +--> controller instance 1
                                                     +--> controller instance 2

on each instance:
  program VMs <--> host-owned shared virtual-point store
```

One logical context may therefore be deployed to multiple controller types and
the server when every target satisfies its declared requirements. Compilation
and physical binding may produce different artifacts for different target
instances, but stable program behavior and virtual-point keys remain portable.
The same flow revision may also participate in more than one logical context.

### Point declarations and binding

A flow program declares every virtual point it uses by stable key, analog or
digital type, units where applicable, read/write capability, persistence, and
optional relinquish default. Declarations allocate no memory during authoring.
When programs are composed, declarations with the same key are unified. Type,
units, persistence, default, or capability conflicts are rejected before
deployment.

Physical points are resolved by each context deployment because different
controller types and installations expose different hardware. Virtual points
require no physical mapping. Activating a deployment allocates or attaches its
declarations to the execution instance's global virtual-point namespace.

Runtime virtual-point identity is:

```text
(executionInstanceId, pointKey)
```

It is never keyed by flow ID, logical context ID, or controller-template ID.
Every program running on one execution instance and referencing a compatible
key observes the same shared value. The same key on another server/controller
instance is an independent value. If an instance permits deployments from more
than one logical context, equal compatible keys still share the instance-global
cell.

Multiple readers are allowed. The initial arbitration policy permits one active
program writer per virtual point per execution instance. Deployment preparation
checks writer ownership against every active program on the instance, not only
the programs in the context currently being deployed. Last-writer-wins behavior
is forbidden unless introduced later as an explicit, versioned arbitration
policy.

### Thread safety and atomic visibility

An execution instance may schedule multiple program VMs concurrently. API
readers, debuggers, persistence workers, and device tasks may access point state
at the same time. The host therefore owns synchronization; portable VM programs
never receive mutable references to shared cells.

At Read Inputs, the host captures an immutable, versioned snapshot of every
shared point needed by that program. The VM executes without holding a shared
store lock and stages all writes privately. At Write Outputs, the host acquires
the required synchronization, revalidates writer ownership, and commits the
program's complete virtual-point output set atomically. Other programs observe
either the state before that commit or the complete state after it, never a
torn analog value, mixed metadata, or a partially published output set.

The value, data quality, timestamp, writer identity, and monotonically
increasing version form one atomic logical record. Allocation, declaration
reconciliation, ownership changes, commit, reset, and retained-state restoration
are synchronized with the same store invariants. A thread-safe dictionary alone
is insufficient because ownership checks and multi-point commits span records.

The managed host uses a dedicated instance-scoped store with a reader/writer
lock, short critical section, or equivalent transactional mechanism. Controller
hosts use an RTOS mutex/critical section or a single-owner point-store task with
message passing. No host holds the shared-store lock while executing Flow IL,
performing network or flash I/O, publishing telemetry, or invoking callbacks.
All interacting locks have one documented acquisition order. Interrupt handlers
must not directly mutate multiword point records.

Committed retained values are persisted asynchronously with their instance ID,
point key, complete contract identity, and committed version. Restore is
synchronized, occurs before affected programs activate, and requires an exact
instance, type, units, persistence, and default match. Values are never coerced
or migrated between contracts. Volatile values reset with their execution
instance. Concurrency conformance tests must force scan, deployment, reset,
persistence, debugger, and API-read interleavings and prove freedom from torn
reads, partial commits, deadlocks, and cross-instance leakage.

## Flow IL v1 contract

The v1 artifact is a canonical, little-endian envelope containing identity,
revision, execution policy, resource requirements, eight independently
versioned sections, and a digest for each section. The sections are typed
constants, point bindings, slot layout, scheduled instructions, commit plan,
symbols, debug map, and source dependencies. Exact encodings and semantic rules
are normative in [Flow IL v1](../reference/flow-il-v1.md).

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
[PLC scan cycle](plc-scan-cycle.md). Each host triggers a scan, captures a
frozen input/current-state image, executes scheduled instructions into private
working storage, and atomically publishes only at Write Outputs. The existing
word `tick` means one complete PLC scan in existing APIs.

Keep the server runtime entirely in managed .NET. Controller firmware may keep
its portable C implementation under `controllers/shared/flow/`. The two hosts
consume the same Flow IL contract and shared conformance fixtures as:

- a managed C# VM hosted directly by `Server.Services`;
- the existing portable controller host-test target; and
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

- typed virtual inputs that can be set manually or scripted;
- captured output terminals showing proposed/effective value, quality,
  arbitration owner, priority, expiry, and write history without touching
  hardware;
- configurable input quality, communication loss, bounce, latency, stale data,
  output failure, reset, and power-cycle fault injection;
- a deterministic virtual monotonic clock with advance-to-next-event, alongside
  optional wall-clock mode;
- resettable retained/volatile memory and controller lifecycle state; and

The first emulator models the controller capability contract and point I/O, not
electrical waveforms, CPU instruction timing, network stacks, or vendor firmware.
Those may be added as explicit device-model extensions. A flow passing the
emulator proves IL/VM and modeled-I/O behavior, but does not replace on-target
commissioning for physical timing or output safety.

## Flow simulator application model

The flow simulator is the application-facing composition of the compiler,
portable debugger, and server-side emulator. It is not another execution host
or evaluator. The browser submits the current draft source, and the backend
validates and compiles it before creating a volatile, server-owned shadow
session. The returned source revision and artifact digest are authoritative for
all subsequent commands. Editing executable source makes the session stale and
requires recompilation.

Simulator sessions occupy a namespace separate from deployments and controller
debug sessions. They support typed inputs and quality, virtual time, scan/node/
instruction stepping, run and pause, breakpoints, frame inspection, reset and
power cycle, fault injection, and output history. Replacement, explicit stop,
idle expiry, disconnect, cancellation, VM
fault, and server shutdown discard any uncommitted execution frame and dispose
the VM. Simulator outputs are always shadow outputs: no simulator endpoint can
activate controller live output or issue a physical point command.

### Point boundaries

Flow Input and Flow Output are not valid node kinds. Analog/Digital Input and
Output nodes form the portable boundary for virtual, external, and hardware
I/O, including simulation and debugging.

### Tutorials

Tutorials are repository-owned, versioned content made from ordinary flow
fixtures, guidance, and optional challenge fixtures.
Every executable palette kind has verified tutorial coverage. Tutorial flows
use the same source parser, compiler, simulator API, and VM as user-authored
flows; tutorial-specific function semantics are forbidden.

### Bounds and lifecycle guarantees

Allocation and execution are bounded before work is accepted. The server
limits interface entries, active sessions, session idle time, breakpoints,
history, inspectable slots, artifact size, instructions per scan, and request
size. Target profiles may advertise smaller limits, in which case the effective
limit is the minimum. Exact current values and structured error codes are
normative in the [flow simulator reference](../reference/flow-simulator.md).

## Deployment model

Deployment resolves an immutable logical-context snapshot and all referenced
flow-program revisions, merges their virtual-point contracts, resolves physical
bindings for the selected execution instance, compiles each program, validates
the IL against that instance, prepares the complete replacement program set and
shared-point allocation, and swaps the context generation into service only
after every preparation succeeds. A failed merge, bind, compile, allocation, or
prepare leaves the prior complete deployment generation running.

Admission is bounded to 128 virtual points per context and execution instance,
including at most 64 retained points per context. A target may advertise a
smaller capacity; deployment uses the smaller effective limit.

The built-in `default` target means the server VM. Hardware templates advertise
the same vocabulary plus limits and supported Flow IL/ABI versions. A compiled
artifact may be reused only when all dependency revisions and target
requirements match. Artifact persistence stores compiler version, source digest,
IL digest, dependency revisions, and optional symbols separately from editable
flow JSON.

Server deployment is the baseline host. The existing
`POST /api/flows/{flowId}/deploy` endpoint is flow-local. Context-to-instance
deployment APIs are authoritative for composed multi-program deployments.
Controller transfer and
debug sessions remain alternate hosts in the same compiler pipeline. An
artifact may be reused across execution instances only when the complete
template capabilities, physical bindings, merged virtual-point contracts, and
dependency revisions match; otherwise the same portable source is compiled into
a separate target-resolved artifact.

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
- Emulator conformance tests compare identical inputs across emulator and
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
- The server VM is managed C#/.NET and must not load or invoke controller C code.
  Server and controller parity is enforced with shared artifacts and expected
  scan results rather than an in-process native ABI.
- There are no deployed flows to preserve before the production milestone. A
  pre-release compiler/format change updates or removes all fixtures and
  consumers in the same change.
- After the production milestone, a compiler upgrade does not silently replace running artifacts. Adoption is
  explicit redeployment with a recorded compiler and IL version.
