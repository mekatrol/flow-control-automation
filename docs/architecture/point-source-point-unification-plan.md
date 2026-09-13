# Point source and point unification implementation plan

## Status and intent

This plan replaces the separate point-source and point-definition configuration
models with one point-source aggregate. A point is always owned by exactly one
point source. The source owns its connection, one or more reusable mappings, and
one or more points that consume values exposed by those mappings.

`docs/architecture/point-source-schema.yaml` is an illustrative HTTP/JSON
example. It establishes the intended nesting, but it is not a complete contract
and must not be implemented as though HTTP/JSON and analog/digital points are
the only supported cases. Home Assistant, MQTT, HTTP/JSON, every currently
supported point value type, and virtual and physical points must continue to be
representable. The final version-1 fixtures produced in Phase 1 are normative.

This is a coordinated, pre-release breaking change:

- all current configuration schemas remain at `schemaVersion: 1`;
- no legacy point document or standalone point storage is retained;
- no compatibility reader, alias, fallback, dual-write, data migration, or
  database upgrade path is added;
- existing development databases and old YAML must be discarded and recreated;
- obsolete models, fields, fixtures, endpoints, UI paths, and tests are deleted,
  rather than deprecated.

## Target model

### Aggregate and identity

The point source is the unit of validation, persistence, revision control,
creation, update, and deletion. Its version-1 document has this shape:

```yaml
schemaVersion: 1
id: source-id
name: Source name
description: Optional description
enabled: true
kind: httpJson
connection: {}
tls: {}
timeouts: {}
mappings: []
points: []
```

The exact connection and mapping fields are discriminated by `kind`. Do not
introduce a wrapper such as `sources: [...]`, and do not retain a separate
`points: [...]` document.

Every point is nested under exactly one source. It therefore has no `sourceId`
or `pointSourceType` field. The source kind supplies the integration type.
Virtual and physical behavior must be represented as source kinds (or as an
equally explicit discriminator on the owning source agreed in Phase 1), not as
an independent point ownership model. Point IDs remain globally unique because
flows, runtime APIs, audit records, and operator actions address a point without
also carrying a source ID. Source IDs, mapping IDs, and point IDs use the
existing strict identifier rules.

Consequently, `Server.Common.Types.PointSourceType` is redundant and must be
deleted. Do not move or rename its `Physical`, `Virtual`, and `Remote` values
onto the point. Extend/replace the source-level `PointSourceKind` contract so it
fully discriminates all supported source implementations, including virtual
and physical sources, and derive any broad presentation grouping from that kind
when needed. A point catalogue projection may expose its owning `sourceKind`,
but it must not persist or independently validate a second source-type value.

Deleting a source deletes its mappings and point definitions as one aggregate.
The operation must still be rejected when a point is referenced by a flow or
another protected dependency. No endpoint may independently move or delete a
nested point.

### Mapping contract

A source contains zero or more mappings. Each mapping has a source-local unique
`id` and communication operations appropriate to the source `kind`. A mapping
may be referenced by multiple points, and a single communication result may
expose multiple named values.

A point refers to one value with the strict path `<mapping-id>/<value-alias>`,
for example `outputs/analogOutput0`. Both segments are required; empty segments,
extra segments, unknown mapping IDs, and unknown aliases are validation errors.
Aliases are unique within a mapping and are case-sensitive.

Communication adapters and templates deal in transport text. They do not infer
the consuming point's type. For an HTTP/JSON read, the adapter makes the request,
constructs the documented template model from the parsed response, and renders
the mapping's read template through `ITemplateService`. The rendered object maps
aliases to raw textual values. Given a response model containing `intensity` and
`index`, this mapping can expose both values:

```yaml
template: |
  {
    "analogOutput0": {{ intensity }},
    "analogVirtual0": {{ index }}
  }
```

A point mapped to `outputs/analogOutput0` then converts that raw value according
to its own `valueType`, limits, state labels, and other point rules. The same raw
alias may be consumed by more than one point when their contracts permit it.
Conversion failures produce a bad-quality/test diagnostic; they must not be
silently coerced or replaced with a default.

Command processing performs the inverse at the aggregate boundary: validate
and serialize the typed point command to invariant transport text, bind it to
the selected mapping alias in the documented template model, render the
mapping's command template through `ITemplateService`, then invoke the mapping's
kind-specific command communication. The Phase 1 contract must explicitly
define the template model for reads and commands, escaping rules, null and
boolean spelling, numeric culture, multi-state handling, missing/extra aliases,
and whether a command response supplies readback. Templates must never be
rendered directly with Scriban or string replacement outside
`ITemplateService`.

Each kind retains its existing communication capabilities and safety controls:

- HTTP/JSON mappings retain relative paths, safe read methods, command methods,
  formats/content types, headers allowed by policy, polling overrides, response
  bounds, redirects, DNS rebinding protection, and private-network policy.
- MQTT mappings retain state and command topics, QoS, retain behavior, payload
  extraction/rendering, subscriptions, reconnect behavior, and timeouts.
- Home Assistant mappings retain entity/property reads and supported service
  commands while moving that communication definition under a mapping.
- Physical mappings retain controller/channel/address and electrical behavior.
- Virtual mappings provide source-owned aliases without external communication
  and preserve volatile/retained runtime semantics.

Read and command sections are optional only when every point using the mapping
is compatible with the available operation. A readable point requires a read
operation; a commandable point requires a command operation. Direction,
`readable`, `commandable`, persistence, limits, safe-disable policy, state
labels, and all existing value types retain their current semantic validation.

### Runtime and test behavior

Runtime lookup by point ID resolves the owning source, mapping, and alias, then
dispatches through the source-kind adapter. One communication read may return
several aliases; cache/update all mapped point values from that result with the
same source timestamp and communication quality, converting each independently.
Polling and subscriptions are scheduled per mapping, not once per consuming
point. Commanding one point invokes its mapping once and uses that point's alias
and typed value.

Source connectivity testing remains non-mutating and checks only the connection
and protocol. Point testing is available from the point-source editor for a
specific nested point:

- **Read** automatically resolves and executes that point's mapping, selects
  its alias, converts it to the point value type, and returns typed value,
  transport preview, stages, timing, quality, and redacted diagnostics.
- **Command** accepts a value using the point's type, validates it against the
  point contract, automatically renders and executes its mapping command, and
  returns the sent preview and safe response/readback details.
- The UI offers only operations supported by the selected point. A command test
  retains the existing explicit warning/confirmation and authorization rules.
- Unsaved testing submits the complete edited source aggregate plus `pointId`,
  operation, and optional typed value. Saved testing identifies the saved source
  and `pointId`; the server loads the authoritative aggregate.
- Tests are bounded, cancellable, rate-limited, audited where currently
  required, SSRF-safe, credential-redacted, and never persist runtime values.

## Proposed API surface

Keep point-source resource naming because configuration remains source-led:

```text
GET    /api/point-sources
POST   /api/point-sources
GET    /api/point-sources/{sourceId}
PUT    /api/point-sources/{sourceId}
DELETE /api/point-sources/{sourceId}?revision={revision}

POST   /api/point-sources/test
POST   /api/point-sources/test-point
POST   /api/point-sources/{sourceId}/test
POST   /api/point-sources/{sourceId}/points/{pointId}/test

GET    /api/points
GET    /api/points/{pointId}/runtime
```

The first five endpoints read and write the complete aggregate YAML. List
responses include source summaries plus point/mapping counts, not full YAML.
`If-Match` continues to protect aggregate updates. Updating any nested mapping
or point increments the source revision.

`GET /api/points` remains a read-only, normalized catalogue for flow authoring
and operator screens; it is a projection from source aggregates. Runtime lookup
also remains point-addressed. Remove standalone point create, get-definition,
update, and delete endpoints. Do not retain their request shapes internally.

Use one point-test result envelope for saved and unsaved operations. It must
identify `sourceId`, `pointId`, mapping ID, alias, operation, typed point value
when available, redacted rendered request/response previews, stages, and a
structured diagnostic. Use the existing API error conventions: malformed or
invalid aggregate/operation is `400`, missing saved source or point is `404`,
revision/dependency conflicts are `409`, forbidden command/network operation is
`403`, and upstream communication failure is `502`.

## Delivery phases

Implementation status (2026-09-13): Phases 1 through 4 are complete. The normative contract is
`point-source-v1-contract.md` plus the paired YAML/normalized-JSON fixtures under
`testdata/contracts/point-sources/valid`; aggregate parser, models, validation, and mapping
resolution now implement that contract. Exhaustive validator coverage and aggregate persistence
are complete; adapter, API, and editor cutovers remain tracked by Phases 5–8.

| Phase | Status |
| --- | --- |
| Phase 1 — Freeze the version-1 contract and fixtures | Complete |
| Phase 2 — Replace common models, parsing, and validation | Complete |
| Phase 3 — Add exhaustive point-source and point-definition validator tests | Complete |
| Phase 4 — Replace persistence and aggregate services | Complete |
| Phase 5 — Implement mapping execution and point operations | Pending |
| Phase 6 — Cut over HTTP endpoints and configuration guidance | Pending |
| Phase 7 — Replace the frontend source and point workflows | Pending |
| Phase 8 — Remove legacy artifacts, update documentation, and verify | Pending |

Each phase below is intended to leave a reviewable, testable result. Because
this is a breaking aggregate replacement, intermediate branches need not run
the entire product until the coordinated backend/frontend cutover, but every
phase must pass its stated focused tests.

### Phase 1 — Freeze the version-1 contract and fixtures (complete)

1. Turn `docs/architecture/point-source-schema.yaml` into a valid canonical
   HTTP/JSON fixture: correct the aliases so the templates and point mapping
   paths agree, and include a mapping that exposes at least two values to two
   points.
2. Define canonical version-1 YAML and normalized JSON fixtures for virtual,
   physical, Home Assistant, MQTT, and HTTP/JSON sources. Across them cover
   analog, digital, multi-state, integer, and text points; readable-only,
   commandable-only, and read/write behavior; multiple mappings; multiple
   points per mapping; and shared aliases.
3. Specify kind-discriminated mapping fields and the exact read/command template
   contexts. Record raw-text conversion rules for every point value type.
4. Add invalid fixtures for the old `sources` wrapper, standalone point
   documents, `pointSourceType`, `sourceId`, inline per-point mapping objects,
   missing/unknown mapping aliases, duplicate IDs/aliases, incompatible
   capabilities, unknown fields, unsupported schema versions, and malformed
   template output.
5. Update `docs/reference/configuration-vocabulary-v1.md` and
   `docs/reference/point-model.md` to describe ownership and vocabulary without
   promising the old split model.

Verification: backend and frontend fixture tests agree on every canonical and
invalid fixture; all canonical documents have `schemaVersion: 1`; a contract
review confirms every existing source kind and point value type is covered.

### Phase 2 — Replace common models, parsing, and validation (complete)

1. Make `PointSource` the aggregate root with `Mappings` and `Points`.
   Introduce a discriminated mapping hierarchy (or converter) whose
   communication fields are selected strictly by source kind.
2. Replace `PointSourceDocument`, `PointDocument`, `PointSourceYaml`, and
   `PointYaml` with one strict aggregate serializer/parser. The transport form
   excludes revision/timestamps, while the stored/API model preserves aggregate
   concurrency metadata.
3. Remove `sourceId`, `pointSourceType`, and inline protocol mapping from point
   models. Keep distinct point subtypes only where value/runtime behavior needs
   them; ownership must not depend on a second hierarchy.
4. Replace separate source and point validators with aggregate validation:
   validate source and connection, mapping IDs and communications, point IDs
   and type rules, mapping-path resolution, operation compatibility, and global
   point-ID uniqueness.
5. Add a single resolver that parses a mapping path and returns source,
   mapping, and alias. Runtime, tests, compilation, and configuration guidance
   must use it instead of reimplementing string splitting.
6. Route all mapping template validation and rendering through
   `ITemplateService`; extend that interface only if a typed object/text render
   contract is genuinely missing. Preserve template diagnostics with mapping
   and operation locations.
7. Delete `PointSourceType.cs`, the TypeScript `PointSourceType` mirror,
   `AutomationPoint.PointSourceType`, `IAutomationPoint.PointSourceType`, and
   source-type fields in `ValidatedPointDefinition`, `PointAvailability`, and
   execution DTOs. Replace source-type switches in validation, runtime reads,
   compilation, execution configuration, flow-node defaults, and UI labels with
   owning-source resolution and source-kind behavior. Remove the
   `AutomationPointJsonConverter` and physical/virtual/remote derived records if
   their only remaining purpose is this obsolete discriminator.
8. Remove other obsolete converters, mapping records, configuration kinds,
   tolerant defaults, and legacy parse branches once no current contract uses
   them.

Verification: serializer round trips are exact; strict rejection tests cover
all removed shapes; aggregate validation tests cover every source/value kind,
shared mappings, bad references, capabilities, and template diagnostics; the
solution compiles without the removed contracts.

### Phase 3 — Add exhaustive point-source and point-definition validator tests (complete)

1. Add focused unit-test fixtures and tests under
   `backend/Server/Tests.Unit/Points/Validation`. Exercise
   `PointSourceValidator` and `PointDefinitionValidator` directly rather than
   relying on endpoint or persistence tests to reach their behavior.
2. Create a YAML fixture matrix containing every Cartesian combination of the
   five version-1 `PointSource.Kind` values (`virtual`, `physical`,
   `homeAssistant`, `mqtt`, and `httpJson`) and the five
   `AutomationPoint.ValueType` values (`analog`, `digital`, `multiState`,
   `integer`, and `text`). Provide at least one valid and one invalid aggregate
   fixture for each of the 25 combinations. Keep each source-kind connection
   and mapping valid while varying the point contract so failures identify the
   value-type rule under test; add kind-specific negative variants separately
   where the mapping or connection is the intended failure.
3. Give every fixture a declared expected outcome and stable diagnostic
   fragment or category. Positive fixtures must parse and pass both applicable
   validators. Negative fixtures must state whether strict YAML parsing,
   aggregate/source validation, mapping resolution, or point-definition
   validation is expected to reject the document, and tests must assert that
   rejection occurs at that boundary.
4. Cover source validation beyond the matrix: identifiers, required names,
   credentials, connection URLs and schemes, TLS requirements, timeout and
   response bounds, MQTT QoS/topics, kind-discriminated mapping fields,
   duplicate mapping/point IDs, duplicate and case-sensitive aliases, malformed
   and unknown mapping paths, readable/commandable operation compatibility,
   template syntax/output aliases, and global point-ID uniqueness across
   aggregates.
5. Cover all value-type and shared point-definition rules, including required
   and forbidden limits, finite numeric ranges and ordering, digital and
   multi-state labels, multi-state cardinality/index rules, integer bounds,
   text length bounds, direction/readable/commandable combinations,
   persistence, relinquish defaults, and safe-disable policies. Add boundary
   cases immediately below, at, and immediately above each numeric or length
   limit where applicable.
6. Parameterize fixture discovery so every YAML file is executed and fail the
   suite when a fixture lacks an expectation or an expectation names a missing
   fixture. Assert explicitly that the matrix contains all 25 kind/value-type
   cells in both the positive and negative sets, preventing silent coverage
   gaps when kinds or value types are added.
7. Reuse the normative Phase 1 fixtures where they express the exact scenario,
   but keep validator-specific fixtures isolated from endpoint and persistence
   setup. Use a real `ITemplateService` for integration-level template cases
   and a controlled test double for deterministic propagation of template
   diagnostics.

Verification: all 25 source-kind/value-type combinations have passing positive
and negative YAML cases; direct tests cover every branch of
`PointSourceValidator` and `PointDefinitionValidator`; every invalid Phase 1
point-source fixture is consumed and rejected for its declared reason; fixture
discovery and completeness checks pass; focused backend validation tests pass
without a database or HTTP server.

### Phase 4 — Replace persistence and aggregate services (complete)

1. Replace separate `PointSourceEntity` and `PointEntity` persistence with one
   source aggregate representation and its owned mapping/point data. Choose
   normalized owned tables or reviewed JSON columns according to existing data
   conventions, but expose one aggregate repository transaction.
2. Enforce unique source IDs and globally unique point IDs in both validation
   and the database. Ensure mapping and alias uniqueness within their parent.
3. Replace `IPointSourceService` plus `IPointDefinitionStore` write behavior
   with one aggregate service. Create/update validates and writes the source,
   mappings, and points atomically; failed validation or concurrency leaves no
   partial children.
4. Rebuild point catalogue and runtime ownership lookup as read projections
   over the aggregate store. Update flow dependency checks, credential usage
   checks, startup validation, execution configuration, and audit descriptions
   to traverse aggregate-owned points.
5. Make deletion enforce flow/credential/runtime dependencies and then remove
   the complete aggregate. Define deterministic behavior for active pollers and
   commands during update/delete.
6. Edit the single pre-release `InitialCreate` migration and model snapshot to
   describe only the new schema. Delete superseded migration artifacts if any
   were created while developing. Do not generate an old-to-new migration or
   copy legacy rows.

Verification: data tests create a fresh database, exercise atomic aggregate
CRUD, concurrency, global point uniqueness, cascades/dependency rejection, and
catalogue lookup. Assert that the EF model contains no legacy point-source
foreign-key fields, standalone point-definition context values, or old tables.

### Phase 5 — Implement mapping execution and point operations

1. Introduce a source-kind adapter boundary with operations for connectivity,
   mapping read, and mapping command. Implement it for every current kind,
   including virtual and physical behavior.
2. For reads, execute once per mapping, build the documented template model,
   invoke `ITemplateService`, validate the rendered alias object, and convert
   each referenced alias through its consuming point contract. Propagate common
   transport quality/timestamp and per-point conversion diagnostics.
3. For commands, validate the typed point value, convert it to invariant raw
   text, render the selected mapping command through `ITemplateService`, and
   execute once. Preserve safe-disable, relinquish, authorization, audit, and
   response-size/network protections.
4. Update polling/subscription coordination so shared mappings do not create
   duplicate external reads. Atomically publish all successfully converted
   point updates from a mapping while retaining an explicit diagnostic for an
   alias that fails conversion.
5. Update `PointReadService`, flow point adapters, retained virtual state, and
   execution/deployment resolution to use aggregate ownership. Ensure flow
   compilation still receives the same normalized point contracts it needs.

Verification: adapter/service unit tests cover each source kind, multiple
aliases from one read, multiple points sharing a mapping/alias, all conversions,
partial conversion failure, one-call scheduling, command rendering, readback,
cancellation, communication loss, and disabled/safe-disable behavior.

### Phase 6 — Cut over HTTP endpoints and configuration guidance

1. Change point-source create/get/update to consume and return the full
   aggregate YAML and change list summaries to include counts. Preserve body
   limits, strict content handling, ETags, redaction, and error mapping.
2. Remove standalone point mutation and definition endpoints. Retain only the
   read-only point catalogue and point runtime endpoint, backed by projections.
3. Change unsaved point testing to accept `{ sourceYaml, pointId, operation,
   value }`; add saved point testing at the proposed nested route. Both paths
   call the same aggregate validator, resolver, and adapter service.
4. Keep source connectivity tests independent from point tests. Ensure an
   unsaved aggregate can be tested without persistence and a saved test cannot
   be tricked into using client-supplied connection or credential data.
5. Maintain the point-source approach in the configuration guide: guidance is
   requested for a point-source aggregate and is discriminated by `kind`.
   Generate contextual sections for connection, mappings, communications,
   templates, and nested points. When a point is selected, show its value-type
   fields and resolved mapping/alias in the same guide; do not reintroduce a
   standalone point guide or fetch a separate source to explain a point.
6. Update endpoint registration, startup wiring, mutation auditing, API access
   policy, and architecture boundary tests. Delete old request contracts such
   as separate `SourceYaml` plus `PointYaml` testing.

Verification: endpoint tests cover aggregate CRUD and ETags, deletion
dependencies, catalogue/runtime projections, saved and unsaved point tests,
every status code, payload bounds, cancellation, redaction, SSRF policy, and
strict rejection of every old endpoint/body. Guidance coverage tests require
help for every current aggregate field and source kind.

### Phase 7 — Replace the frontend source and point workflows

1. Update `pointSourceSchema.ts` and strict DTO parsing for the complete
   aggregate, including kind-discriminated mappings, nested points, mapping
   reference syntax, and all source/value kinds. Remove standalone point schema
   ownership and legacy DTO fields.
2. Make `AppPointSourceEditorView.vue` the only create/edit/delete surface for
   definitions. Add repeatable mapping and point sections, source-kind-aware
   examples, clear validation locations, and safeguards for deleting a mapping
   or point that is still referenced.
3. Keep the point-source list and routes. Show point/mapping counts and preserve
   source filtering, pagination, revision conflicts, and delete confirmation.
   Remove standalone point creation/edit routes and actions.
4. Keep `AppPointListView.vue` as a read-only catalogue for discovery and flow
   authoring. Link a point to its owning source editor and expose source name,
   mapping path, direction, value type, and runtime status. Do not offer direct
   definition mutation from this view.
5. In the source editor's test area, populate a selector from nested points.
   Selecting a point determines whether Read and/or Command is available and
   the command input/editor for its type. The frontend sends only the aggregate
   or saved source identity plus the selected point ID; it never asks the user
   to paste a second point YAML document or manually choose a mapping.
6. Display typed result, quality, resolved mapping/alias, communication stages,
   redacted previews, and diagnostics. Preserve cancellation, stale-response
   suppression, destructive-command confirmation, accessibility, and focus
   behavior.
7. Update configuration-guidance requests/components to send the full source
   aggregate and optional selected point ID. Update navigation, stores, flow
   point pickers, server types, API clients, and error handling for the new
   read-only catalogue and aggregate writes.

Verification: component/API/store tests cover editing every kind, nested
mapping/point changes, strict parsing, revision conflicts, selected-point read
and command, typed inputs/results, disabled operations, cancellation, and
guidance. Browser tests cover create, edit, test, flow selection, delete
dependency failure, and successful aggregate deletion with keyboard and screen
reader assertions.

### Phase 8 — Remove legacy artifacts, update documentation, and verify

1. Delete standalone point serializers/documents, services, database entities,
   mutation endpoints, frontend editors/stores/routes, compatibility helpers,
   split fixtures, and implementation-coupled tests. Replace useful behavioral
   coverage with aggregate tests; delete tests whose only purpose was preserving
   an obsolete contract.
2. Search C#, TypeScript/Vue, fixtures, docs, migrations, and generated schemas
   for `PointDocument`, `PointYaml`, `IPointDefinitionStore`, `PointEntity`,
   `PointSourceType`, `pointSourceType`, `sourceId`, `pointYaml`, standalone
   point mutation routes, and the old `sources` wrapper. Every remaining
   occurrence must be justified as a negative rejection test or historical
   document.
3. Update the configuration guide and all current point/source API,
   architecture, development, operations, and testing documentation. Preserve
   the source-led explanation: users create a source, define its mappings, then
   define and test its points. Update examples for every kind, not only
   HTTP/JSON.
4. Regenerate only reviewed source artifacts. Do not commit build output, test
   results, local databases, or copied legacy fixtures.
5. Run formatting/static checks, all backend and frontend unit tests, browser
   E2E and accessibility tests, and relevant controller/host tests. Start the
   application against a brand-new database and execute the acceptance journey
   below.

Verification: all suites pass; a repository search finds no live legacy
contract; the application starts from an empty database; documentation and UI
examples validate against the same version-1 fixtures.

## Acceptance journey

The final end-to-end test should perform this sequence without fixtures that
bypass the public API:

1. Create an HTTP/JSON source with two mappings. One read response is rendered
   to at least two aliases and used by analog and integer/digital points.
2. Save and reload the source, confirming one aggregate revision and exact
   normalized nested mappings/points.
3. Run source connectivity testing and confirm it does not mutate point state.
4. Select a readable point and test Read. Confirm the server automatically
   resolves the mapping and alias, makes one communication call, converts the
   result to the point value type, and shows typed value and quality.
5. Select a commandable point, enter a typed value, confirm the warning, and
   test Command. Confirm the rendered command uses the selected alias and the
   external adapter receives the expected payload.
6. Add a second point that shares the first mapping, then confirm a mapping read
   updates both points without a duplicate external request.
7. Select the points in a flow and exercise server execution/runtime reads to
   prove point IDs still resolve independently of the UI aggregate.
8. Attempt to delete the source while referenced by the flow and observe a
   conflict. Remove the dependency, delete the source, and confirm its mappings,
   points, pollers/subscriptions, and catalogue entries are gone.
9. Submit each removed version-1 legacy shape and confirm strict rejection;
   confirm no migration or compatibility behavior is invoked.

Repeat focused contract/adapter coverage for MQTT, Home Assistant, physical,
and virtual sources and for every point value type.

## Completion criteria

Implementation is complete only when:

- every point belongs to one persisted point-source aggregate;
- every point resolves a strict `<mapping-id>/<value-alias>` reference;
- a mapping can expose multiple aliases and serve multiple points with one
  communication operation;
- transport aliases remain raw text until converted by the consuming point;
- all template validation and rendering goes through `ITemplateService`;
- every existing communication kind and point value type remains supported;
- aggregate CRUD is atomic and protected by one revision;
- standalone point definition CRUD, storage, schemas, and UI are gone;
- `PointSourceType` and all independently stored or transported point source
  classification are gone; source behavior is selected from the owning
  source's kind;
- point catalogue/runtime access and flow references still work by point ID;
- source and selected-point read/command testing work for saved and unsaved
  aggregates and automatically use the point's mapping;
- configuration guidance remains source-led and covers mappings and points;
- all schemas are version 1, with strict rejection of the removed version-1
  shapes;
- the fresh database model contains only the new structure and no migration or
  legacy context code exists; and
- backend, frontend, browser, accessibility, controller/host, and fresh-start
  verification passes.
