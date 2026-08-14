# Flow simulator and tutorial implementation plan

## 1. Goal

Allow a user to create or open a flow, define its inputs and outputs, execute it
without deploying to physical hardware, and debug its behaviour through the same
Flow IL compiler and portable VM used by production targets. The simulator must
also support deterministic, executable tutorials for every function block.

This is a delivery plan, not a replacement architecture. The existing portable
Flow IL architecture, debugger contract, PLC scan cycle, and pre-release version
policy remain authoritative.

## 2. Existing foundation

The repository already contains much of the low-level implementation required:

- The ASP.NET Core backend compiles resolved flow source into Flow IL.
- The portable VM is the normative evaluator for server, emulator, and controller
  hosts.
- Debug sessions support server, controller-emulator, and physical-controller
  targets.
- Tick, node, and instruction stepping, run, pause, restart, breakpoints, frame
  inspection, shadow outputs, and stale-revision detection already exist.
- The server-side emulator supports virtual time, Boolean input changes, output
  history, reset/power-cycle operations, and basic fault injection.
- The Vue designer already contains debug target, debug control, and emulator
  panels.
- Twenty of the thirty-six registered designer node kinds are currently marked
  executable. The remaining palette entries cannot yet be compiled and run.

The main gaps are product integration rather than a missing execution engine:

- Simulator state and controls are embedded in the designer view rather than
  presented as a coherent simulator workflow.
- A flow has no explicit, reusable input/output interface.
- Emulator inputs and outputs are point-oriented and mostly Boolean.
- Runtime values are not presented clearly on connectors throughout the graph.
- There is no persisted scenario/replay contract.
- There is no tutorial catalogue or tutorial coverage for function blocks.
- Executable coverage does not yet match the full node palette.

## 3. Architectural constraints

All implementation phases must preserve these rules:

1. The browser never compiles or evaluates a flow.
2. The backend compiles the current flow source to the single current Flow IL
   version.
3. The simulator executes that artifact through the portable VM. Do not add a
   JavaScript or separate C# graph evaluator.
4. Server, emulator, and controller debugging share compiler artifacts, stable
   node identities, debug maps, snapshots, and debugger semantics.
5. Each scan follows Read Inputs, Execute Logic, and Write Outputs. Simulator
   input values are frozen for the scan; state and outputs are published only at
   a successful commit boundary.
6. Simulator sessions are volatile and separate from saved definitions,
   deployed runtimes, and controller activation.
7. Simulator outputs are shadow outputs and cannot command physical equipment.
   Existing explicit controller live-output confirmation remains a separate
   controller-debugging feature.
8. Editing executable flow content makes the current simulator session stale.
   The UI must require recompilation instead of associating old results with a
   new graph.
9. During pre-release development, update the current schema and all producers,
   consumers, fixtures, and tests together. Do not add compatibility adapters
   for superseded formats.
10. Tests and UI changes follow `.codex/instructions.md`,
    `.codex/new-component.md`, and `.codex/test-documentation-rules.md`.

## 4. Target user experience

A user can:

1. Create or open a flow.
2. Define named, typed flow inputs and outputs.
3. Add matching input/output nodes and connect function blocks.
4. choose **Simulator** without saving, deploying, or configuring hardware.
5. Enter input values and input quality states.
6. Start the simulator, step one scan, step one node, step one instruction, run,
   pause, restart, or stop.
7. Add or remove node breakpoints directly on the canvas.
8. Observe connector values, node results, state, output values, quality, and
   diagnostics.
9. Advance deterministic virtual time and inject supported faults.
10. Record, save, replay, import, and export input scenarios and expectations.
11. Open a tutorial for a function block, change its example, and run it in the
    same simulator.

## 5. Contract decisions to settle first

Complete these decisions before changing persistence or compiler contracts.

### 5.1 Flow interface

Add a versioned flow interface to the current flow source schema. A suitable
initial model is:

```text
interface
  inputs[]
    id
    name
    dataType                 boolean | number | string | event
    units?
    defaultValue?
    required
  outputs[]
    id
    name
    dataType                 boolean | number | string | event
    units?
```

Add canonical `flowInput` and `flowOutput` node kinds. Each node stores an
`interfaceId` and obtains its connector type, name, units, and direction from
the referenced interface entry. Do not copy live values into node configuration
or the persisted flow DTO.

Flow-interface terminals differ from point nodes:

- interface terminals are portable flow boundaries used by simulation,
  tutorials, tests, and future reusable flows;
- point nodes bind a flow to virtual, external, or physical automation points;
- the simulator may offer point bindings as advanced inputs, but a tutorial must
  be runnable through interface terminals without external configuration.

Validation must cover unique stable IDs, unique user-facing names, supported
types, finite numeric defaults, compatible units, required defaults, references,
connector compatibility, and deletion of referenced entries.

### 5.2 Simulator session source

The simulator must accept the current draft graph as an executable source. The
backend remains responsible for validation, dependency resolution, compilation,
and revision calculation. Replace the frontend-only graph hash as the long-term
correlation authority with a backend-issued source revision/digest returned by
the compile/start operation.

### 5.3 Scenario format

Store scenarios separately from flow definitions. Use one current bounded
schema, for example:

```text
schemaVersion
id
name
description?
flowId
flowRevision
initialState?
steps[]
  atMilliseconds
  inputs
  qualities?
  action                     apply | step | advance | reset
expectations[]
  scan?
  outputId
  operator                   equals | approximately | changes | remains
  expectedValue?
  tolerance?
```

Scenarios use stable interface IDs, not display labels or DOM identities. A
scenario targeting another flow revision is reported as stale and is never
silently rebound.

### 5.4 Tutorial format

Tutorials are repository-owned, versioned content composed of:

- metadata: ID, title, function kind, category, objective, prerequisites;
- an ordinary current-schema flow fixture;
- one or more ordinary simulator scenarios;
- ordered guidance steps and expected observations;
- an optional challenge and solution fixture.

Tutorial execution must use the normal compiler, simulator APIs, and portable
VM. Do not implement tutorial-specific function semantics.

## 6. Delivery phases

Implementation status: Phases 0, 1, 2, 3, and 4 are complete. Phase 5 is next. The status refers
to this simulator plan; similarly named phases in the earlier portable Flow IL
roadmap are separate work.

### Phase 0 - Baseline and contract record

**Purpose:** Establish an accurate implementation baseline and freeze the first
simulator contracts before broad changes.

Tasks:

- Inventory executable and non-executable node kinds and map each executable
  node to compiler opcode/lowering, VM semantics, debug-map output, and tests.
- Inventory the current debug and emulator endpoints, service lifecycles, limits,
  error responses, and session cleanup paths.
- Add an ADR or contract document covering the flow interface, scenario storage,
  and the distinction between simulation and controller live-output debugging.
- Define stable diagnostic codes and JSON error envelopes for compile, session,
  input, scenario, stale-revision, and capability failures.
- Record shared limits for interface entries, scenarios, steps, expectations,
  breakpoints, history, inspectable values, and session duration.

Commit gate:

- The contract decisions are documented and reviewed.
- Existing backend, portable VM, unit, integration, and frontend test baselines
  pass without behavioural changes.

### Phase 1 - First-class simulator state and lifecycle

**Purpose:** Consolidate the existing debug/emulator components into a coherent
Simulator mode before adding more capabilities.

Backend tasks:

- Introduce or formalize application-level simulator endpoints that wrap the
  existing emulator and debug-session services without duplicating execution.
- Start a session from a draft executable source, compile it on the backend, and
  return the authoritative revision/digest, capabilities, inspection frame, and
  latest snapshot.
- Make session replacement, expiry, stop, disconnect, and server shutdown discard
  uncommitted frames and release resources deterministically.
- Separate simulator shadow sessions from deployed runtime and physical
  controller session namespaces.

Frontend tasks:

- Add an explicit Simulator mode/tab to the flow designer.
- Move debug/emulator request and lifecycle state from
  `AppFlowDesignerView.vue` into a dedicated Pinia simulator store and focused
  composables.
- Present lifecycle states consistently: idle, compiling, ready, running,
  paused, faulted, stopped, and stale.
- Change user-facing low-level labels such as **Load debug session** to actions
  such as **Start simulation**, while retaining an advanced debugger section.
- Disable or stop incompatible operations during requests and cancel stale
  requests on flow/route changes.

Tests:

- Unit-test lifecycle transitions, latest-request handling, stale revisions,
  cleanup, errors, and capability negotiation.
- Add API tests proving simulator isolation from deployment.
- Add Playwright coverage for start, step, restart, stop, edit-to-stale, keyboard
  operation, and status announcements.

Commit gate:

- A draft using currently executable blocks can run on the server-hosted
  simulator without deployment or a controller.
- Leaving the route or stopping a session releases it and commits no partial
  scan.

### Phase 2 - Persisted flow inputs and outputs

**Purpose:** Make flows self-contained and directly testable without point
definitions.

Backend and contract tasks:

- Extend the current flow persistence/source DTO with the agreed interface
  model and update strict parsers, validators, normalized storage, fixtures, and
  API contracts together.
- Add compiler source models for interface terminals.
- Add deterministic compiler lowering and typed slot allocation for `flowInput`
  and `flowOutput`.
- Add Flow IL bindings/metadata required to supply interface inputs and identify
  committed interface outputs without treating them as physical point commands.
- Preserve stable IDs through symbols, debug maps, and decompilation.
- Reject missing, duplicate, incompatible, or unsupported interface references
  with stable paths and node IDs.

Frontend tasks:

- Add flow settings for creating, renaming, reordering, and deleting interface
  inputs and outputs.
- Add `flowInput` and `flowOutput` to the node-kind registry and palette.
- Derive terminal connectors and node labels from the referenced interface
  entry.
- Preserve unsupported or missing references visibly with actionable
  diagnostics; never silently delete nodes or connections.
- Link interface-validation errors to both the settings field and affected node.

Tests:

- Contract fixture and parser parity tests.
- Compiler determinism, type, units, scheduling, symbols, debug-map, and
  decompiler round-trip tests.
- Portable VM tests for frozen input values and atomic output publication.
- Frontend unit and Playwright tests for CRUD, references, connections,
  persistence, reload, and accessible validation.

Commit gate:

- A flow with Boolean and number terminals saves, reloads, compiles, and executes
  with no point or controller configuration.
- Identical resolved source produces byte-identical Flow IL.

### Phase 3 - Typed simulator I/O workbench

**Purpose:** Let users supply and inspect values through understandable controls.

Tasks:

- Generate input controls from the flow interface:
  - Boolean switch;
  - finite numeric field with optional units and range constraints;
  - text field or constrained state selector;
  - event trigger and payload control when event execution is supported.
- Allow quality selection such as good, bad, stale, and unavailable where the
  selected execution profile supports it.
- Add **Apply inputs and step** as the primary deterministic action.
- Freeze one coherent interface-input image at scan start.
- Present each output's typed value, units, quality, last-change scan, and bounded
  history.
- Clearly label proposed, committed simulator, deployed, and physical output
  states so they cannot be confused.
- Generalize emulator API/client parsing beyond Boolean-only input and output
  shapes using the existing typed-value vocabulary.
- Add reset-to-default and reset-state operations.

Tests:

- Boundary tests for each type, non-finite numbers, missing required inputs,
  type mismatch, bad quality, stale values, reset, and atomic commits.
- Cross-host fixture tests showing identical simulator inputs produce identical
  server/emulator snapshots.
- Responsive and accessible frontend coverage for long names, units, errors,
  keyboard use, status announcements, and mobile reflow.

Commit gate:

- Boolean, number, and string interface values can be entered and observed
  without raw JSON or external points.
- Invalid or missing input never becomes false, zero, or empty text silently.

### Phase 4 - Visual graph debugging

**Purpose:** Make it possible to diagnose a flow at node and connector level.

Tasks:

- Overlay the latest typed value, units, and quality on every connector with a
  value in the current snapshot/frame.
- Highlight the current node and distinguish committed values from uncommitted
  paused-frame values.
- Add accessible canvas actions for before-node and after-node breakpoints.
- Display breakpoint state without relying on colour alone.
- Add execution-order and breakpoint summaries outside the SVG canvas.
- Show current and staged-next values for stateful blocks.
- Link compile and runtime diagnostics to nodes, connectors, interface entries,
  and configuration fields.
- Keep instruction pointer, raw slots, and VM details in an expandable advanced
  inspector.
- Add run-to-node and run-to-tick-boundary actions.

Tests:

- Debugger conformance tests pause at every legal instruction and prove that
  resuming produces the same committed snapshot as uninterrupted execution.
- Tests abort at every stop point and prove no state/output commit occurred.
- Unit and Playwright tests cover breakpoint controls, overlays, focus,
  keyboard operation, accessible names/states, stale overlays, and errors.

Commit gate:

- A user can trace a value through the graph and identify the first node that
  differs from the expected result without reading raw VM slots.

### Phase 5 - Complete executable function coverage

**Purpose:** Ensure every function offered as simulatable has production-grade
portable semantics.

Implement in vertical slices:

1. Maths: `average`, `clamp`, `min`, `max`, `line`, and `calculator`.
2. Logic and routing: `if`, `selector`, `split`, and `sequence`.
3. Time and state: `delay`, `timer`, `pulse`, `schedule`, and `calendar`.
4. Override and any remaining point-oriented functions.
5. Event-driven blocks after the event source/execution contract is complete.

For each block:

- Specify connector types, arity, units, configuration, state, quality,
  initialization, reset, fault, and scan semantics.
- Add canonical compiler validation and lowering.
- Implement opcode semantics once in the portable C VM.
- Update the loader's capability and bounds validation.
- Add debug-map/source identity coverage and frame inspection.
- Update default and custom controller-template capability vocabulary.
- Add deterministic compiler fixtures, VM tests, native/.NET boundary tests,
  emulator tests, frontend configuration tests, and Playwright simulation tests.
- Set `executable: true` only when the complete vertical slice passes.

Commit gate for each block:

- It compiles and produces deterministic output.
- Server and emulator snapshots match for shared fixtures.
- Pausing/aborting cannot commit partial state or output.
- The palette, configuration editor, compiler, VM, controller capabilities,
  icons, fixtures, and documentation use one canonical kind.

### Phase 6 - Scenario recording, persistence, and replay

**Purpose:** Make simulations reproducible for debugging and CI.

Backend tasks:

- Add strict scenario parsing, validation, persistence, listing, retrieval,
  update, deletion, import, and export.
- Keep scenarios separate from flow definitions and compiled artifacts.
- Execute scenarios with deterministic virtual time and bounded work.
- Evaluate output expectations using stable interface IDs and typed comparison
  rules.
- Return structured failures containing step, scan, output ID, expected value,
  actual value, quality, and diagnostic code.
- Reject stale flow revisions and unsupported scenario versions explicitly.

Frontend tasks:

- Add record, stop recording, save, replay, run-all, reset, and
  advance-to-next-event controls.
- Add a scenario step/expectation editor with accessible validation.
- Show a timeline of applied inputs, scans, faults, resets, and outputs.
- Present expectation results and navigate failures to the relevant output node.
- Allow explicit import/export of bounded scenario fixtures.

Tests:

- Deterministic replay, stale revision, reset, stateful replay, time-driven
  behavior, quality/fault steps, expectation matching, limits, cancellation, and
  malformed import coverage.
- CI command that runs repository tutorial/scenario fixtures without starting a
  browser.
- Playwright coverage for record, save, reload, replay, and failure diagnosis.

Commit gate:

- A user can reproduce a reported simulator failure from one stored scenario.
- The same scenario is runnable headlessly in CI.

### Phase 7 - Function tutorial framework

**Purpose:** Teach each executable function through real, editable simulator
flows.

Tasks:

- Add a repository-owned tutorial catalogue and strict tutorial parser.
- Add searchable tutorials grouped by logic, maths, routing, timing, and
  override.
- Add **Learn this block** to palette entries and node help/configuration.
- Open tutorials as disposable examples or user-owned copies; canonical
  tutorial fixtures remain immutable.
- Guide the user through setting inputs, stepping, advancing time, observing
  nodes, and validating outputs.
- Add optional challenges and solution flows.
- Provide tutorials for the first milestone blocks:
  `and`, `or`, `not`, `add`, `comparator`, `onDelay`, and `memory`.
- Then require at least one passing tutorial for every executable node kind.
- Add a coverage check that fails when:
  - an executable function has no tutorial;
  - a tutorial references an unknown/non-executable function;
  - its flow does not compile;
  - its scenario or expectations fail;
  - its schema/version is not current.

Commit gate:

- Every executable palette function links to at least one automatically verified
  tutorial.
- Tutorials run through the normal compiler and simulator, not mocked results.

### Phase 8 - Hardening, accessibility, and conformance

**Purpose:** Establish the simulator as a safe, bounded, cross-target execution
and debugging tool.

Tasks:

- Bound active sessions, lease duration, graph/interface size, scenario size,
  step count, runtime, snapshot/history size, breakpoint count, inspectable
  slots, and API request/response sizes.
- Add deterministic cleanup for expiry, disconnect, route change, replacement,
  cancellation, VM fault, and shutdown.
- Fuzz source, scenario, input-frame, artifact, snapshot, and native ABI
  boundaries.
- Run maximum-size and long-duration tests for memory, scheduling,
  cancellation, and backpressure.
- Differentially execute identical artifact/input/prior-state tuples on server,
  emulator, portable host tests, and supported controllers.
- Prove reset, power cycle, stale/bad input, output failure, deadline overrun,
  and debugger abort semantics.
- Complete WCAG 2.2 AA coverage for the simulator and tutorial experience,
  including keyboard access, focus management, status/error announcements,
  contrast, zoom/reflow, touch targets, and all supported viewports.
- Keep Playwright tests separated into simulator lifecycle, I/O, debugging,
  scenarios, and tutorials rather than adding unrelated cases to general route
  suites.

Commit gate:

- All repository quality and conformance gates pass.
- No simulator path can activate or command physical output.
- Cross-host shared fixtures produce identical committed snapshots within the
  explicitly documented target capability profile.

## 7. Recommended first milestone

Deliver the following before expanding to the full function catalogue:

- First-class Simulator mode and Pinia lifecycle store.
- Backend-authoritative draft compilation and session revision/digest.
- Boolean and numeric flow inputs and outputs.
- Start, step tick, step node, restart, and stop.
- Typed input controls and captured shadow outputs.
- Connector/node value overlays and node breakpoints.
- Deterministic scenario replay.
- Verified tutorials for `and`, `or`, `not`, `add`, `comparator`, `onDelay`, and
  `memory`.

This milestone exercises combinational logic, maths, comparison, time, and
retained state while proving the complete flow-interface-to-portable-VM path.

## 8. Likely repository touchpoints

The exact structure may evolve, but implementation should begin by reviewing
and extending these existing areas.

Frontend:

- `frontend/flow-control-ui/src/features/flows/nodeKinds.ts`
- `frontend/flow-control-ui/src/features/flows/types.ts`
- `frontend/flow-control-ui/src/features/flows/flowDebugSource.ts`
- `frontend/flow-control-ui/src/features/flows/api/flowDto.ts`
- `frontend/flow-control-ui/src/features/flows/api/flowDebugApi.ts`
- `frontend/flow-control-ui/src/features/flows/api/flowEmulatorApi.ts`
- `frontend/flow-control-ui/src/features/flows/views/AppFlowDesignerView.vue`
- `frontend/flow-control-ui/src/features/flows/components/AppFlowDebugPanel.vue`
- `frontend/flow-control-ui/src/features/flows/components/AppFlowEmulatorPanel.vue`
- `frontend/flow-control-ui/src/features/flows/components/AppFlowDesignerCanvas.vue`
- `frontend/flow-control-ui/src/features/flows/components/AppFlowNode.vue`
- `frontend/flow-control-ui/src/features/flows/stores/`
- `frontend/flow-control-ui/e2e/portableFlowDebug.spec.ts`

Backend:

- `backend/Server/Server.Api/Extensions/FlowDebugEndpointRouteBuilderExtensions.cs`
- `backend/Server/Server.Api/Extensions/FlowEmulatorEndpointRouteBuilderExtensions.cs`
- `backend/Server/Server.Services/IFlowCompiler.cs`
- `backend/Server/Server.Services/IFlowDebugService.cs`
- `backend/Server/Server.Services/IFlowEmulatorService.cs`
- `backend/Server/Server.Services/Implementation/FlowCompiler.cs`
- `backend/Server/Server.Services/Implementation/FlowDebugService.cs`
- `backend/Server/Server.Services/Implementation/FlowEmulatorService.cs`
- `backend/Server/Server.Services/Implementation/LocalFlowDebugSession.cs`
- `backend/Server/Tests.Unit/Flows/`

Portable contracts/runtime:

- `controllers/shared/flow/`
- `controllers/tests/`
- `docs/flow-il-v1-contract.md`
- `docs/flow-il-v1-debug-contract.md`
- `docs/flow-vm-host-abi-v1.md`
- `docs/plc-scan-cycle.md`
- `testdata/contracts/flow-il-v1/`

Repository guidance and contracts:

- `.codex/instructions.md`
- `.codex/flows.md`
- `.codex/ui-flow-schema.md`
- `.codex/ui-runtime-api.md`
- `.codex/implementation-plan.md`
- `.codex/new-component.md`
- `.codex/test-documentation-rules.md`

## 9. Verification gates

Run gates proportionate to each phase, and run the complete set before marking
the simulator delivery complete.

Frontend, from `frontend/flow-control-ui`:

```sh
npm run format
npm run lint
npm run type-check
npm run test:unit -- --run
npm run test:e2e
npm run test:e2e:dotnet
npm run build
```

Backend, from the repository root or `backend/Server` as appropriate:

```sh
dotnet format backend/Server/Server.slnx --verify-no-changes
dotnet build backend/Server/Server.slnx
dotnet test backend/Server/Server.slnx
```

Portable VM/host tests, from `controllers`:

```sh
cmake -S tests -B build-host
cmake --build build-host
ctest --test-dir build-host --output-on-failure
```

Flow IL fixtures:

```sh
node tools/generate-flow-il-v1-fixtures.mjs --check
```

Each phase must also add targeted tests for its trust boundaries, lifecycle,
atomic scan behavior, stale revisions, cancellation, accessibility, and failure
states. A phase is not complete merely because its happy-path UI works.

## 10. Definition of done

The overall plan is complete when:

- A flow can explicitly declare typed inputs and outputs.
- A draft can be compiled and simulated without deployment, configured points,
  or physical hardware.
- Users can run, pause, step scans/nodes/instructions, use breakpoints, inspect
  state, and diagnose values on the graph.
- Simulator input, output, state, quality, timing, and fault semantics follow the
  same portable VM and PLC scan cycle as production targets.
- Scenarios are deterministic, persisted separately, replayable, and runnable in
  CI.
- Every executable function block has at least one passing simulator tutorial.
- Every palette function described as executable has complete compiler, VM,
  debug, emulator, frontend, and conformance coverage.
- Simulator sessions are bounded and reliably cleaned up.
- Simulator output cannot operate physical equipment.
- Required frontend, backend, portable-host, fixture, accessibility, and build
  gates pass.
