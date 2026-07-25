# Points, flows, and controller templates implementation plan

## 1. Goal

Implement the point and flow models described in `.codex/point-types.md` and
`.codex/flows.md` across the Go backend and Vue frontend. Controller templates
form part of the same implementation because they constrain point definitions,
flow functions, graph connections, deployment, and runtime behaviour. Users
must be able to:

- create, view, edit, and delete point definitions;
- create reusable Home Assistant, MQTT, and HTTP/JSON point sources and test
  their connectivity in real time before mapping points;
- create, view, edit, and delete point groups;
- keep a point standalone or assign it to exactly one visible group;
- add typed point-read and point-write blocks to a flow;
- select only points compatible with a block's direction;
- connect blocks only when their value types are compatible;
- explicitly convert analog and digital values with a level-shifter block;
- save, reload, and deploy valid point-backed flows without breaking existing
  saved flows;
- choose the controller targeted by a flow;
- view the built-in, read-only default controller template;
- create and edit custom controller templates as validated YAML;
- edit all user-facing point, group, source, and controller configuration as
  validated YAML while the backend retains normalized JSON persistence; and
- see precise authoring and deployment diagnostics when a target does not
  support a point type, point capability, connector type, flow function, or
  runtime feature.

The work is divided into independently buildable and committable phases. Every
phase includes its own tests and leaves the application deployable. Backend
contract work precedes frontend consumers, and persisted flow changes remain
backward compatible.

## 2. Scope and delivery boundaries

### Initial usable release (phases 0-9)

The first release covers point and point-group definition management, flow
authoring, deployment validation, and a level shifter. It supports all initial
value types from the point model:

- `analog`
- `digital`
- `multi_state`
- `integer`
- `text`

This release implements reusable Home Assistant, MQTT, and HTTP/JSON source
adapters early enough to test connectivity and read mapped input values. It
does not yet enable arbitrary output writes or field-protocol drivers. Point
blocks are valid graph endpoints, but deployment must report any runtime
limitation explicitly; it must never pretend that an unreadable or disconnected
bound point is live.

### Runtime release (phases 10-13)

The subsequent release adds runtime values, drivers, command arbitration,
quality propagation, commissioning, alarms, trends, and audit history. These
features are separate because point definitions, live state, commands, and
history have different persistence and safety requirements.

### Explicitly deferred

The following should not be smuggled into an earlier phase:

- a production credential store;
- BACnet, Modbus, KNX, and other field-protocol drivers;
- unit conversion beyond exact-unit compatibility;
- user/role authorization;
- alarm notification delivery and long-term trend storage;
- high-availability runtime coordination;
- a graphical controller-template form builder;
- automatic discovery of every physical controller capability; and
- silently reducing or rewriting a flow to fit a smaller controller.

The extension points for these features are defined below, but each needs its
own design and threat/safety review before implementation.

## 3. Architectural decisions

### 3.1 User configuration and internal persistence

Create a new backend package, `backend/internal/points`, rather than extending
the flow store. All user-editable source, group, point, and controller
configuration is exposed and imported/exported as YAML. The backend strictly
parses YAML into typed domain models, validates it, and persists normalized
internal state as JSON. Users never edit the JSON persistence files directly.

Use separate JSON files configured by `POINT_DATA_FILE` and
`POINT_SOURCE_DATA_FILE`, defaulting to `data/points.json` and
`data/point-sources.json`. This preserves the existing `FLOW_DATA_FILE`
contract and prevents live point data from being written into definitions.

Persist one versioned document:

```json
{
  "schemaVersion": 1,
  "revision": 1,
  "groups": [],
  "points": []
}
```

Use atomic temporary-file-and-rename persistence, matching the existing flow
store. Reject duplicate IDs, duplicate case-insensitive names, invalid
references, and unsupported schema versions when loading.

YAML endpoints accept and return canonical user configuration. JSON API
responses may still carry list metadata, runtime envelopes, diagnostics, and
internal DTOs used by the SPA; this does not make JSON a user-editable
configuration format. YAML writes and JSON persistence must round-trip without
semantic loss, and only the backend may add revisions and timestamps.

Do **not** persist a hidden `__StandalonePointGroup__` initially. A nullable
`groupId` is simpler and accurately represents a standalone point. The store
must enforce that `groupId` is either absent or references exactly one group.
The reserved name `__StandalonePointGroup__` must not be available to users so
a hidden implementation group can be introduced by a future migration if a
storage engine requires it.

### 3.2 Definition model

The initial `PointDefinition` contract is:

```text
id, name, description, enabled
groupId?
implementation            virtual | bound
direction                 input | output | input_output | value
valueType                 analog | digital | multi_state | integer | text
units?
stateLabels?
readable, commandable
persistence               volatile | retained
relinquishDefault?
sourceId?
mapping?
limits?
safeDisablePolicy?
revision, createdAt, updatedAt
```

The initial `PointGroup` contract is:

```text
id, name, description
sourceId?
mappingDefaults?
revision, createdAt, updatedAt
```

A group's optional source and mapping defaults support shared subscriptions,
polls, and atomic/batched updates. A point contains its source-relative mapping
and may inherit the group's source. Server/broker connection details live only
in the referenced source. Secrets are credential references, never literal
credentials.

The initial `PointSource` contract is:

```text
id, name, description, enabled
kind                      home_assistant | mqtt | http_json
connection                typed, kind-specific non-secret settings
credentialRef?
tls?
timeouts?
revision, createdAt, updatedAt
```

Home Assistant defines base URL and event/API options; MQTT defines broker,
client, QoS/session, TLS, and reconnect options; HTTP/JSON defines base URL,
allowed read methods, headers without secrets, polling defaults, TLS, redirect,
and response-size policies. Point mappings define the entity, topic and payload
selector, or relative path and JSON selector. A source can be referenced by
multiple points and multiple groups.

Typed defaults and limits must be represented without losing type information.
Go validation and frontend DTO validation must apply the same rules:

- analog values are finite JSON numbers;
- integer values are safe whole JSON numbers;
- digital values are booleans and have exactly two non-empty labels;
- multi-state values use a stable state key and have at least two unique labels;
- text values are strings with an explicit maximum length;
- units apply to numeric values and are normalized identifiers;
- minimum cannot exceed maximum;
- `input` points are readable and not commandable;
- `output` points are commandable, with readability determined by readback;
- `input_output` and virtual `value` points may be both;
- virtual points have no external binding;
- bound points require a valid source either directly or through their group
  and a mapping compatible with that source kind;
- a retained virtual point has a valid `relinquishDefault`; and
- output definitions require explicit startup, shutdown, communication-loss,
  and disable policies before runtime writes are enabled.

Avoid comments that merely narrate code. Add comments where a validation or
safety rule exists for a non-obvious operational reason.

### 3.3 Concurrency and deletion

Return an integer `revision` on definitions and groups. Update and delete
requests send the last observed revision. A stale revision returns
`409 Conflict`, preventing one browser from silently overwriting another.

Group deletion is rejected with `409 Conflict` while it contains points. The UI
must offer an explicit transaction to make those points standalone before
retrying deletion. Point deletion is rejected with `409 Conflict` while any
flow references it. The response includes the referencing flow IDs so the user
can repair them. Do not silently delete graph nodes or cascade-delete points.
Source deletion is rejected while any point or group references it and returns
the referencing IDs. A source-kind change reports all affected mappings and
requires explicit confirmation.

Because points and flows use separate files, cross-store checks must occur under
a service layer that serializes definition mutations. A later database
migration can replace that service without changing HTTP contracts.

### 3.4 HTTP API

Add these JSON endpoints:

```text
GET    /api/points
POST   /api/points
GET    /api/points/{pointId}
PUT    /api/points/{pointId}
DELETE /api/points/{pointId}?revision={revision}

GET    /api/point-groups
POST   /api/point-groups
GET    /api/point-groups/{groupId}
PUT    /api/point-groups/{groupId}
DELETE /api/point-groups/{groupId}?revision={revision}
POST   /api/point-groups/{groupId}/make-points-standalone

GET    /api/point-sources
POST   /api/point-sources
GET    /api/point-sources/{sourceId}
PUT    /api/point-sources/{sourceId}
DELETE /api/point-sources/{sourceId}?revision={revision}
POST   /api/point-sources/test
POST   /api/point-sources/{sourceId}/test

GET    /api/points/{pointId}/runtime
```

Configuration create/update/get operations support canonical YAML media types;
list, test, diagnostics, and runtime endpoints use JSON. The unsaved-source test
accepts candidate YAML without persisting it. Both test endpoints return or
stream structured stages (`dns`, `tcp`, `tls`, `authentication`, `protocol`)
with bounded latency and redacted diagnostics, and support cancellation. Tests
are read-only: no publish, Home Assistant service call, command-topic write, or
mutating HTTP request is permitted.

List endpoints support server-side `filter`, `page`, `pageSize`, and `sort`, as
well as point filters for `groupId`, `implementation`, `direction`,
`valueType`, and `enabled`. Use the existing page response shape. Use `400` for
malformed input, `404` for unknown resources, `409` for revision/reference
conflicts, and `500` for persistence failure. Error responses retain the
existing `{ "message": "..." }` field and may add stable `code` and `details`
fields.

### 3.5 Flow graph representation

Add these persisted node kinds:

- `read-point`
- `write-point`
- `point-changed`
- `release-point-command`
- `level-shifter`

The first flow-authoring milestone exposes `read-point`, `write-point`, and
`level-shifter`. The event and relinquish nodes become visible only when their
runtime support exists.

Point nodes store only stable configuration:

```text
pointId
expectedValueType
expectedUnits?
```

The expected type/units snapshot makes definition drift detectable without
duplicating names or live values into flow JSON. Labels displayed on the canvas
are resolved from the current point catalogue. If a point is missing or its
contract changed, the node remains loadable and editable but is visibly invalid
and cannot be deployed.

Map point types to existing connector types as follows:

| Point value type | Connector data type |
| --- | --- |
| analog | `number` |
| integer | `number` |
| digital | `boolean` |
| multi-state | `string` |
| text | `string` |

`read-point` has an output connector and allows readable `input`,
`input_output`, or `value` points. `write-point` has an input connector and
allows commandable `output`, `input_output`, or `value` points. Direction is
based on capabilities, not merely labels.

Exact value type and unit compatibility is required even where two values map
to the same connector primitive. Thus an integer cannot silently connect to an
analog-only point, and degrees Celsius cannot silently connect to percent.
Existing legacy nodes using `any` remain loadable, but point nodes do not use
`any` to bypass validation.

### 3.6 Level shifter

The level shifter has two explicit modes:

- `digital_to_analog`: configure finite `lowValue` and `highValue`; they must be
  different and within optional output limits.
- `analog_to_digital`: configure finite `offThreshold` and `onThreshold`, with
  `offThreshold < onThreshold`. The gap is mandatory hysteresis. While the
  input lies between thresholds, retain the previous digital output.

The node's connectors change with mode. Persist the mode and values, validate
them in both the browser and backend, and implement a pure runtime evaluator
with explicit initial-state behavior. Do not call this unit conversion: it is a
control-level mapping and does not make incompatible engineering units
compatible.

### 3.7 Frontend structure and accessibility

Add a `points` feature alongside `flows`, with DTO, mapper, API, Pinia store,
views, components, and unit tests. Add primary navigation routes:

```text
/points
/points/new
/points/:pointId
/point-groups
/point-groups/new
/point-groups/:groupId
/point-sources
/point-sources/new
/point-sources/:sourceId
```

Use semantic landmarks, headings, tables, fieldsets, legends, labels, status
messages, and native controls. Dialogs must trap and restore focus. Validation
errors must be programmatically associated with fields. All operations must be
keyboard accessible, meet WCAG 2.2 AA contrast/focus requirements in every
theme, and avoid conveying direction, type, quality, or errors by color alone.

Reuse `AppTable`, pagination, button, modal-focus, theme, and request-cancellation
patterns where appropriate. Use SVG icons in
`public/icons/flow-nodes/`, with `currentColor`/theme-compatible styling and the
same sizing/view-box conventions as existing node icons.

Source, group, and point detail screens use an accessible labelled YAML editor
for configuration. Typed summaries and safe helper controls may accompany it,
but YAML remains the canonical user-editable representation. The point detail
screen separately displays the live runtime envelope: present value, units,
quality/reliability, source timestamp, last-update age, connection state, and
live/cached/simulated/unavailable status. It subscribes when supported and
otherwise polls with cancellation, pauses when hidden, and never presents a
stale value as current.

### 3.8 Controller templates and target binding

A controller template is a versioned capability contract, not a deployed
controller instance. Expose custom templates as user-editable YAML, then persist
their normalized internal state as JSON under `CONTROLLER_DATA_FILE`, defaulting
to `data/controllers.json`. Keep the built-in `default` template embedded in the
application so it is always available, cannot be changed or deleted, and
represents every feature supported by this project.

The version-1 YAML contract must include:

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
  flowFunctions: [] # replaced by the complete catalogue shown in flows.md
  executionModes: [event, interval]
  runtimeFeatures:
    [virtual_points, bound_points, command_arbitration, quality_propagation]
limits:
  maxFlows: null
  maxNodesPerFlow: null
  maxConnectionsPerFlow: null
  minimumIntervalMilliseconds: null
```

The checked-in default example must enumerate `flowFunctions`; the abbreviated
empty list above is descriptive only. Absence of a capability means unsupported.
Unknown capability names, duplicate entries, invalid limits, unsupported schema
versions, a mismatched file/id, and YAML aliases or tags that weaken deterministic
parsing are rejected. Bound parsing size and nesting depth. YAML is converted
into the same typed Go/TypeScript DTO used by validation; application code must
not inspect arbitrary YAML maps.

Custom template writes use YAML parse, schema validation, semantic validation,
normalization, then atomic JSON temporary-file-and-rename persistence. Reject
an ID of `default` and stale revisions. Return line/column information for
syntax errors and stable field paths for semantic errors. Never expose
filesystem paths in API errors.

Add `controllerTemplateId` to each flow, defaulting missing legacy values to
`default` at the API/domain boundary. Drafts may retain unsupported elements so
users can repair them or change target, but unsupported items are visibly
diagnosed. Save validates graph integrity; deploy applies the selected template
to the complete resolved graph and point catalogue. A deployment captures the
template revision and validated flow/point snapshot so a later template edit
cannot mutate running behaviour. Redeployment is required to adopt changes.

The template service owns:

- listing and retrieving the built-in and custom templates;
- validating custom YAML and atomically storing normalized JSON state;
- calculating structured compatibility diagnostics;
- preventing deletion while a flow targets the template;
- reporting affected flow IDs before a capability-reducing edit; and
- keeping frontend filtering and backend deployment validation aligned through
  one canonical capability vocabulary.

Expose JSON metadata plus YAML content through:

```text
GET    /api/controller-templates
POST   /api/controller-templates/validate
POST   /api/controller-templates
GET    /api/controller-templates/{templateId}
PUT    /api/controller-templates/{templateId}
DELETE /api/controller-templates/{templateId}?revision={revision}
GET    /api/controller-templates/{templateId}/yaml
```

The default resource returns `readOnly: true`; mutation attempts return `409`.
The UI provides a semantic template catalogue and an accessible labelled YAML
text editor with validation summary, line/column messages, keyboard operation,
unsaved-change protection, and a read-only default example. Compatibility is
never communicated by colour alone, and filtering the palette must be
supplemented by explanatory text and server-side validation.

## 4. Definition of done for every phase

A phase is complete only when:

1. its production code, unit tests, and named E2E coverage are in the same
   commit;
2. existing persisted data opens without manual editing;
3. failed persistence leaves the previous in-memory and on-disk state intact;
4. malformed API and persisted input fail with useful, non-sensitive errors;
5. new UI is keyboard usable and passes the relevant accessibility scan;
6. comments explain intent or safety constraints, not syntax;
7. formatting, lint, type checking, and builds pass; and
8. the manual smoke test listed for the phase passes.

Run the following full quality gate before every phase commit:

```sh
(cd backend && gofmt -w <changed-go-files> && go test ./...)
(cd frontend/flow-control-ui && npm run format)
(cd frontend/flow-control-ui && npm run lint)
(cd frontend/flow-control-ui && npm run test:unit -- --run)
(cd frontend/flow-control-ui && npm run build)
```

Run the phase's targeted Playwright spec during development and
`npm run test:e2e` before merging the phase. Formatting commands may update
files; inspect the diff before committing.

## 5. Implementation phases

### Phase 0 - Contract fixtures and compatibility baseline

**Purpose:** Freeze current behavior and create shared examples before adding
new persisted concepts.

**Implementation**

- Add canonical version-1 point/group/source YAML fixtures plus normalized
  internal JSON fixtures covering every value and source type,
  standalone/grouped points, virtual/bound implementations, and invalid cases.
- Add version-1 controller-template YAML fixtures for the exhaustive built-in
  target, constrained physical targets, and syntax/semantic failures.
- Add legacy flow fixtures that contain only current node kinds.
- Document canonical enum spellings and the point-to-connector mapping.
- Add test helpers for temporary backend data files and Playwright point API
  seeding.
- Record baseline accessibility and full-suite results.

**Unit/integration tests**

- Existing flow fixtures decode, validate, save, and reload unchanged.
- YAML configuration and normalized JSON fixtures agree between Go decoding and
  TypeScript DTO parsing without semantic loss.
- Template fixtures produce equivalent typed capabilities and diagnostics in
  Go and TypeScript.
- Unknown fields and unsupported schema versions fail.

**E2E/smoke**

- Existing flow list and designer CRUD journeys still pass with no point file.

**Commit gate:** No user-visible behavior changes; all existing tests and builds
pass.

**Suggested commit:** `test: establish points contract compatibility baseline`

### Phase 1 - Point-source foundation and live connectivity testing

**Purpose:** Define and verify reusable external systems before bound points or
groups can map values from them.

**Implementation**

- Add typed Home Assistant, MQTT, and HTTP/JSON source contracts, canonical YAML
  parsing/rendering, normalized JSON persistence, revisions, CRUD service/API,
  and reference-safe deletion.
- Implement read-only connectivity adapters with staged, cancellable, bounded
  DNS/TCP/TLS/authentication/protocol checks and redacted diagnostics.
- Enforce credential references, TLS policy, redirect/outbound-network policy,
  SSRF protection, unique short-lived MQTT test clients, response-size limits,
  rate limits, and audit events without secrets.
- Define a credential-resolver interface and an initial deployment-secret/
  environment reference implementation; building a general credential-management
  UI remains deferred.
- Add accessible `/point-sources` catalogue and YAML create/detail/edit screens
  with a live Test connection action, progress/status announcements, focusable
  error summary, retry/cancel, dirty-navigation protection, and last-test result
  kept as transient UI state.

**Unit/integration tests**

- YAML/typed/JSON semantic round trips for each source kind.
- Validation for URLs, broker schemes, TLS, timeouts, credentials, headers,
  redirects, unknown fields, duplicate YAML keys, tags/aliases, size/depth, and
  stale revisions.
- Deterministic connectivity tests for success, DNS/TCP/TLS/auth/protocol
  failures, cancellation, timeout, oversized response, forbidden address and
  redirect, MQTT cleanup, and redaction.
- Atomic store rollback, restart, concurrent tests, rate limiting, and deletion
  reference conflicts.

**E2E/smoke**

- Add `e2e/pointSources.spec.ts`: create unsaved YAML for each kind, run mocked
  staged tests, fix a failure, save/reload/edit/delete, cancel a slow test, and
  verify keyboard-only operation and axe scans at desktop/mobile sizes and
  light/dark themes.

**Commit gate:** A user can define and safely test each initial source without
creating a point; connectivity testing never mutates the external system or
persists credentials/test results.

**Suggested commit:** `feat(points): add reusable point sources and connectivity tests`

### Phase 2 - Backend point and group domain model

**Purpose:** Introduce validated domain types without HTTP or UI changes.

**Implementation**

- Create `backend/internal/points/model.go`.
- Define enums and typed validation for definitions, groups, labels, limits,
  defaults, capabilities, safe policies, source references, and source-specific
  point/group mappings.
- Centralize compatibility and capability predicates so HTTP and flow
  validation do not invent separate rules.
- Reserve the hidden-group name.

**Unit tests**

- Table-test every valid value type/direction/implementation combination.
- Cover NaN/infinity, integer precision, ranges, labels, defaults, units,
  capabilities, missing/conflicting sources, invalid mappings, credential
  literals, unsafe output policies, whitespace, duplicate names, and reserved
  names.
- Fuzz YAML and JSON decode and validation; they must never panic.

**E2E/smoke**

- Not applicable at the UI level; run the existing E2E suite unchanged.

**Commit gate:** Package is unused by production routing, backend tests pass,
and the existing application builds/runs unchanged.

**Suggested commit:** `feat(points): add validated point definition model`

### Phase 2A - Controller-template domain and built-in default

**Purpose:** Establish the capability vocabulary before API, UI, or deployment
layers depend on it.

**Implementation**

- Add `backend/internal/controllers` typed capability, limit, and diagnostic
  models plus strict bounded YAML parsing.
- Embed the exhaustive, read-only default template and derive/check its
  flow-function entries against the canonical backend node registry.
- Centralize predicates for point, connector, function, execution-mode, and
  runtime-feature support.

**Tests**

- Table-test valid constrained targets and every invalid enum, duplicate,
  limit, ID, alias/tag, size, and nesting case.
- Fuzz YAML parsing and semantic validation; it must never panic.
- Assert the default includes every supported point type, feature, connector,
  execution mode, runtime feature, and flow function.

**Commit gate:** No route or persisted flow changes; all existing suites pass.

**Suggested commit:** `feat(controllers): define validated capability templates`

### Phase 3 - Durable backend point and group store

**Purpose:** Persist point/group definitions atomically.

**Implementation**

- Add the versioned document and `Store` with list/get/create/update/delete.
- Enforce unique IDs/names, one-or-zero group membership, revisions, referential
  integrity, valid source inheritance/references, and deterministic persistence
  order.
- Add the explicit make-standalone transaction.
- Wire `POINT_DATA_FILE` into server startup, but expose no routes yet.

**Unit/integration tests**

- Empty/missing file startup; round-trip every type.
- Atomic create/update/delete and rollback after injected write/rename failure.
- Concurrent operations under `go test -race ./...`.
- Stale revision conflicts, group-in-use conflicts, duplicate names/IDs,
  orphaned group/source references, corrupt JSON, and unsupported versions.
- Make-standalone updates all members or none.

**E2E/smoke**

- Start the server with an absent point file; health and existing flow APIs
  remain available. Restart and confirm saved point fixtures reload.

**Commit gate:** Server startup remains backward compatible and no point
endpoint is public yet.

**Suggested commit:** `feat(points): persist point and group definitions`

### Phase 4 - Point and group HTTP and YAML API

**Purpose:** Make definition management available to clients.

**Implementation**

- Add point/group service and handlers with bounded canonical YAML
  create/update/get bodies plus JSON lists, diagnostics, and runtime envelopes.
- Add paging/filter/sort, stable error codes, revision conflicts, and reference
  details.
- Extend the handler constructor to receive both stores explicitly.
- Preserve all existing flow paths and response contracts.

**Unit/integration tests**

- Handler tests for success, all validation failures, unknown IDs, malformed
  query/YAML values, duplicate keys, trailing documents, oversized requests,
  persistence failures, stale revisions, membership/source conflicts, and
  content types.
- Pagination/filter/sort determinism.
- API-created YAML survives normalized JSON store reopen and returns
  semantically equivalent canonical YAML.

**E2E/smoke**

- Add `e2e/pointsApi.spec.ts`: create group, create member and standalone
  points mapped to shared and direct sources, edit, filter, make standalone,
  delete, and reload server-backed data.

**Commit gate:** API is complete enough for the UI; frontend remains unchanged
and all suites pass.

**Suggested commit:** `feat(points): expose point and group definition API`

### Phase 4A - Controller-template store and HTTP API

**Purpose:** Safely manage custom YAML templates and expose the default example.

**Implementation**

- Add atomic normalized JSON persistence, revisions, deterministic listing,
  reserved-default protection, YAML round trips, and reference conflicts.
- Add the controller-template endpoints from section 3.8 with bounded bodies,
  stable diagnostics, and YAML/JSON content types.
- Wire `CONTROLLER_DATA_FILE` into startup; an absent file means no custom
  templates.

**Tests**

- Cover create/validate/update/delete/reopen, syntax line/column diagnostics,
  semantic paths, stale revisions, default mutations, malformed/oversized
  input, YAML/JSON round trips, rollback, and concurrent access.
- Add API E2E coverage for viewing the default and round-tripping a constrained
  custom YAML template.

**Commit gate:** The default is always retrievable and custom write failures
leave the prior file and in-memory state intact.

**Suggested commit:** `feat(controllers): expose YAML template API`

### Phase 5 - Frontend data layer and read-only catalogue

**Purpose:** Show point definitions and source relationships before enabling
point/group mutations.

**Implementation**

- Add strict DTO parsing/mapping, API client, latest-request handling, and Pinia
  store.
- Add `/points` and `/point-groups` routes and navigation.
- Add strict controller-template DTO parsing/API/store code and a
  `/controller-templates` catalogue showing built-in/custom and read-only state.
- Build semantic, responsive, paginated catalogue tables with filters, empty,
  loading, stale-request, and error states.
- Display membership, source, implementation, direction, value type, units,
  capabilities, and enabled state. Catalogue values are shown only when backed
  by a runtime envelope and are never fabricated.

**Unit tests**

- DTO rejection/mapping for every contract field.
- API query encoding and error mapping.
- Store race/error behavior.
- Table semantics, keyboard navigation, filters, empty/loading/error states,
  and accessible names.

**E2E/smoke**

- Add `e2e/pointsCatalogue.spec.ts` for navigation, paging, filtering, reload,
  network failure, keyboard operation, and axe checks at desktop/mobile sizes
  and light/dark themes.
- Add `e2e/controllerTemplates.spec.ts` for viewing and keyboard-navigating the
  exhaustive read-only default template.

**Commit gate:** Read-only screens tolerate an empty catalogue and an older
backend returning 404 by showing an actionable unavailable state.

**Suggested commit:** `feat(ui): add accessible point catalogues`

### Phase 6 - Point/group YAML editing and live value detail

**Purpose:** Complete YAML definition management and show a mapped point's real
value on its definition screen.

**Implementation**

- Add accessible labelled YAML create/edit screens with value-type and
  source-specific examples, validation summary, line/column and field-path
  diagnostics, and optional safe helper controls.
- Add group source/default mapping and point source/mapping configuration. Point
  source selection includes every compatible saved source and clearly shows
  whether the source is direct or inherited from the group.
- Add confirmation flows for deletion, conflicts, and make-standalone.
- Preserve unsaved input after server validation/conflict errors.
- Refresh revisions after successful writes and warn before navigating away
  with dirty forms.
- Add the backend `PointReadService` and `/api/points/{pointId}/runtime`
  envelope. Resolve direct/inherited source mappings and perform bounded,
  read-only Home Assistant entity reads, MQTT state subscriptions, and
  HTTP/JSON reads. Share connections safely where possible and mark every
  timeout, disconnect, parse failure, or stale sample with explicit quality.
- After a bound point is saved, start its read-only adapter and show the live
  typed runtime envelope on the same point definition screen. Subscribe where
  the source supports it; otherwise poll. Show value, units, quality,
  reliability, source timestamp, age, connection state, and
  live/cached/simulated/unavailable status with pause/retry controls.

**Unit tests**

- YAML parsing/rendering, examples, and helper synchronization for all point
  types and source mappings.
- Client validation mirrors backend validation without replacing it.
- Direct/inherited source mapping, group reassignment, source-kind mismatch,
  revision/delete conflicts, focus management, dirty navigation, submission
  lock, runtime subscription/poll cancellation, stale-value presentation, and
  API failure recovery.

**E2E/smoke**

- Add `e2e/pointsCrud.spec.ts`: create/edit/reload/delete every point type;
  map Home Assistant, MQTT, and HTTP/JSON points; create a group whose members
  share one source; move points between standalone and grouped; observe live
  value/quality/timestamp changes on point detail; show disconnected and stale
  states honestly; reject group deletion while occupied; resolve conflict; and
  verify keyboard-only use and WCAG scans.

**Commit gate:** Every CRUD journey survives a browser reload, no operation can
create an orphan or multi-group/source conflict, and a saved mapped point's
definition screen shows its real value or an explicit unavailable state.

**Suggested commit:** `feat(ui): manage points and point groups`

### Phase 6A - Accessible custom-template YAML editing

**Purpose:** Let users manage custom targets without prematurely building a
graphical schema editor.

**Implementation**

- Add create/edit routes with a labelled monospace textarea, template metadata,
  validate/save actions, line/column and field-path diagnostics, revision
  conflict handling, deletion confirmation, and dirty-navigation protection.
- Make the default view read-only while keeping its YAML selectable and usable
  as an example for a new custom template.

**Tests**

- Unit-test input preservation, diagnostic focus/associations, stale revisions,
  default immutability, delete conflicts, focus restoration, and API failures.
- E2E-test keyboard-only create/validate/fix/save/reload/delete and axe scans at
  desktop/mobile sizes and light/dark themes.

**Commit gate:** Invalid YAML cannot be persisted and all errors are available
to assistive technology without relying on colour.

**Suggested commit:** `feat(ui): manage controller template YAML`

### Phase 7 - Point nodes in the flow schema and toolbox

**Purpose:** Make point endpoints authorable while keeping old flows valid.

**Implementation**

- Extend Go and TypeScript node-kind enums with `read-point` and `write-point`.
- Add themed SVG icons and a `points` palette category.
- Create nodes with typed connectors and a required `pointId` contract snapshot.
- Add accessible inspector comboboxes:
  - read nodes list readable input/input-output/value points;
  - write nodes list commandable output/input-output/value points.
- Support search and group labels without making group membership part of flow
  identity.
- Preserve a node with a missing/changed point as an invalid placeholder.
- Persist `controllerTemplateId`, treating a missing legacy value as `default`,
  and add an accessible target selector to flow settings.
- Filter/annotate point choices and palette functions using the target
  capabilities while preserving already-saved unsupported nodes for repair.

**Unit tests**

- Registry/Go catalogue parity and icon existence.
- Node creation for all point types.
- Capability/direction filtering and accessible selection.
- DTO round-trip of new and legacy nodes.
- Missing, disabled, direction-changed, and type-changed point behavior.
- Legacy default-target migration, target switching, palette/point filtering,
  and unsupported-node diagnostics.

**E2E/smoke**

- Add `e2e/designerPoints.spec.ts`: palette keyboard/drag creation, compatible
  point selection, save/reload, group labels, invalid deleted-point rendering,
  theme appearance, and accessibility.

**Commit gate:** Existing flows load byte-semantically unchanged; new draft
flows with point nodes save and reload; deployment remains guarded until phase
8 validation is present.

**Suggested commit:** `feat(flows): add typed point nodes to the designer`

### Phase 8 - Cross-resource validation and safe deletion

**Purpose:** Ensure saved/deployed flows cannot reference incompatible points.

**Implementation**

- Add a flow validation service that resolves point references against a
  consistent point snapshot.
- On save, reject malformed point-node configuration and impossible node
  contracts. Allow missing references only for drafts, with structured
  diagnostics.
- On deploy, reject missing, disabled, unreadable/uncommandable, type-changed,
  or unit-changed point references.
- Add point delete-reference checks and return referencing flow IDs.
- Surface server diagnostics in the designer and link users to affected nodes.
- Resolve the selected controller template and validate all graph functions,
  connector types, point contracts, execution mode, limits, and runtime
  features before deployment.
- Capture template revision with the deployed snapshot; block deletion or
  capability-reducing edits when referenced unless the explicit conflict flow
  is completed.

**Unit/integration tests**

- Matrix of node kind, point direction/capability, enabled state, type, units,
  and draft/deploy outcome.
- Concurrent point edit versus flow save/deploy uses one snapshot and gives a
  deterministic result.
- Point deletion is blocked for every reference and succeeds after repair.
- Legacy flows without point nodes are unaffected.
- Missing templates, stale template revisions, every capability family, limit
  boundaries, edits racing deployment, and unchanged running snapshots.

**E2E/smoke**

- Extend `designerPoints.spec.ts`: definition drift, blocked deployment,
  navigate-to-invalid-node, blocked delete with referencing flows, repair,
  successful save/deploy/delete.

**Commit gate:** No dangling point can enter a deployed flow, and pre-points
flows deploy as before.

**Suggested commit:** `feat(points): validate flow references and safe deletion`

### Phase 9 - Level-shifter authoring and validation

**Purpose:** Provide an explicit, safe analog/digital conversion boundary.

**Implementation**

- Add `level-shifter` to Go/TypeScript registries and the maths/conversion
  palette with a themed SVG icon.
- Add mode-dependent connectors and inspector fields.
- Implement shared-equivalent validation rules, including mandatory hysteresis.
- Update connection editing to reject analog/digital direct connections and
  present a useful suggestion to insert a level shifter.
- Implement a pure evaluator even if the general runtime does not yet execute
  graphs, so semantics are fixed and testable.

**Unit tests**

- Digital-to-analog low/high mapping and invalid/equal/non-finite values.
- Analog-to-digital rising/falling transitions, retained state inside the
  hysteresis band, exact thresholds, and documented initial state.
- Connector regeneration without orphaning silently incompatible connections.
- Backend rejects handcrafted invalid configuration.

**E2E/smoke**

- Add `e2e/designerLevelShifter.spec.ts`: direct connection rejected; configure
  both modes; invalid threshold errors; keyboard connection; save/reload;
  backend rejection of tampered graphs; light/dark accessibility.

**Commit gate:** Every analog/digital conversion is explicit and a saved graph
cannot contain an invalid hysteresis configuration.

**Suggested commit:** `feat(flows): add validated level shifter block`

### Phase 10 - Runtime value store and complete point detail

**Purpose:** Promote on-demand mapped reads into the complete point runtime and
execute virtual-point reads/writes safely.

**Implementation**

- Add a runtime store separate from definitions, with typed value, quality,
  reliability reason, timestamps, sequence/revision, and last-good value.
- Restore retained virtual values; reset volatile ones to defaults.
- Implement read/write point execution and the level-shifter evaluator.
- Add point snapshot/change APIs; use polling first if subscriptions are not yet
  available, while preserving sequence semantics for a future stream.
- Reuse the Phase 1 source adapters and Phase 6 point-detail envelopes rather
  than opening duplicate connections; manage shared subscriptions/pollers by
  source and group.
- Refuse runtime construction when the deployed snapshot requests a capability
  outside its captured target contract.

**Unit/integration tests**

- Typed reads/writes, restart behavior, sequence ordering, stale writes,
  last-good handling, disabled points, and atomic group updates.
- End-to-end flow evaluation for read -> level shifter -> write.
- No failed/missing input becomes zero/false/empty text.

**E2E/smoke**

- Add `e2e/pointRuntime.spec.ts`: deploy a virtual-point flow, observe typed
  values and quality, restart persistence fixtures, share one source across
  multiple points/groups, and preserve honest disconnected/stale bound states.

**Commit gate:** Runtime reads mapped bound inputs and supports virtual points,
but cannot write external equipment until command and safe-output phases.

**Suggested commit:** `feat(points): execute virtual point values`

### Phase 11 - Command arbitration and flow lifecycle

**Purpose:** Make writable point behavior deterministic and safe.

**Implementation**

- Add command table, site policy, deterministic arbitration, expiry, source
  attribution, correlation IDs, and relinquish.
- Add `release-point-command` node and expose it only now.
- Give each deployed flow a stable source ID. Stop/delete/undeploy releases its
  non-retained commands.
- Add command/relinquish endpoints with idempotency keys.

**Unit/integration tests**

- Class and priority ordering, tie-breaks, expiry, targeted relinquish, restart
  retention, unauthorized class rejection, and lifecycle cleanup.
- Property tests show arbitration is deterministic for reordered inputs.

**E2E/smoke**

- Competing flows, visible winner/source, stop winner to reveal next command,
  release own command, expiry, and browser reload.

**Commit gate:** Last-write-wins is not used and ordinary flows cannot issue or
release protected commands.

**Suggested commit:** `feat(points): add deterministic command arbitration`

### Phase 12 - Binding/driver expansion and point-group I/O

**Purpose:** Expand beyond initial read adapters and connect safe bound outputs
without coupling protocols to the core model.

**Implementation**

- Define driver interfaces for lifecycle, capability discovery, samples,
  commands, health, and group-level atomic/batched payloads.
- Make the Phase 1 Home Assistant, MQTT, and HTTP/JSON adapters satisfy the
  shared driver contract; implement an in-memory/loopback conformance driver
  before enabling writes or adding field protocols.
- Apply input/output pipelines, timeouts, reconnection, scaling, explicit safe
  policies, and group update semantics.
- Keep credential material behind references and redact it from logs/errors.

**Unit/integration tests**

- Contract tests every driver must pass.
- Reconnect/stale/error transitions, out-of-order samples, atomic group
  application, partial payload policy, failed output, disable/startup/shutdown,
  and no duplicate writes after retry.
- Race and fault-injection tests.

**E2E/smoke**

- A mixed-direction loopback group updates as one payload; disconnect changes
  quality; reconnect recovers; disable invokes its declared safe policy.

**Commit gate:** Only drivers passing the contract suite may be enabled, and
output bindings without complete safe policies remain disabled.

**Suggested commit:** `feat(points): add safe grouped binding runtime`

### Phase 13 - Quality-aware flows, commissioning, alarms, and history

**Purpose:** Complete the operational model from `.codex/point-types.md`.

**Implementation**

- Emit value, quality, timestamp, and sequence envelopes from point-read/change
  nodes; add `point-changed` only now.
- Add explicit fallback/inhibit behavior and quality propagation.
- Add out-of-service simulation and controlled return to service.
- Add command table/override UI with expiry, reason, and relinquish.
- Add alarm evaluation, trend configuration, and immutable audit records in
  stores separate from definitions/runtime.
- Require an authorization design before hazardous actions are enabled outside
  development.

**Unit/integration tests**

- Quality propagation and fallback policies; stale/bad input never silently
  drives a normal output.
- Out-of-service input/output and return-to-service reconciliation.
- Alarm state/deadband/delay/acknowledgement transitions.
- Trend retention and immutable command/configuration audit records.

**E2E/smoke**

- Simulate bad/stale values; verify flow inhibit/fallback and visible reason.
- Commission a point out of service and return it safely.
- Apply and expire an override, inspect underlying automatic command, alarm
  transition, acknowledgement, and audit trail.
- WCAG scan all operational states, not only empty/default screens.

**Commit gate:** Safety-sensitive actions are attributable and auditable;
quality is visible throughout control decisions; no feature relies on a single
combined `state` enum.

**Suggested commit:** `feat(points): add quality and commissioning services`

## 6. Migration and compatibility strategy

- Absence of `data/points.json` means an empty version-1 catalogue.
- Absence of `data/point-sources.json` means an empty source catalogue.
- Absence of `controllerTemplateId` means the embedded `default`; absence of
  `data/controllers.json` means only the default is available.
- Never persist or overwrite the embedded default. Template schema changes
  require fixture-tested YAML contract and normalized JSON migrations.
- Never rewrite `flows.json` merely because the server starts.
- Old flow node kinds and connector contracts remain accepted.
- New fields added to point documents require a schema-version migration with
  fixture tests from every previous version.
- Migrations write a backup and use atomic replacement; failure keeps the
  original readable file and prevents startup with a clear error.
- A point rename is safe because flows reference stable IDs.
- A point type, unit, direction, or capability change increments its revision
  and may invalidate drafts; it cannot silently alter a deployed flow.
- Before such a contract-changing edit, the API returns affected flow IDs and
  requires explicit confirmation/revision. The runtime continues using the
  last deployed validated snapshot until a corrected flow is redeployed.
- Feature flags may hide runtime-only palette nodes before their backend phase,
  but persisted kinds must never be conditionally rejected after release.

## 7. Test inventory

By the end of phase 9, the repository should contain at least:

```text
backend/internal/points/model_test.go
backend/internal/points/store_test.go
backend/internal/points/http_test.go
backend/internal/points/service_test.go
backend/internal/points/source_model_test.go
backend/internal/points/source_store_test.go
backend/internal/points/source_connectivity_test.go
backend/internal/points/source_read_test.go
backend/internal/flows/point_validation_test.go
backend/internal/flows/level_shifter_test.go
backend/internal/controllers/model_test.go
backend/internal/controllers/store_test.go
backend/internal/controllers/http_test.go
backend/internal/flows/controller_validation_test.go

frontend/flow-control-ui/src/features/points/**/__tests__/*.spec.ts
frontend/flow-control-ui/src/features/flows/**/__tests__/*point*.spec.ts
frontend/flow-control-ui/src/features/flows/**/__tests__/*levelShifter*.spec.ts
frontend/flow-control-ui/src/features/controllers/**/__tests__/*.spec.ts

frontend/flow-control-ui/e2e/pointsApi.spec.ts
frontend/flow-control-ui/e2e/pointSources.spec.ts
frontend/flow-control-ui/e2e/pointsCatalogue.spec.ts
frontend/flow-control-ui/e2e/pointsCrud.spec.ts
frontend/flow-control-ui/e2e/designerPoints.spec.ts
frontend/flow-control-ui/e2e/designerLevelShifter.spec.ts
frontend/flow-control-ui/e2e/controllerTemplates.spec.ts
frontend/flow-control-ui/e2e/designerControllerTargets.spec.ts
```

Prefer behavior-based test names and public boundaries. Unit tests should own
validation edge cases; E2E tests should cover a smaller set of high-value user
journeys rather than repeat the entire validation matrix.

## 8. Phase review checklist

Before merging each phase, review:

- **Persistence:** Is the write atomic, versioned, deterministic, and recoverable?
- **Configuration boundary:** Are user-edited point/source/group/controller
  documents canonical YAML while normalized durable state remains JSON?
- **Sources:** Can one source safely serve many points/groups, are mappings
  validated, and are connectivity tests bounded, cancellable, read-only, and
  redacted?
- **Compatibility:** Can the previous release's files and flows still load?
- **Integrity:** Can a point be orphaned, multiply grouped, or silently cascaded?
- **Targeting:** Is the default exhaustive/read-only, is custom YAML validated,
  and does deployment use one immutable capability snapshot?
- **Typing:** Are value type, units, direction, and capabilities checked at
  browser, API, deployment, and runtime boundaries as applicable?
- **Safety:** Can missing/bad data operate an output or become a default value?
- **Concurrency:** Are stale revisions and racing changes deterministic?
- **Accessibility:** Are semantics, labels, errors, focus, contrast, zoom, and
  keyboard paths covered?
- **Observability:** Are operational failures visible without leaking secrets?
- **Live value:** Does point detail show value, quality, timestamp, age, and
  connection state without presenting cached or failed data as live?
- **Testing:** Are positive, negative, persistence-failure, and regression cases
  present at the appropriate layer?
- **Releaseability:** Can this exact commit build, run, and be rolled back
  without requiring an uncommitted next phase?
