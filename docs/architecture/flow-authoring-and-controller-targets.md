# Flow authoring and controller targets

## Purpose

A flow is a persisted, directed graph of automation functions. Users create and
connect nodes in the Vue designer, save a draft through the ASP.NET Core API,
and deploy a validated snapshot to the runtime. Flows may run in this
application or target a physical controller with a smaller feature set.

This document defines the flow and controller-target architecture. The
historical delivery sequence is in the
[archived implementation plan](../archive/points-flows-controller-templates-implementation-plan.md);
point semantics are in the [point model](../reference/point-model.md), and the
browser wire contract is in the [UI flow schema](../reference/ui-flow-schema.md).

## Lifecycle

1. The user creates a draft flow with a stable ID and display metadata.
2. The user may select a logical execution context as an authoring preview. The
   selection validates requirements but does not bind the flow to one target.
3. The designer offers functions, connectors, and declared point contracts.
   Unsupported saved content remains visible with diagnostics so it can be
   repaired; it is never silently deleted.
4. Saving validates the persisted graph and records only durable authoring
   state. Selection, viewport, pointer state, validation presentation, and live
   runtime telemetry are not persisted in the graph.
5. A logical execution context selects immutable flow revisions and merges
   their point requirements.
6. Deploying resolves the context, physical bindings, concrete execution
   instance, controller template, and other dependencies as one consistent
   snapshot. Missing, stale, incompatible, disabled, or unsupported resources
   produce structured diagnostics.
7. A successful deployment records exact flow, context, instance, point,
   binding, template, compiler, and artifact revisions. Later edits do not
   mutate a running deployment; redeployment is explicit.
8. The backend compiles the resolved snapshot to canonical Flow IL. The server
   and hardware controllers load it through equivalent portable VM hosts.
9. Disabling, stopping, undeploying, or deleting a flow shuts down only that
   flow and releases non-retained point commands belonging to its stable source
   ID.

The backend persists flow definitions and compiles resolved graphs to the single
current scheduled Flow IL version. Pre-release code rejects non-current versions
and does not translate, migrate, or execute them.

## Persisted graph

A flow contains:

```text
id, name, description
status                    draft | deployed
disabled
updatedAt
controllerTemplateId      defaults to "default" when absent
nodes[]
connections[]
```

Each node has a stable ID, canonical kind, label, finite canvas coordinates and
z-order, connectors, and scalar configuration. Each connector has a stable ID,
label, direction (`input` or `output`), data type, and side. Each connection
references an existing output connector and existing input connector.

Current primitive connector data types are `any`, `boolean`, `event`, `number`,
and `string`. Except for the explicit `any` wildcard, endpoint primitive
types must match. Domain rules can be stricter: analog and integer both use
`number`, for example, but point contracts must still prevent accidental
analog/integer or engineering-unit mismatches.

Node configuration is intentionally restricted to JSON scalar values. New
structured requirements should receive an explicit versioned contract rather
than placing arbitrary browser objects in persisted state. Node kinds are wire
values and the C# validator, TypeScript enum, node registry, icons, fixtures,
and controller-template function vocabulary must remain aligned.

Unknown or invalid graph content must fail at API boundaries with a useful data
path. Previously released kinds remain loadable. Migrations are explicit and
fixture-tested; server startup must not rewrite saved flows.

## Point nodes

Points are the boundary between flow logic and virtual values, hardware, or
external systems. The planned point-related functions are:

- `read-point`: emits the value of a readable point;
- `write-point`: submits a command to a commandable point;
- `point-changed`: triggers from a value/quality change once event runtime
  support exists;
- `release-point-command`: relinquishes this flow's command; and
- `level-shifter`: explicitly maps digital to analog or analog to digital with
  mandatory hysteresis in the latter direction.

Point nodes persist `pointId`, `expectedValueType`, and optional
`expectedUnits`, not copied names or live values. A missing or changed point
therefore remains identifiable as an invalid draft node. It cannot deploy until
repaired. Live values, quality, commands, and history belong to their runtime
services and never to the flow JSON.

Bad or missing input never silently becomes zero, false, or empty text. Quality,
fallback, and inhibition are explicit runtime behaviour. Multiple writers use
command arbitration rather than last-write-wins.

## Point sources and live values

A point source is a reusable external connection. Initial source kinds are Home
Assistant, MQTT, and HTTP/JSON. The source owns server or broker location, TLS,
timeouts, reconnect behaviour, and a credential reference. It never contains a
literal secret. One source can serve many standalone points, multiple groups,
and all members of those groups.

A bound point maps to a source-relative entity, topic, JSON selector, or device
address. A group can select a source and shared mapping defaults so members can
share a subscription, poll, or atomic/batched operation. A point may inherit
the group source or select one explicitly when group rules allow it. Deleting
or changing a source is blocked while incompatible point/group references
remain.

Point sources are implemented before bound-point authoring. While editing an
unsaved or saved source, the user can test connectivity in real time. The test
reports cancellable DNS, TCP, TLS, authentication, and protocol stages with
latency and redacted diagnostics. It is strictly read-only: it cannot publish
MQTT, call a Home Assistant service, command a point, or issue mutating HTTP
requests. Tests have bounded time, response size, redirect count, and
concurrency, and HTTP destinations are subject to an explicit outbound-network
policy to prevent SSRF.

After a point is saved and mapped, its definition screen shows the actual typed
present value with units, quality/reliability, source timestamp, age, source
connection state, and whether it is live, cached, simulated, or unavailable.
The screen subscribes where possible and otherwise polls with cancellation. A
failure or stale timestamp changes the displayed status; the last known value
must never continue to look current.

Point configuration and live state stay separate. Editing YAML cannot overwrite
a runtime value, and a connectivity test result is not a point sample.

## Controller templates

A controller template declares what a target can represent and execute. It is a
capability contract, not a controller instance, connection credential, driver,
or deployed-flow snapshot. Templates allow a simple physical controller to
exclude features such as overrides, certain point types, flow functions, event
execution, or quality propagation.

The built-in `default` template targets this application. It is embedded,
read-only, always available, and exhaustive for the features implemented by the
current release. Users can view its YAML as a canonical example but cannot edit,
replace, or delete it.

Custom templates are initially authored as YAML rather than through a graphical
form. The backend is authoritative: it parses YAML into a typed, versioned
model, validates syntax and semantics, and stores valid files atomically. The
frontend may parse enough to improve editing feedback but cannot be the only
validation layer.

Version 1 declares:

- identity, description, schema version, and revision;
- supported point value types and directions;
- point features such as read, command, retention, override, relinquish,
  quality, alarms, and trends;
- connector data types and canonical flow-function kinds;
- event and interval execution modes;
- runtime features such as virtual/bound points, command arbitration, and
  quality propagation; and
- optional limits for flow, node, connection, and minimum interval capacity.

The initial built-in template is conceptually the following YAML. Its function
list covers the current catalogue and the point/runtime functions introduced by
the combined implementation plan; code must update this list and its parity
test whenever another function becomes supported.

```yaml
schemaVersion: 1
id: default
name: Flow Control Automation
description: Built-in unrestricted application target
readOnly: true
capabilities:
  pointTypes: [analog, digital, multi_state, integer, text]
  pointDirections: [input, output, input_output, value]
  pointFeatures:
    [read, command, retain, override, relinquish, quality, alarms, trends]
  connectorDataTypes: [any, boolean, event, number, string]
  flowFunctions:
    - and
    - average
    - calculator
    - calendar
    - clamp
    - comparator
    - delay
    - if
    - invert
    - level-shifter
    - line
    - max
    - min
    - nand
    - nor
    - not
    - or
    - override
    - point-changed
    - pulse
    - read-point
    - release-point-command
    - schedule
    - selector
    - sequence
    - split
    - timer
    - write-point
    - xnor
    - xor
  executionModes: [event, interval]
  runtimeFeatures:
    [virtual_points, bound_points, command_arbitration, quality_propagation]
limits:
  maxFlows: null
  maxNodesPerFlow: null
  maxConnectionsPerFlow: null
  minimumIntervalMilliseconds: null
```

`not` is the sole canonical NOT-gate name; obsolete aliases are rejected. A
production default should be generated or checked against the
canonical registries so documentation cannot make an unsupported function
deployable.

Capabilities are allowlists: absence means unsupported. Names are canonical
case-sensitive wire values. Unknown names and duplicate entries are errors,
because accepting them would make a misspelling look like a supported feature.
Limits are either explicit valid values or unlimited; zero and null must not
acquire ambiguous meanings.

YAML handling must be bounded and deterministic. Reject unsupported schema
versions, duplicate mapping keys, custom tags, unsafe aliases, excessive size
or nesting, mismatched file/declared IDs, reserved `default` identity, path
traversal, and non-finite numeric values. Syntax errors include line and column;
semantic errors include stable field paths. API errors do not reveal host paths
or secrets.

Custom-template updates use optimistic revisions and atomic replacement.
Deleting a referenced template is blocked and reports affected execution
instances and deployments. A capability-reducing edit reports affected
resources and requires an explicit
conflict-resolution path. No edit changes an already-running deployment.

## Authoring behaviour

The context-preview selector is part of flow settings and is fully keyboard
accessible. Changing it recomputes diagnostics without changing the portable
graph. Concrete target selection and physical mapping belong to the deployment
screen. The palette and point selectors may filter unsupported choices to
reduce mistakes, but the UI must also explain why an item is unavailable.
Colour is never the only indicator.

Draft editing remains lossless:

- switching preview contexts does not delete nodes or connections;
- opening a flow whose preview context is missing preserves its configured ID;
- opening a graph with a now-unsupported function renders the function and an
  actionable diagnostic;
- point definition drift is shown at the affected node; and
- unsupported graphs may be saved as drafts when structurally valid, but
  cannot be deployed.

All designer controls use semantic HTML where HTML is appropriate. The SVG
canvas exposes accessible names and keyboard equivalents for pointer actions.
Forms use labels, fieldsets/legends, associated error text, status announcements,
visible focus, focus trapping/restoration for dialogs, and WCAG 2.2 AA contrast
in every theme and viewport. The YAML editor is a labelled native text area
with a validation summary and navigable line/field diagnostics.

## Validation boundaries

Validation is deliberately repeated at trust boundaries while sharing one
canonical rule vocabulary:

- YAML load/write validates template syntax, schema, semantics, and identity.
- Flow DTO parsing validates shape, enums, finite values, unique IDs, endpoint
  existence, direction, and primitive connector compatibility.
- Draft save validates graph integrity and declarations and reports preview
  incompatibility without destroying repairable content.
- Deployment resolves one consistent context, instance, template, physical
  binding, and point snapshot, then validates all function kinds, connectors,
  point contracts, execution mode, runtime features, and target limits.
- Runtime construction checks the captured deployment contract defensively and
  refuses unsupported work.

Diagnostics have stable codes, human messages, severity, and locations such as
node ID, connector ID, point ID, or template field path. The frontend links a
diagnostic to the relevant editor control or canvas element.

The application must never infer that a physical target supports a feature
merely because the default target does. It must never silently coerce a graph,
drop a node, substitute a point, lower an interval, or ignore an unsupported
feature to make deployment succeed.

## Compilation and execution model

All hosts use the strict PLC Scan Cycle defined in
the [PLC scan cycle](plc-scan-cycle.md): Read Inputs, Execute Logic, and Write Outputs. The input image and current
state are immutable during Execute Logic; state and proposed commands remain
private until the atomic Write Outputs boundary.

Editable graph topology is a compiler input, not a target runtime format. The
backend resolves one immutable context/flow/point/instance/template snapshot, validates types,
units and capabilities, rejects combinational cycles, applies deterministic
Kahn scheduling, allocates typed slots/state, and emits canonical Flow IL plus
stable symbols. Browser validation is advisory and targets never accept the
designer DTO.

The backend also owns Flow IL decompilation for artifact recovery and designer
import; targets and the VM do not decompile. The decompiler accepts only the
single current IL version, validates it without executing it, and emits a
current, valid designer DTO. Artifacts recover stable executable IDs/configuration and a deterministic
semantically equivalent graph, layout, provenance, and explicit warnings for
non-runtime authoring details. Lossless labels/groups/layout recovery requires
a future versioned metadata contract. Unsupported versions and unrepresentable behavior fail with structured
diagnostics and is never silently dropped.

Flow IL is a project-specific automation bytecode, not CLR IL. The normative
portable VM implementation is shared by the ASP.NET Core server host, portable
host tests, and controller firmware. The server must not grow an independent
C# evaluator with different node or state semantics. Current Flow IL encodes an
already scheduled instruction stream so targets do not run Kahn
or reconstruct graph topology.

Flow IL also carries an optional bounded debug map. The portable VM debugger can
run on the server, on a controller, or in a server-side controller emulator. It
supports tick, node, and instruction stepping, breakpoints, continue, pause,
run-to, and typed frame inspection subject to negotiated target limits. Pausing
inside a tick exposes only a private debug frame; state and output commands are
published only if execution reaches the atomic tick commit.

The controller emulator uses the same VM and a selected hardware template, but
replaces physical point adapters with deterministic simulated inputs, captured
outputs, virtual time, scripted traces, and fault injection. It is not a second
evaluator and does not replace physical commissioning.

Each deployed flow has an isolated lifecycle and cancellation context. Event
flows wait for subscribed events; interval flows use a validated ticker and do
not overlap executions unless the function contract explicitly supports it.
Stopping is graceful, bounded, and independent of other flows.

VM execution uses typed slots and value/quality envelopes prepared by the
compiler. Cycles require an explicitly supported stateful/delay construct;
accidental combinational cycles are compile errors. Opcodes should be pure where
possible. Stateful opcodes define initialization, next-state, persistence, and
shutdown behaviour. Output commands carry flow source and correlation IDs for
arbitration and audit.

Errors are contained to the affected flow and exposed as runtime diagnostics.
Unhandled exceptions are recovered at the flow boundary, recorded without sensitive data, and
must not terminate unrelated runtimes. Updates build and validate a replacement
before swapping it into service so a failed redeployment leaves the prior
deployment intact.

## API and persistence

Existing flow CRUD and runtime endpoints remain the public boundary described
in the [UI flow schema](../reference/ui-flow-schema.md) and
[runtime API](../reference/runtime-api.md). Durable backend
configuration is stored in EF Core/SQLite through the checked-in migrations.
Point definitions, live point state, controller templates, deployed snapshots,
commands, and audit/history remain separate domains because they have different
consistency and safety needs.

All configuration intended for user editing—point sources, point groups, point
definitions, and controller templates—is represented as validated YAML. The
backend converts it to typed models and persists normalized internal state as
JSON. JSON remains the backend persistence format and may be used for runtime,
list, and diagnostic API payloads, but it is not the user-editable
configuration format. YAML and normalized JSON must round-trip without semantic
loss.

Custom controller templates are edited through YAML and persisted as normalized
JSON under `CONTROLLER_DATA_FILE`. The embedded default is never written to that
store and custom definitions cannot shadow it. Template APIs list/retrieve
metadata, retrieve YAML, validate without saving, create, update, and delete.
Mutations return revision conflicts rather than silently overwriting another
editor.

Cross-resource mutations and deployments go through a service layer capable of
holding consistent snapshots across the flow, context, instance, point, and
template stores. Storage implementations may change without changing the HTTP
contracts.

## Testing and completion standard

Changes require production code, unit/integration tests, focused Playwright
coverage where user behavior changes, and an appropriate smoke test. Tests
cover positive and negative cases, malformed input, target mismatch,
concurrency, atomic-write failure, and recovery. Current virtual-point
acceptance coverage is defined in
[virtual-point verification](../testing/virtual-points.md).

Frontend work must pass formatting, linting, unit tests, production build, and
focused plus full E2E suites. User-visible flows are tested with keyboard-only
operation and automated accessibility scans at desktop/mobile sizes and
light/dark themes. E2E specs are separated by user-facing responsibility and
use role/label locators except where SVG graph geometry requires a direct
selector.

Backend work uses table tests for capability matrices, fixture parity, fuzzing
for parsers/validators, race tests for stores and deployment snapshots, and
fault injection for atomic persistence. Comments explain non-obvious safety,
compatibility, concurrency, or parsing intent; they do not narrate syntax.
