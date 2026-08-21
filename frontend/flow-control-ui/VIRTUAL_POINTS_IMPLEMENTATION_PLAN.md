# Virtual Points and VM Execution Contexts — Implementation Plan

## Goal

Allow portable flow programs to declare analog or digital virtual-point dependencies, group those programs into a logical execution context, and deploy that context to one or more concrete server or controller execution instances. Programs running on the same execution instance exchange values through instance-global named virtual points. The same virtual-point key on a different execution instance represents a different value.

Examples:

- `server-instance/temp-setpoint`
- `controller-east-instance/temp-setpoint`
- `controller-west-instance/temp-setpoint`

These are three independent runtime values even though their point key is the same. Two different flow programs deployed to `controller-east-instance` and referencing `temp-setpoint` share the same memory cell.

Flow authors use the existing Analog Input, Digital Input, Analog Output, and Digital Output nodes for both physical and virtual points. A flow is a portable program and must not be permanently tied to a controller template, controller instance, or the server VM. It declares typed point keys. A logical execution context groups programs and their point contracts; each context deployment binds those contracts to a concrete execution instance and its physical capabilities.

## Product decisions

1. A virtual point is either analog or digital for the first release.
2. A virtual point has `implementation: virtual`, `direction: value`, and may be readable, commandable, or both.
3. Analog/Digital Input and Output nodes are the only point-access nodes. Their behavior is determined by the selected point definition; separate virtual-input and virtual-output node kinds are not needed.
4. A flow is a portable program that may be deployed more than once and to different controller types or the server VM when its requirements are compatible.
5. A logical execution context is a portable deployment composition: it selects flow programs, unifies their declared point contracts, and supplies target-independent configuration.
6. An execution instance is a concrete server VM or installed controller. A controller template describes an instance's capabilities and limits but is not an instance or a runtime namespace.
7. One logical execution context may be deployed to many heterogeneous execution instances. Each deployment is compiled or resolved for its target instance.
8. Runtime virtual-point identity is `(executionInstanceId, pointKey)`. Virtual points are global across all programs on that instance, including programs originating from different context deployments if the host permits more than one context.
9. Two declarations of the same virtual-point key on one instance must unify to a compatible contract. Type, units, persistence, default, and capability conflicts fail deployment.
10. Flows communicate when deployed to the same execution instance and referencing the same compatible virtual-point key.
11. Physical/bound points remain backed by a point source or instance-specific controller mapping. Virtual points have no physical source or mapping.
12. Multiple flows may read a virtual point. Output-driver ownership must be deterministic; the initial implementation permits one active writer per point per execution instance.
13. `volatile` values reset when their execution instance restarts. `retained` values survive restarts using instance-scoped persistence.
14. Shared virtual-point access must be thread-safe. Individual flow scans may execute concurrently, but no program may observe a torn value or another program's uncommitted working state.
15. Flow Input and Flow Output are not valid node kinds. Analog/Digital Input and Output nodes backed by virtual points provide cross-flow communication.

## Existing implementation we can reuse

The repository already contains part of this model:

- Point definitions support `implementation: virtual` and `implementation: bound`.
- Virtual points are required to use `direction: value`.
- Point value types already include `analog` and `digital`.
- Point definitions already support readable/commandable capabilities and `volatile`/`retained` persistence.
- The compiler target resolver already accepts a readable virtual point for Analog/Digital Input and a commandable virtual point for Analog/Digital Output.
- Controller runtime capabilities already include `VirtualPoints`.
- Flow I/O nodes already store a `pointId`.

The incomplete pieces are:

- There is no distinction between a portable logical execution context and a concrete controller/server execution instance.
- Flow source does not own portable virtual-point declarations.
- Point definitions, deployment bindings, and runtime values are not correctly separated.
- The flow designer uses a free-text point ID rather than a searchable lookup.
- The server virtual-point read path always reports `not_initialized`.
- Server flow output commands are cached but are not committed into shared virtual-point state.
- Cross-flow writer conflict, scan visibility, persistence, and deployment rules are not fully defined.

## Target domain model

### Flow program and variable declarations

A flow definition is portable source code. It declares the virtual-point keys it reads or writes without naming a controller type or concrete runtime:

```text
FlowProgram
  id
  revision
  graph
  virtualPointDeclarations[]

VirtualPointDeclaration
  key                   stable program-visible key, for example temp-setpoint
  valueType             analog | digital
  units                 analog only
  readable
  commandable
  persistence
  relinquishDefault
```

The declarations are program requirements, not allocated runtime memory. Input/output nodes reference the declaration key. Reusing the same key within one program refers to the same declaration.

### Logical execution context

A logical execution context is a portable composition that selects programs and unifies their requirements. It is comparable to an application manifest, not a running VM:

```text
ExecutionContextDefinition
  id
  name
  programs[]            flow ID plus immutable revision
  pointContracts[]      merged virtual declarations and logical physical roles
  schedulingPolicy
  revision
```

The context merger groups virtual declarations by key. Equal keys become one logical global variable. Conflicting types, units, persistence policies, defaults, or incompatible capabilities are context-definition errors. The context remains target-neutral and may be deployed repeatedly.

### Concrete execution instance

An execution instance is an actual place where programs run:

```text
ExecutionInstance
  id                    stable installation identity
  name
  kind                  server | controller
  controllerTemplateId
  controllerTemplateRevision
  connection/device identity
  enabled
  revision
```

The built-in server VM is a concrete instance such as `server`. Every installed controller is a different instance even when several use the same controller template. Do not use `controllerTemplateId` as instance identity.

### Context deployment and physical bindings

A deployment materializes one logical context on one execution instance:

```text
ExecutionContextDeployment
  id
  executionContextId
  executionContextRevision
  executionInstanceId
  physicalPointBindings[]
  compiledPrograms[]
  status/generation
```

Virtual point keys do not need per-instance mappings; compatible declarations allocate or attach to the instance-global virtual-point cell with that key. Physical logical roles require deployment-specific mappings because different controller types expose different physical point IDs.

The same context can have active deployments on a server instance, a KC868 instance, and another controller type. Compilation may produce different target artifacts, but program behavior and virtual-point keys remain portable.

### Instance-global runtime value

Store virtual-point state in the concrete execution instance, outside any individual program VM:

```text
VirtualPointState
  executionInstanceId
  pointKey
  resolvedContract
  typedValue
  quality
  timestamp
  writerDeploymentId
  writerFlowId
  version
```

Runtime identity is `(executionInstanceId, pointKey)`. A store keyed only by `pointKey`, flow ID, context ID, or controller template ID is invalid. Every program VM hosted by the same execution instance receives access to the same host-owned store.

Suggested API routes:

```text
GET/POST /api/execution-contexts
GET/PUT  /api/execution-contexts/{contextId}
GET/POST /api/execution-instances
GET/PUT  /api/execution-instances/{instanceId}
GET/POST /api/execution-contexts/{contextId}/deployments
GET      /api/execution-instances/{instanceId}/virtual-points
GET      /api/execution-instances/{instanceId}/virtual-points/{pointKey}/runtime
```

## Flow ownership and deployment

Do not add a concrete target ID to portable flow source. Associate immutable flow revisions with a logical execution context, then create one deployment record per target execution instance.

Compilation and deployment must:

1. Load the immutable execution-context definition and all selected flow revisions.
2. Merge virtual-point declarations by key and reject incompatible contracts.
3. Load the concrete execution instance and resolve its controller template, capabilities, and limits.
4. Resolve deployment-specific physical point bindings.
5. Validate virtual-point capacity, type support, persistence support, and defaults.
6. Compile each program for the instance while preserving its stable virtual-point keys.
7. Detect writer conflicts against every active program on the execution instance, not only programs in the deployment currently being prepared.
8. Include context, instance, template, flow, physical-binding, and point-contract revisions in artifact provenance.
9. Prepare all programs and virtual-point allocations before atomically activating the context deployment.

Deployment must fail if any target mapping is missing, the controller type lacks a required capability, point contracts conflict with an existing instance-global cell, or another active program owns the requested writer role. A failed multi-program deployment leaves the previous complete generation running.

## Runtime semantics

### Scan visibility

Use deterministic commit semantics:

1. At scan start, each program VM obtains an immutable snapshot of the instance-global virtual points it reads.
2. The host takes that snapshot under synchronization and records the relevant point versions.
3. The program executes only against private working state and calculates proposed output commands without mutating shared memory.
4. After successful execution, the host validates ownership and commits the program's complete output set atomically under synchronization.
5. Readers observe only complete committed values on a later scan; they never observe partial output sets or in-progress program state.

This prevents flow scheduling order from changing results. If same-scan propagation is later required, it needs an explicit scheduler and a context-wide dependency graph with cycle detection.

### Thread synchronization and shared memory

The shared virtual-point store is host-owned and may be accessed by multiple flow schedulers, API readers, persistence workers, debugger sessions, and device tasks concurrently. All implementations must provide equivalent synchronization semantics.

- Never expose mutable point cells directly to a program VM.
- Use immutable snapshots for reads and atomic transactions for commits.
- Protect cell allocation, contract reconciliation, ownership changes, value commits, reset, and retained-state restoration with synchronization.
- A concurrent dictionary alone is insufficient because a scan may commit several points as one transaction and ownership must be checked with the write.
- On the .NET server, use a dedicated store with a reader/writer lock or short critical section plus monotonically increasing versions. Do not hold locks while executing flow instructions, performing network I/O, publishing events, or writing persistence.
- On controllers, use the platform's mutex/critical-section primitive or a single-owner store task with message passing. Interrupt handlers must not mutate multiword point state directly.
- Analog value, quality, timestamp, writer identity, and version form one atomic logical record; readers must never observe fields from different commits.
- Acquire locks in one documented global order when deployment, scheduler, point store, and persistence locks interact. Prefer copying work out of the lock to avoid deadlocks and priority inversion.
- Versioned compare-and-commit may detect a stale read set when required, but ordinary cross-program communication uses last committed snapshot/next-scan visibility rather than optimistic retries inside the VM.
- Publish telemetry and persist retained values after the in-memory commit. Include the committed version so asynchronous consumers can discard stale work.
- Thread sanitizers, concurrency stress tests, and forced scheduler interleavings are required for both managed and controller hosts.

### Writer ownership

For the initial release:

- Many readers are allowed.
- One active deployed flow program may write a given virtual point on an execution instance.
- A flow may contain only one output node for that point.
- Deployment reports the existing writer when a conflict occurs.
- Undeploying or disabling the writer releases ownership.

Do not silently use last-writer-wins behavior.

### Initial values and persistence

- A virtual point without a committed value uses its typed `relinquishDefault` when defined.
- Without a default, reads return `uninitialized`/unavailable quality rather than an invented zero or false value.
- `volatile` state is cleared when that execution instance restarts.
- `retained` state is persisted with `(executionInstanceId, pointKey)` and restored only when its stored type and resolved contract remain compatible.
- Disabling or deleting a point follows an explicit policy for clearing retained state.

### Server VM

Replace the current server behavior that always returns `not_initialized` for virtual points. Introduce an instance-scoped, thread-safe virtual-point state service shared by every deployed program VM on that server instance. Publishing a non-interface output must commit through this service when the resolved point is virtual; bound outputs continue through their appropriate adapter/driver.

### Controller VM

Define equivalent synchronized read-snapshot, propose, atomic-commit, persistence, and ownership semantics in the controller runtime protocol. A controller reports support for virtual points only if it implements these semantics across all concurrently scheduled programs. Include instance/context/deployment identity in commands so commands cannot be applied to the wrong controller instance.

## Flow designer UX

### Portable authoring and context selection

The flow designer edits a portable program. It may associate the flow with one or more logical execution contexts for validation and deployment, but it must not write a concrete controller/server instance ID into the flow graph.

Suggested behavior:

- Show the flow's declared virtual-point contracts while authoring.
- Allow the user to choose a logical execution context as a validation preview without making that context the program's only deployment target.
- Manage concrete execution-instance selection and physical-point mapping in a separate context-deployment screen.
- Show compatibility results for every intended execution instance/controller type.
- Changing the preview context or deployment target revalidates every point node and physical mapping.
- Do not silently replace point keys or physical mappings.

### Point lookup with manual entry

Replace the plain `pointId` text field in Analog/Digital Input/Output properties with a searchable combobox:

- Search by point key/ID and display name.
- Show ID, name, physical/virtual status, type, units, and readable/commandable capability.
- Filter suggestions using node requirements:
  - Analog Input: analog + readable.
  - Analog Output: analog + commandable.
  - Digital Input: digital + readable.
  - Digital Output: digital + commandable.
- Prefer compatible points, but allow users to reveal incompatible points with an explanation.
- Allow arbitrary manual key/ID entry for expert workflows and paste operations.
- Debounce remote lookup and support keyboard selection.
- Preserve the typed draft while validation is pending or fails.

For virtual points, lookup searches the flow's declarations and the selected logical context's merged contracts. For physical points, authoring selects a logical role; each execution-instance deployment must resolve that role to a compatible concrete point.

### Manual-ID validation

When a user enters an ID manually:

1. Validate identifier syntax locally.
2. Resolve a virtual key against the program/context declarations, or a physical role against the selected deployment instance.
3. Show a pending state while resolving.
4. Show an inline error if it does not exist.
5. Show a specific error for wrong type, missing read/write capability, disabled point, or incompatible units.
6. Prevent save and deployment while any point reference is invalid or unresolved.
7. Revalidate on context change, target-instance change, catalogue refresh, save, and deployment.

Do not turn a network failure into “point does not exist.” Report unavailable validation separately and retry.

### Creating a virtual point from the node editor

Add an optional “Create virtual point” action when a typed ID does not exist:

- Preselect analog/digital from the node kind.
- Default to readable and commandable so another flow can consume or produce it.
- Ask for display name, units for analog, persistence, and optional default.
- Add its declaration to the flow program and merge it into every containing logical execution context.
- Select the newly created point after successful creation.
- Require an explicit confirmation if the user lacks point-management permission or if creation affects a live controller.

This is an enhancement after lookup and validation are working; it must not be required for the first delivery.

## API and validation changes

1. Add execution-context CRUD and catalogue endpoints.
2. Separate portable program declarations, logical context contracts, instance physical catalogues, and instance runtime-value APIs.
3. Add a lightweight point-resolution endpoint suitable for manual-ID validation.
4. Return stable machine-readable validation codes in addition to messages.
5. Extend flow save validation to check syntax and known local shape, while deployment performs authoritative context/revision validation.
6. Return cross-flow writer conflicts with execution-instance ID, point key, and conflicting flow/deployment ID.
7. Enforce authorization independently for viewing contexts, editing points, commanding points, and deploying flows.
8. Audit virtual-point creation, definition changes, manual commands, deployment ownership changes, and retained-value clearing.

Suggested resolution response:

```json
{
  "executionContextId": "climate-programs",
  "executionInstanceId": "controller-east-instance",
  "pointKey": "temp-setpoint",
  "exists": true,
  "implementation": "virtual",
  "valueType": "analog",
  "readable": true,
  "commandable": true,
  "units": "°C",
  "revision": 4
}
```

## Clean-slate transition

1. Create the built-in `server` execution instance.
2. Create an execution instance for each installed controller.
3. Author new flows using point nodes and portable virtual-point declarations.
4. Create logical execution contexts and deployments from the new definitions.
5. Do not import, diagnose, or convert Flow Input/Output nodes. Existing databases and saved flows are intentionally discarded.

## Delivery phases

Implementation status last updated: 21 August 2026.

| Phase | Status | Current result |
| --- | --- | --- |
| Phase 1 | Complete | Portable declarations, execution contexts and instances, deployment records, persistence, migration support, and the built-in server instance are implemented. |
| Phase 2 | Complete | Instance-scoped synchronized state, atomic commits, defaults/uninitialized quality, durable retained restoration, server VM routing, inspection APIs, writer ownership/release, volatile reset, isolation, and concurrency coverage are implemented. |
| Phase 3 | Complete | Active deployment revalidates revisions, capabilities, bindings, contracts, and writer ownership, then compiles every context program for the concrete instance and persists immutable context/instance/template/flow artifact provenance. |
| Phase 4 | Complete | The designer supports context-preview selection, searchable flow/context/physical points, debounced authoritative existence checks, distinct unavailable diagnostics, save/deploy blocking, and creation of typed virtual declarations. |
| Phase 5 | In progress | The controller VM now supports correctly typed numeric Memory state and typed retained-state import/export, and all host tests pass. Multi-program instance-global storage, protocol identity/versioning, and ownership negotiation remain. |
| Phase 6 | Complete | Flow Input/Output node kinds have been removed from authoring and are rejected by backend validation. Virtual points are the sole cross-flow communication mechanism. |

Status labels describe implementation progress; a phase is complete only after its exit criteria and applicable tests pass.

### Phase 1 — Domain and persistence

Status: **Complete**.

- Add portable flow declarations, logical context contracts, concrete execution instances, and context-deployment records.
- Add the built-in server execution instance.
- Add controller-instance management separate from controller templates.
- Add composite uniqueness and revision rules.
- Add migration tooling and diagnostics for existing points.

Exit criteria: one context can be deployed to multiple heterogeneous instances, and each instance gets an isolated `temp-setpoint` runtime cell.

### Phase 2 — Runtime virtual-point state

Status: **Complete**. Volatile and retained instance-scoped state, synchronized immutable snapshots, atomic command validation/commit, default and uninitialized behavior, durable restoration, server VM routing, writer ownership/release, volatile reset, runtime inspection, isolation, and concurrency stress coverage are implemented.

- Implement execution-instance-scoped volatile state.
- Implement synchronized snapshots and atomic multi-point commits shared by all program VMs on an instance.
- Implement atomic scan commit and next-scan visibility.
- Implement uninitialized/default behavior.
- Implement retained persistence and restoration.
- Route server VM reads and writes through the virtual-point store.
- Implement writer ownership and release.

Exit criteria: two server-VM programs on the same instance exchange analog and digital values through one shared key, while the same key on another instance remains isolated under concurrency stress.

### Phase 3 — Compiler and deployment enforcement

Status: **Complete**. Portable declarations reach executable flow sources; active deployment revalidates immutable program/context/instance/template revisions, enabled instances, exact physical bindings, merged contracts, and writer conflicts. It resolves each program for the concrete target, compiles the complete context before persistence, and records the artifact together with context, instance, template, and flow provenance.

- Compile a logical context separately for each target execution instance and its template.
- Merge portable declarations and resolve deployment-specific physical bindings.
- Validate type, capability, units, enabled state, and writer conflicts.
- Include context identity in compiled artifact provenance.
- Revalidate immediately before activation.

Exit criteria: invalid or stale mappings cannot be deployed, and deployment diagnostics identify the exact context and point.

### Phase 4 — Designer lookup and validation

Status: **Complete**. The designer provides a searchable editable point selector across flow declarations, selected context contracts, and physical points. Manual IDs receive debounced authoritative existence and compatibility validation; context changes revalidate the graph; unavailable lookup is distinct from a missing point; invalid references block save and deployment; and missing IDs can be added as typed virtual declarations.

- Add execution-context selection/display.
- Implement the searchable, editable point combobox.
- Filter and annotate compatible physical and virtual points.
- Implement debounced manual-ID validation and inline errors.
- Block save/deploy for invalid references.
- Add the optional create-virtual-point workflow.

Exit criteria: ordinary users do not need to know point IDs, expert users can type them, and nonexistent IDs are never silently accepted.

### Phase 5 — Controller runtime support

Status: **In progress**. The compiler now encodes Memory transient/state slots as numeric, the controller VM executes them correctly, typed retained-state images can be exported and restored, and the controller host suite passes. Multi-program instance-global storage, atomic shared-point transactions, deployment/device identity in the protocol, and firmware capability negotiation remain.

- Version the controller protocol for virtual-point definitions and state.
- Implement volatile/retained storage on supported controllers.
- Implement atomic command commit and ownership rules.
- Confirm controller instance identity during deployment and command exchange.
- Add capability negotiation and diagnostics for unsupported firmware.

Exit criteria: cross-flow communication behaves the same on a supported controller VM as on the server VM.

### Phase 6 — Flow-interface decision and cleanup

Status: **Complete**. Flow Input/Output node kinds are no longer valid. The frontend schema does not expose or serialize them and backend validation/compiler boundaries do not accept them. No migration path is provided. Virtual points are the only mechanism for cross-flow communication.

- Remove the interface editor and node kinds from authoring.
- Reject unsupported nodes in backend validation and before server/controller artifact compilation.
- Do not provide data migration or compatibility behavior.

Exit criteria: there is one unambiguous mechanism for cross-flow communication, with no duplicate configuration UI.

## Testing strategy

### Current verification status

- Backend unit tests: **250 passed, 0 failed**.
- Frontend unit tests: **183 passed, 0 failed**.
- Frontend production build, lint, formatting, and diff checks: **passed**.
- Playwright Chromium, Edge, and mobile Chromium projects pass. Firefox is currently blocked before page creation by the upstream Playwright-on-elevated-Windows `_page` startup defect; it does not reach application assertions.
- Real .NET-backed end-to-end tests: **3 passed, 0 failed**.
- Controller host tests: **17 passed, 0 failed**.

### Domain and API tests

- The same virtual-point key is allowed in different execution instances and produces isolated state.
- Repeated compatible keys across programs on one instance unify into one contract and cell.
- Incompatible declarations for the same key are rejected before deployment.
- Virtual analog/digital definitions validate correctly.
- Virtual points reject source mappings and non-value directions.
- Context-qualified lookup cannot return a point from another context.
- Authorization and audit records cover point and context mutations.

### Compiler and deployment tests

- Each I/O node resolves only compatible points from its context.
- Free-typed missing IDs produce a specific diagnostic.
- Analog/digital and units mismatches are rejected.
- Disabled and stale-revision points are rejected.
- Two active writers on one execution instance are rejected.
- The same writer key on different execution instances does not conflict.
- Context requirements/instance-template mismatch is rejected.

### Runtime tests

- Flow A writes and Flow B reads the committed value on the next scan.
- Analog and digital values preserve their types and quality.
- Uninitialized points report unavailable unless a default exists.
- Volatile values reset only with their owning context.
- Retained values restore only in their owning context.
- Failed flow execution does not partially commit outputs.
- Disabling or undeploying a writer releases ownership.
- Server and controller execution instances with the same point key remain isolated.
- Concurrent scans never observe torn records or partially committed point sets.
- Deployment, reset, retained restore, and scan commit interleavings remain deadlock-free.

### Frontend tests

- Lookup filters by node type and read/write capability.
- Physical and virtual options are clearly labelled.
- Keyboard search and selection work.
- Manual IDs validate after debounce and on blur/save.
- Missing IDs and lookup-service failures show different errors.
- Context changes revalidate mappings without silently replacing them.
- Save/deploy controls are disabled for invalid mappings.
- Creating a virtual point selects it after success.

### End-to-end acceptance scenario

1. Create controller execution instances `controller-east` and `controller-west` from the same template.
2. Create one logical context containing writer and reader flow programs that both declare the retained analog key `temp-setpoint`.
3. Deploy that context to both controller instances.
4. Verify the reader sees the writer's value after commit.
5. Verify each controller has an independent `temp-setpoint` value and that writes on one do not affect the other.
6. Restart `controller-east` and verify retained state restoration.
7. Attempt a second writer deployment and verify it is rejected with a clear conflict.
8. Enter an unknown point ID in the designer and verify save/deploy is blocked with an inline validation error.

## Operational and safety considerations

- Put limits on virtual points per context and retained storage size.
- Treat point commands as privileged operations.
- Preserve data quality and timestamps, not only raw values.
- Define behavior for controller disconnect/reconnect and stale values.
- Prevent a deployment intended for one controller instance from being accepted by another instance using the same template.
- Use optimistic concurrency on execution contexts, point definitions, and deployments.
- Provide observability for current value, quality, writer, last update, persistence state, and readers.
- Avoid logging sensitive physical mappings or credentials with point values.
- Provide backup/restore semantics for retained virtual-point state separately from configuration backups.

## Open decisions to resolve before implementation

1. Can the same flow revision belong to multiple logical execution contexts simultaneously, and how are context-specific configuration values supplied without changing source?
2. Who creates execution instances and how are controller instances paired with physical devices?
3. Is one-writer ownership sufficient, or is explicit priority/arbitration required?
4. What is the exact scan boundary when multiple independently scheduled flows share a context?
5. Should retained values survive point-definition revision changes when type and units remain compatible?
6. Are manual operator commands another writer, and how do they interact with flow ownership?
7. Are virtual points limited to analog/digital permanently, or should the existing integer, multi-state, and text types become available later?

## Definition of done

The feature is complete when a user can author a target-neutral flow program with analog/digital virtual-point declarations, compose it into a logical execution context, deploy that context to multiple server/controller execution instances of compatible but potentially different types, and safely exchange values between all programs on each instance through synchronized instance-global point keys while identically named points on other instances remain independent.
