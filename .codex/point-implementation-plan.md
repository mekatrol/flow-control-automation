# Points implementation plan

## 1. Goal

Implement the point model described in `.codex/point-types.md` across the Go
backend and Vue frontend. Users must be able to:

- create, view, edit, and delete point definitions;
- create, view, edit, and delete point groups;
- keep a point standalone or assign it to exactly one visible group;
- add typed point-read and point-write blocks to a flow;
- select only points compatible with a block's direction;
- connect blocks only when their value types are compatible;
- explicitly convert analog and digital values with a level-shifter block; and
- save, reload, and deploy valid point-backed flows without breaking existing
  saved flows.

The work is divided into independently buildable and committable phases. Every
phase includes its own tests and leaves the application deployable. Backend
contract work precedes frontend consumers, and persisted flow changes remain
backward compatible.

## 2. Scope and delivery boundaries

### Initial usable release (phases 0-8)

The first release covers point and point-group definition management, flow
authoring, deployment validation, and a level shifter. It supports all initial
value types from the point model:

- `analog`
- `digital`
- `multi_state`
- `integer`
- `text`

This release stores binding configuration but does not implement protocol
drivers. Point blocks are valid graph endpoints, but a deployed block cannot
exchange a live value until the runtime work in later phases is present.
Deployment must report that limitation explicitly; it must never pretend that a
bound point is live.

### Runtime release (phases 9-12)

The subsequent release adds runtime values, drivers, command arbitration,
quality propagation, commissioning, alarms, trends, and audit history. These
features are separate because point definitions, live state, commands, and
history have different persistence and safety requirements.

### Explicitly deferred

The following should not be smuggled into an earlier phase:

- a production credential store;
- individual BACnet, Modbus, KNX, MQTT, Home Assistant, and HTTP drivers;
- unit conversion beyond exact-unit compatibility;
- user/role authorization;
- alarm notification delivery and long-term trend storage;
- high-availability runtime coordination.

The extension points for these features are defined below, but each needs its
own design and threat/safety review before implementation.

## 3. Architectural decisions

### 3.1 Point and group persistence

Create a new backend package, `backend/internal/points`, rather than extending
the flow store. Use a separate JSON definition file configured by
`POINT_DATA_FILE`, defaulting to `data/points.json`. This preserves the existing
`FLOW_DATA_FILE` contract and prevents live point data from being written into
flow definitions.

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
binding?
limits?
safeDisablePolicy?
revision, createdAt, updatedAt
```

The initial `PointGroup` contract is:

```text
id, name, description
binding?
revision, createdAt, updatedAt
```

A group's optional binding contains the shared transport/address configuration
for atomic or batched updates. A point's optional binding contains its member
mapping within that transport. Keep both as typed envelopes with `driver` and
driver-specific `configuration`; do not accept arbitrary properties at the
top-level API boundary. Secrets are credential references, never literal
credentials.

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
- bound points require a binding either on themselves or their group;
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
```

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

- Add version-1 point/group JSON contract fixtures covering every value type,
  standalone/grouped points, virtual/bound implementations, and invalid cases.
- Add legacy flow fixtures that contain only current node kinds.
- Document canonical enum spellings and the point-to-connector mapping.
- Add test helpers for temporary backend data files and Playwright point API
  seeding.
- Record baseline accessibility and full-suite results.

**Unit/integration tests**

- Existing flow fixtures decode, validate, save, and reload unchanged.
- Contract fixtures agree between Go JSON decoding and TypeScript DTO parsing.
- Unknown fields and unsupported schema versions fail.

**E2E/smoke**

- Existing flow list and designer CRUD journeys still pass with no point file.

**Commit gate:** No user-visible behavior changes; all existing tests and builds
pass.

**Suggested commit:** `test: establish points contract compatibility baseline`

### Phase 1 - Backend point and group domain model

**Purpose:** Introduce validated domain types without HTTP or UI changes.

**Implementation**

- Create `backend/internal/points/model.go`.
- Define enums and typed validation for definitions, groups, labels, limits,
  defaults, capabilities, safe policies, and binding envelopes.
- Centralize compatibility and capability predicates so HTTP and flow
  validation do not invent separate rules.
- Reserve the hidden-group name.

**Unit tests**

- Table-test every valid value type/direction/implementation combination.
- Cover NaN/infinity, integer precision, ranges, labels, defaults, units,
  capabilities, missing bindings, credential literals, unsafe output policies,
  whitespace, duplicate names, and reserved names.
- Fuzz JSON decode and validation; it must never panic.

**E2E/smoke**

- Not applicable at the UI level; run the existing E2E suite unchanged.

**Commit gate:** Package is unused by production routing, backend tests pass,
and the existing application builds/runs unchanged.

**Suggested commit:** `feat(points): add validated point definition model`

### Phase 2 - Durable backend store

**Purpose:** Persist point/group definitions atomically.

**Implementation**

- Add the versioned document and `Store` with list/get/create/update/delete.
- Enforce unique IDs/names, one-or-zero group membership, revisions, referential
  integrity, and deterministic persistence order.
- Add the explicit make-standalone transaction.
- Wire `POINT_DATA_FILE` into server startup, but expose no routes yet.

**Unit/integration tests**

- Empty/missing file startup; round-trip every type.
- Atomic create/update/delete and rollback after injected write/rename failure.
- Concurrent operations under `go test -race ./...`.
- Stale revision conflicts, group-in-use conflicts, duplicate names/IDs,
  orphaned group references, corrupt JSON, and unsupported versions.
- Make-standalone updates all members or none.

**E2E/smoke**

- Start the server with an absent point file; health and existing flow APIs
  remain available. Restart and confirm saved point fixtures reload.

**Commit gate:** Server startup remains backward compatible and no point
endpoint is public yet.

**Suggested commit:** `feat(points): persist point and group definitions`

### Phase 3 - Point and group HTTP API

**Purpose:** Make definition management available to clients.

**Implementation**

- Add point/group service and handlers with bounded JSON bodies.
- Add paging/filter/sort, stable error codes, revision conflicts, and reference
  details.
- Extend the handler constructor to receive both stores explicitly.
- Preserve all existing flow paths and response contracts.

**Unit/integration tests**

- Handler tests for success, all validation failures, unknown IDs, malformed
  query/body values, trailing JSON, oversized requests, persistence failures,
  stale revisions, membership conflicts, and content types.
- Pagination/filter/sort determinism.
- API-created data survives store reopen.

**E2E/smoke**

- Add `e2e/pointsApi.spec.ts`: create group, create member and standalone
  points, edit, filter, make standalone, delete, reload server-backed data.

**Commit gate:** API is complete enough for the UI; frontend remains unchanged
and all suites pass.

**Suggested commit:** `feat(points): expose point and group definition API`

### Phase 4 - Frontend data layer and read-only catalogue

**Purpose:** Show point data before enabling mutations.

**Implementation**

- Add strict DTO parsing/mapping, API client, latest-request handling, and Pinia
  store.
- Add `/points` and `/point-groups` routes and navigation.
- Build semantic, responsive, paginated catalogue tables with filters, empty,
  loading, stale-request, and error states.
- Display membership, implementation, direction, value type, units,
  capabilities, and enabled state. Do not display fake live values.

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

**Commit gate:** Read-only screens tolerate an empty catalogue and an older
backend returning 404 by showing an actionable unavailable state.

**Suggested commit:** `feat(ui): add accessible point catalogues`

### Phase 5 - Frontend point and group CRUD

**Purpose:** Complete definition management.

**Implementation**

- Add create/edit forms with value-type-specific fieldsets and dependent fields.
- Add group assignment and group binding/member mapping editors.
- Add confirmation flows for deletion, conflicts, and make-standalone.
- Preserve unsaved input after server validation/conflict errors.
- Refresh revisions after successful writes and warn before navigating away
  with dirty forms.

**Unit tests**

- Conditional fields and defaults for all types.
- Client validation mirrors backend validation without replacing it.
- Group reassignment, revision conflicts, delete conflicts, focus management,
  dirty navigation, submission lock, and API failure recovery.

**E2E/smoke**

- Add `e2e/pointsCrud.spec.ts`: create/edit/reload/delete every point type;
  create a group with input and output members; move points between standalone
  and grouped; reject group deletion while occupied; resolve conflict; verify
  keyboard-only use and WCAG scans.

**Commit gate:** Every CRUD journey survives a browser reload and no operation
can create an orphan or multi-group membership.

**Suggested commit:** `feat(ui): manage points and point groups`

### Phase 6 - Point nodes in the flow schema and toolbox

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

**Unit tests**

- Registry/Go catalogue parity and icon existence.
- Node creation for all point types.
- Capability/direction filtering and accessible selection.
- DTO round-trip of new and legacy nodes.
- Missing, disabled, direction-changed, and type-changed point behavior.

**E2E/smoke**

- Add `e2e/designerPoints.spec.ts`: palette keyboard/drag creation, compatible
  point selection, save/reload, group labels, invalid deleted-point rendering,
  theme appearance, and accessibility.

**Commit gate:** Existing flows load byte-semantically unchanged; new draft
flows with point nodes save and reload; deployment remains guarded until phase
7 validation is present.

**Suggested commit:** `feat(flows): add typed point nodes to the designer`

### Phase 7 - Cross-resource validation and safe deletion

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

**Unit/integration tests**

- Matrix of node kind, point direction/capability, enabled state, type, units,
  and draft/deploy outcome.
- Concurrent point edit versus flow save/deploy uses one snapshot and gives a
  deterministic result.
- Point deletion is blocked for every reference and succeeds after repair.
- Legacy flows without point nodes are unaffected.

**E2E/smoke**

- Extend `designerPoints.spec.ts`: definition drift, blocked deployment,
  navigate-to-invalid-node, blocked delete with referencing flows, repair,
  successful save/deploy/delete.

**Commit gate:** No dangling point can enter a deployed flow, and pre-points
flows deploy as before.

**Suggested commit:** `feat(points): validate flow references and safe deletion`

### Phase 8 - Level-shifter authoring and validation

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

### Phase 9 - Runtime value store and virtual points

**Purpose:** Execute point reads/writes safely for virtual points.

**Implementation**

- Add a runtime store separate from definitions, with typed value, quality,
  reliability reason, timestamps, sequence/revision, and last-good value.
- Restore retained virtual values; reset volatile ones to defaults.
- Implement read/write point execution and the level-shifter evaluator.
- Add point snapshot/change APIs; use polling first if subscriptions are not yet
  available, while preserving sequence semantics for a future stream.
- Mark bound points `bad/binding_not_configured` until a driver owns them.

**Unit/integration tests**

- Typed reads/writes, restart behavior, sequence ordering, stale writes,
  last-good handling, disabled points, and atomic group updates.
- End-to-end flow evaluation for read -> level shifter -> write.
- No failed/missing input becomes zero/false/empty text.

**E2E/smoke**

- Add `e2e/pointRuntime.spec.ts`: deploy a virtual-point flow, observe typed
  values and quality, restart persistence fixture, and display unavailable
  bound points honestly.

**Commit gate:** Runtime supports virtual points only and cannot write external
  equipment.

**Suggested commit:** `feat(points): execute virtual point values`

### Phase 10 - Command arbitration and flow lifecycle

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

### Phase 11 - Binding/driver boundary and point-group I/O

**Purpose:** Connect bound points without coupling protocols to the core model.

**Implementation**

- Define driver interfaces for lifecycle, capability discovery, samples,
  commands, health, and group-level atomic/batched payloads.
- Implement one low-risk reference driver (in-memory/test or loopback) before a
  real protocol.
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

### Phase 12 - Quality-aware flows, commissioning, alarms, and history

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

By the end of phase 8, the repository should contain at least:

```text
backend/internal/points/model_test.go
backend/internal/points/store_test.go
backend/internal/points/http_test.go
backend/internal/points/service_test.go
backend/internal/flows/point_validation_test.go
backend/internal/flows/level_shifter_test.go

frontend/flow-control-ui/src/features/points/**/__tests__/*.spec.ts
frontend/flow-control-ui/src/features/flows/**/__tests__/*point*.spec.ts
frontend/flow-control-ui/src/features/flows/**/__tests__/*levelShifter*.spec.ts

frontend/flow-control-ui/e2e/pointsApi.spec.ts
frontend/flow-control-ui/e2e/pointsCatalogue.spec.ts
frontend/flow-control-ui/e2e/pointsCrud.spec.ts
frontend/flow-control-ui/e2e/designerPoints.spec.ts
frontend/flow-control-ui/e2e/designerLevelShifter.spec.ts
```

Prefer behavior-based test names and public boundaries. Unit tests should own
validation edge cases; E2E tests should cover a smaller set of high-value user
journeys rather than repeat the entire validation matrix.

## 8. Phase review checklist

Before merging each phase, review:

- **Persistence:** Is the write atomic, versioned, deterministic, and recoverable?
- **Compatibility:** Can the previous release's files and flows still load?
- **Integrity:** Can a point be orphaned, multiply grouped, or silently cascaded?
- **Typing:** Are value type, units, direction, and capabilities checked at
  browser, API, deployment, and runtime boundaries as applicable?
- **Safety:** Can missing/bad data operate an output or become a default value?
- **Concurrency:** Are stale revisions and racing changes deterministic?
- **Accessibility:** Are semantics, labels, errors, focus, contrast, zoom, and
  keyboard paths covered?
- **Observability:** Are operational failures visible without leaking secrets?
- **Testing:** Are positive, negative, persistence-failure, and regression cases
  present at the appropriate layer?
- **Releaseability:** Can this exact commit build, run, and be rolled back
  without requiring an uncommitted next phase?
