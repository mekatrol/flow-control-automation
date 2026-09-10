# Unified flow execution context implementation plan

## Purpose

Unify flow simulation and debugging behind one backend-owned execution-context
contract. The Vue application may display whether a context is simulated or
connected to a debug target, but it must not select different execution APIs or
implementations when the user runs, pauses, steps, restarts, inspects, or sets a
breakpoint.

After creation, the browser addresses every operation using an execution-context
ID. The ASP.NET Core backend resolves that ID and controls the appropriate VM
host, emulator, controller transport, lifecycle, and cleanup.

This replaces the current arrangement in which the frontend separately consumes
`debug-sessions` and `simulator-sessions`, and
`AppFlowDesignerView.vue` understands both implementations.

## Required outcomes

1. Simulation and debugging implement the same backend execution contract.
2. Both modes support run, pause, stop, restart, tick/node/instruction stepping,
   breakpoints, run-to, inspection, and snapshots.
3. Simulator facilities such as input editing, virtual time, reset, and fault
   injection are advertised through capabilities on the same context.
4. Controller facilities such as live physical output commissioning are also
   advertised through capabilities.
5. Every operation after context creation uses only the execution-context ID.
6. The backend owns the decision about how a context executes.
7. The frontend has one API module and one `useRuntimeContext` composable.
8. The designer does not branch on workspace mode to perform execution.
9. Context metadata remains available for labels, warnings, available controls,
   and edit-locking policy.
10. Context creation resolves, validates, compiles, and loads the current saved
    flow automatically. There is no separate debugger **Load** action.

## Existing implementation

The backend currently exposes:

- `/api/flows/{flowId}/debug-sessions`
- `/api/flows/{flowId}/simulator-sessions`

This is mostly an API and lifecycle separation, not an execution-engine
requirement. `FlowSimulatorService` already creates a private
`FlowDebugSessionRegistry` and delegates execution to `FlowDebugService`.
The simulator therefore already uses debugger and portable Flow VM semantics.

The frontend nevertheless has separate APIs, state, lifecycle names, and
composables. Its `useRuntimeContext` exposes `debug` and `simulator`
children, causing the designer to unwrap both. This is the abstraction leak
this plan removes.

## Architectural boundary

### Backend responsibilities

The backend:

- creates and owns execution contexts;
- resolves the stored flow and requested revision;
- compiles and automatically loads the flow;
- selects the server VM, emulator, or physical controller transport;
- owns sessions, events/polling, leases, cancellation, and cleanup;
- validates operations against context capabilities;
- normalizes lifecycle, snapshots, inspection, breakpoints, and diagnostics;
- returns metadata required for presentation and safety decisions.

### Frontend responsibilities

The frontend:

- saves the current draft before requesting execution;
- creates or receives an execution-context ID;
- invokes uniform operations using that ID;
- renders returned lifecycle, snapshot, inspection, and diagnostics;
- enables controls from backend-reported capabilities;
- displays context mode and safety metadata.

The frontend must not choose a simulator API versus debugger API for an
operation or infer capabilities from `WorkspaceMode`. Workspace mode may be
used for presentation and when initially requesting a context, but it is not
an execution dispatcher.

## Backend domain contract

Introduce a mode-neutral data contract under
`Server.Common.Models/FlowExecution`, with its enums under
`Server.Common.Types`:

```csharp
public enum FlowExecutionMode
{
    Simulator,
    Debugger
}

public enum FlowExecutionLifecycle
{
    Preparing,
    Ready,
    Running,
    Paused,
    Stepping,
    Stale,
    Faulted,
    Stopped
}

public sealed record FlowExecutionContext
{
    public required string Id { get; init; }
    public required string FlowId { get; init; }
    public required int Revision { get; init; }
    public required FlowExecutionMode Mode { get; init; }
    public required FlowExecutionLifecycle Lifecycle { get; init; }
    public required FlowExecutionCapabilities Capabilities { get; init; }
    public required IReadOnlyList<FlowDebugBreakpoint> Breakpoints { get; init; }
    public DebugRuntimeSnapshot? Snapshot { get; init; }
    public FlowDebugInspection? Inspection { get; init; }
    public FlowExecutionIo? Io { get; init; }
    public FlowExecutionPresentation Presentation { get; init; } = new();
    public FlowExecutionDiagnostic? Diagnostic { get; init; }
}
```

Reuse existing VM snapshot, typed-value, inspection, and breakpoint vocabulary
where it is already mode-neutral. Rename contracts carrying a misleading
`Debug` prefix only when that does not cause an unnecessary wire migration.

### Capabilities

Unsupported operations are capabilities, not missing service methods:

```csharp
public sealed record FlowExecutionCapabilities
{
    public bool CanRun { get; init; }
    public bool CanPause { get; init; }
    public bool CanStop { get; init; }
    public bool CanRestart { get; init; }
    public bool CanStepTick { get; init; }
    public bool CanStepNode { get; init; }
    public bool CanStepInstruction { get; init; }
    public bool CanUseBreakpoints { get; init; }
    public bool CanRunTo { get; init; }
    public bool CanEditInputs { get; init; }
    public bool CanAdvanceVirtualTime { get; init; }
    public bool CanInjectFaults { get; init; }
    public bool CanResetIo { get; init; }
    public bool CanEnableLiveOutputs { get; init; }
    public bool LocksFlowEditing { get; init; }
}
```

Core VM debugging capabilities must be enabled for both simulated and debug
contexts. Genuine target limitations remain explicit.

### Presentation metadata

Return mode information without using it to dispatch operations:

```csharp
public sealed record FlowExecutionPresentation
{
    public string ModeLabel { get; init; } = "";
    public string HostLabel { get; init; } = "";
    public bool IsSimulated { get; init; }
    public bool UsesPhysicalIo { get; init; }
}
```

The UI uses this for headings, badges, physical-output warnings, and editing
policy. Operational code still calls the same methods.

## Backend service and registry

Add `IFlowExecutionContextService` under `Server.Common.Contracts`, with
its internal implementation under `Server.Services/FlowExecution`. It has a
uniform operation set:

```csharp
Task<FlowExecutionContext> CreateAsync(CreateFlowExecutionContext request, CancellationToken token);
Task<FlowExecutionContext> GetAsync(string contextId, CancellationToken token);
Task<FlowExecutionContext> RunAsync(string contextId, RunFlowExecution request, CancellationToken token);
Task<FlowExecutionContext> PauseAsync(string contextId, CancellationToken token);
Task<FlowExecutionContext> StopAsync(string contextId, CancellationToken token);
Task<FlowExecutionContext> RestartAsync(string contextId, CancellationToken token);
Task<FlowExecutionContext> StepTickAsync(string contextId, CancellationToken token);
Task<FlowExecutionContext> StepNodeAsync(string contextId, CancellationToken token);
Task<FlowExecutionContext> StepInstructionAsync(string contextId, CancellationToken token);
Task<FlowExecutionContext> RunToAsync(string contextId, FlowDebugBreakpoint boundary, CancellationToken token);
Task<FlowExecutionContext> ReplaceBreakpointsAsync(
    string contextId,
    IReadOnlyList<FlowDebugBreakpoint> breakpoints,
    CancellationToken token);
```

Input, virtual-time, fault, reset, and live-output methods belong to this same
service. An unsupported operation produces a structured capability error.

Replace the independent registries with one internal registry under
`Server.Services/FlowExecution`, keyed by context ID. An
internal entry records mode and owns implementation resources:

- Flow VM/debug session;
- emulator ID and simulated I/O;
- physical controller transport identity;
- lease and last-access time;
- cancellation source;
- live-output state;
- idempotent cleanup.

Only the backend examines this entry to select an implementation. IDs must be
unguessable, scoped to the requesting principal when authentication exists,
and rejected if used against another flow.

## Automatic flow preparation

Remove the explicit debugger Load operation. Creating a context atomically:

1. Reads the flow from the authoritative backend store.
2. Verifies `expectedRevision`.
3. Resolves the target and capabilities.
4. Validates point contracts and execution dependencies.
5. Compiles the saved flow to canonical Flow IL.
6. Creates an emulator when required.
7. Loads the artifact into the selected VM host.
8. Applies initial breakpoints.
9. Registers and returns a `Ready` context.

Failures must not leave a partial context or emulator.

The browser currently sends `ExecutableFlowSource`. The new create request
carries identity and intent:

```json
{
  "mode": "simulator",
  "expectedRevision": 42,
  "targetId": "server",
  "replaceExisting": true,
  "breakpoints": []
}
```

The frontend saves first. A revision mismatch returns `409 Conflict`; invalid
or uncompilable content returns structured `422 Unprocessable Entity`.

## Unified HTTP API

```text
POST   /api/flows/{flowId}/execution-contexts
GET    /api/execution-contexts/{contextId}
DELETE /api/execution-contexts/{contextId}

POST   /api/execution-contexts/{contextId}/run
POST   /api/execution-contexts/{contextId}/pause
POST   /api/execution-contexts/{contextId}/stop
POST   /api/execution-contexts/{contextId}/restart
POST   /api/execution-contexts/{contextId}/step-tick
POST   /api/execution-contexts/{contextId}/step-node
POST   /api/execution-contexts/{contextId}/step-instruction
POST   /api/execution-contexts/{contextId}/run-to
PUT    /api/execution-contexts/{contextId}/breakpoints

PUT    /api/execution-contexts/{contextId}/inputs
POST   /api/execution-contexts/{contextId}/advance
PUT    /api/execution-contexts/{contextId}/fault
POST   /api/execution-contexts/{contextId}/reset-io
POST   /api/execution-contexts/{contextId}/reset-inputs
POST   /api/execution-contexts/{contextId}/live-output
POST   /api/execution-contexts/{contextId}/keepalive
GET    /api/execution-contexts/{contextId}/events
```

Every successful mutation returns the same context envelope.

| Condition | Status |
| --- | --- |
| Unknown flow or context | 404 |
| Stale revision or conflicting active context | 409 |
| Invalid flow, target, or unsupported operation | 422 |
| Live-output authorization failure | 403 |

Server-sent events are preferred. Bounded polling may remain during migration,
but it must live in `useRuntimeContext` and use the same endpoint for both
modes.

## Frontend contract

Replace `flowDebugApi.ts` and `flowSimulatorApi.ts` with
`flowExecutionContextApi.ts`. Replace both mode composables and the simulator
store with one composable:

```ts
interface RuntimeContext {
  context: Readonly<Ref<FlowExecutionContext | undefined>>;
  contextId: ComputedRef<string | undefined>;
  lifecycle: ComputedRef<FlowExecutionLifecycle>;
  capabilities: ComputedRef<FlowExecutionCapabilities>;
  presentation: ComputedRef<FlowExecutionPresentation>;
  runtime: ComputedRef<FlowRuntimeSnapshot | undefined>;
  inspection: ComputedRef<FlowExecutionInspection | undefined>;
  breakpoints: ComputedRef<FlowExecutionBreakpoint[]>;
  io: ComputedRef<FlowExecutionIo | undefined>;
  error: Readonly<Ref<string | undefined>>;

  create(request: CreateExecutionContext): Promise<void>;
  refresh(): Promise<void>;
  run(): Promise<void>;
  pause(): Promise<void>;
  stop(keepalive?: boolean): Promise<void>;
  restart(): Promise<void>;
  stepTick(): Promise<void>;
  stepNode(): Promise<void>;
  stepInstruction(): Promise<void>;
  runTo(boundary: FlowExecutionBreakpoint): Promise<void>;
  replaceBreakpoints(values: FlowExecutionBreakpoint[]): Promise<void>;
  applyInputs(values: FlowExecutionInputChange[]): Promise<void>;
  advance(milliseconds: number): Promise<void>;
  injectFault(fault: string | null): Promise<void>;
  resetIo(powerCycle: boolean): Promise<void>;
  resetInputs(): Promise<void>;
  enableLiveOutput(pointIds: string[]): Promise<void>;
}
```

There are no `debug` or `simulator` properties. Every method exists in every
mode, with availability controlled by backend capabilities.

## Designer changes

`AppFlowDesignerView.vue` constructs one runtime context:

```ts
const execution = useRuntimeContext({
  flowId,
  requestedMode: workspaceMode,
  targetId: selectedTargetId
});
```

Remove operational references to:

- `execution.debug` and `execution.simulator`;
- `isDebuggerWorkspace` and `isSimulatorWorkspace`;
- mode-specific lifecycle and snapshot aliases;
- `loadDebugSession` and `startSimulation`.

Replace separate simulator/debugger panels with one capability-driven execution
panel. Context creation automatically loads the saved flow, so remove **Load**.
Optional simulated I/O, virtual-time, fault, and live-output sections are shown
by capability. Editing is controlled by `LocksFlowEditing`, not local mode
assumptions.

## Remaining delivery phases

### Phase 2 — Unified registry

- Add the internal execution-context registry under
  `Server.Services/FlowExecution`.
- Move ownership, leases, cancellation, and cleanup into it.
- Adapt debug and simulator execution through the same VM engine.
- Make stop and cleanup idempotent.
- Test expiration, replacement, and concurrency.

### Phase 3 — Unified service

- Implement `IFlowExecutionContextService`.
- Resolve and compile saved flows during creation.
- Implement core operations for both modes.
- Add breakpoint and run-to parity for simulation.
- Normalize state, snapshots, inspection, and errors.
- Enforce optional operations through capabilities.

### Phase 4 — Unified endpoints

- Add context endpoints and request validation.
- Add structured error mapping.
- Add common event or polling support.
- Replace the old debug and simulator endpoints in the same coordinated
  backend/frontend change. Do not add compatibility adapters.

### Phase 5 — Single frontend data layer

- Add `flowExecutionContextApi.ts`.
- Implement the single `useRuntimeContext`.
- Centralize cancellation, updates, keepalive, and teardown.
- Add composable and strict parser tests.
- Stop exporting mode-specific implementations.

### Phase 6 — Unified execution UI

- Replace separate control panels with one capability-driven panel.
- Remove the debugger Load action.
- Save and create the context as one user workflow.
- Bind canvas runtime, inspection, breakpoints, and I/O to the common context.
- Use presentation metadata only for labels and warnings.

### Phase 7 — Legacy removal and verification

- Migrate browser E2E interception to unified endpoints.
- Run backend, frontend, emulator/controller, and accessibility suites.
- Remove old frontend APIs, composables, and simulator store.
- Remove the old debug and simulator endpoints and registrations; their
  `IFlowDebugService` and `IFlowSimulatorService` contracts; session request,
  model, option, exception, registry, and service types; and mode-specific
  mapping code.
- Replace and remove debug-prefixed public snapshot, typed-value, breakpoint,
  capability, inspection, and controller-debug envelope models that duplicate
  the unified execution-context contract.
- Remove `CompatibilitySnapshot`, `ToCompatibilitySnapshot`, and duplicate
  debug snapshot mapping. Keep one canonical execution snapshot mapper and one
  current execution-context schema fixture.
- Remove old frontend execution APIs, composables, simulator state, E2E mocks,
  and implementation-coupled tests. Reorganize debugger and simulator coverage
  as shared execution-context service, registry, endpoint, and UI tests.
- Review canonical point, point-source, controller-template, controller seed,
  and normalized JSON/YAML artifacts. Remove obsolete aliases, missing-field
  defaults, debug/session properties, and other tolerant parser or serializer
  paths; retain negative fixtures that verify strict rejection.
- Delete the orphaned `testdata/contracts/flows/legacy.json` rather than
  modernizing it. Replace only useful graph coverage with a current-schema
  fixture, and remove latent copy/output references.
- Audit flow and configuration mappers, serializers, validators, stores, and
  JSON options for missing-version or `controllerTemplateId` defaults,
  alternate names or spellings, case-insensitive values, scalar coercion,
  ignored unknown fields, dual properties, and read-old/write-new behavior.
  Replace each compatibility branch with strict validation and a rejection
  test for the obsolete input while preserving security rejection and bounds.
- Keep a single reviewed pre-release `InitialCreate` database migration. If
  this work creates intermediate migrations, squash them according to
  `docs/development/database-migrations.md`; test fresh current databases, not
  upgrades from unsupported pre-release versions.
- Remove current-document promises of legacy flow, session, field, or endpoint
  compatibility and update API references to the unified endpoints and current
  schemas. Do not include generated output, copied fixtures, test results, or
  local runtime data in the change set.
- Add strict rejection tests for removed old request and document formats.
- Update related reference documentation.

## Test plan

### Backend

- Create both modes through one service and response contract.
- Automatically resolve, compile, and load the saved revision.
- Reject stale revisions without leaking resources.
- Run, pause, restart, stop, and all step variants in both modes.
- Set, replace, remove, and hit breakpoints in both modes.
- Run to the same boundary in both modes.
- Return equivalent snapshot and inspection shapes.
- Resolve operations solely from context ID.
- Reject expired, mismatched, or unknown IDs.
- Enforce optional-operation capabilities.
- Preserve PLC scan-cycle commit semantics while paused inside a tick.
- Clean up emulator, controller, VM, timers, and live outputs exactly once.
- Isolate concurrent contexts.

### Frontend

- Parse the common envelope strictly.
- Send context ID on every post-creation operation.
- Never select an API based on mode.
- Render lifecycle and diagnostics identically.
- Enable controls from capabilities.
- Save before context creation and handle revision conflicts.
- Stop on route exit/unload and ignore stale updates.

### End-to-end

1. In simulation, automatically create a context, add a breakpoint, run, pause,
   step, and stop.
2. In debugging, perform the same sequence through the same endpoints.
3. Confirm no Load interaction exists.
4. Confirm labels differ while operational controls and calls do not.
5. Confirm edit locking follows backend metadata.
6. Confirm simulator inputs, virtual time, and faults appear by capability.
7. Confirm live-output warnings appear by capability.
8. Confirm stale revisions do not leak sessions.

## Rollout

Use a coordinated pre-release cutover:

1. Implement the unified backend service and endpoints behind the new tests.
2. Migrate the frontend API, composable, UI, unit tests, and E2E mocks.
3. Delete old endpoints, contracts, registries, models, compatibility
   projections, and fixtures in that same delivery sequence.
4. Run all backend, frontend, controller/emulator, and browser suites.
5. Observe context creation, cleanup, expiry, and operation failures.

Do not ship dual execution APIs, aliases, or read-old/write-new behavior. There
is no released session contract requiring a compatibility window.

## Completion criteria

The work is complete when:

- simulator and debugger implement the same core operations;
- the browser uses one context API and one composable;
- post-creation operations use only the context ID;
- context creation automatically resolves, compiles, and loads the saved flow;
- the debugger has no Load button or Load-specific action;
- the designer contains no execution dispatch based on workspace mode;
- mode appears only as backend-provided presentation and policy metadata;
- capabilities, not mode checks, control optional UI;
- old session APIs and mode-specific frontend state are removed;
- obsolete YAML/JSON fields, legacy fixtures, compatibility projections, and
  acceptance tests listed in Phase 7 are removed;
- unit tests are reorganized and updated for current contracts, with explicit
  rejection coverage replacing legacy acceptance coverage;
- backend, frontend, E2E, accessibility, cleanup, and concurrency tests pass.

Add an enforcement test or lint rule that fails if
`AppFlowDesignerView.vue` imports a simulator/debug execution API or accesses
a mode-specific runtime child.
