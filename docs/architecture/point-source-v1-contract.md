# Point-source aggregate architecture and configuration contract, version 1

## Contract and ownership

A point source is the aggregate root for configuration, validation, persistence, optimistic
concurrency, and deletion. Each YAML document describes exactly one source and contains its
connection, reusable mappings, and owned points. There is no `sources` wrapper, standalone point
document, compatibility reader, or legacy ownership field. Existing pre-unification YAML and
development databases must be recreated rather than migrated.

The machine-readable schema is
[`testdata/contracts/point-sources/point-source-v1.schema.json`](../../testdata/contracts/point-sources/point-source-v1.schema.json).
It is a JSON Schema because YAML uses the JSON data model after parsing. The browser editor and
external tooling consume this same file. Normative examples and normalized JSON live under
`testdata/contracts/point-sources/valid`; server validation remains authoritative for semantic
rules that JSON Schema cannot express across records.

Every point is nested under exactly one source. A point has neither `sourceId` nor
`pointSourceType`; the owning `kind` selects `virtual`, `physical`, `homeAssistant`, `mqtt`, or
`httpJson` behavior. Source and point IDs are globally unique. Mapping IDs are source-local,
aliases are mapping-local and case-sensitive, and identifiers match
`^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$`. A point selects one alias using exactly
`<mapping-id>/<alias>`. Multiple points may share a mapping or alias.

Transport YAML contains `schemaVersion: 1` and excludes server-owned `revision`, `createdAt`, and
`updatedAt`. Unknown fields, duplicate keys, YAML aliases/anchors, legacy fields, and unsupported
versions are rejected. The aggregate parser produces `PointSource` directly; separate point
documents and tolerant legacy parse branches no longer exist.

## Mappings and typed values

Mapping fields are strictly discriminated by source kind:

- `virtual` contains local volatile or retained options and empty read/command markers.
- `physical` contains controller, channel, optional address, and electrical metadata.
- `homeAssistant` uses entity/property reads and service/service-data commands.
- `mqtt` uses state and command topics, QoS, retain behavior, format, and templates.
- `httpJson` uses relative paths, safe read methods, command methods, format/content type,
  templates, polling overrides, response bounds, redirects, and private-network policy.

A readable point requires `read`; a commandable point requires `command`. The shared resolver
validates both mapping-path segments and returns the source, mapping, and alias. Compilation,
runtime access, guidance, and saved/unsaved tests all use it.

Adapters exchange transport text and do not infer the consuming point type. Read templates receive
the adapter response model and render one JSON object containing every declared alias exactly once
and no undeclared alias. Command templates receive the selected alias as a top-level value. All
validation and rendering passes through `ITemplateService`; standard member renaming, JSON
escaping, and redacted diagnostics apply. Command responses are diagnostic only; readback requires
a read operation.

Raw values convert at the point boundary:

- analog: finite invariant decimal;
- integer: invariant base-10 whole number in the JSON safe-integer range;
- digital: exactly lowercase `true` or `false`;
- multi-state: an exact, case-sensitive configured state key;
- text: unchanged Unicode text within `limits.maximumLength`.

Null never converts. Whitespace is significant for text and trimmed otherwise. Limits, units,
state labels, persistence, relinquish defaults, direction/capability rules, and safe-disable
policies are validated by the point contract. Conversion failure produces bad quality and a
diagnostic; values are never silently coerced or defaulted.

## Persistence, runtime, and testing

The database stores one source row with owned mapping and point data. Create and update validate
and write the complete graph atomically. Updating any child increments the source revision;
`If-Match` protects updates and the revision query protects deletion. Database and service checks
enforce globally unique point IDs. Deleting a source removes mappings and points together, but is
rejected while a flow or protected dependency references a point. Nested points cannot be moved
or deleted independently.

The point catalogue and runtime ownership lookup are projections over source aggregates. Runtime
lookup by point ID resolves source, mapping, and alias, then dispatches to the source-kind adapter.
Polling and subscriptions run per mapping. One read updates all consumers with the same source
timestamp and communication quality while converting each independently. Commands are type-
checked, serialized to invariant text, rendered once, and dispatched once. Virtual retained
values use the retained store; missing adapters report unavailable quality.

Source connectivity tests are non-mutating protocol checks. Point tests return source, point,
mapping, alias, operation, typed value when available, redacted previews, stages, timing, quality,
and a structured diagnostic. Unsaved tests submit the edited aggregate plus `pointId`; saved tests
load the authoritative aggregate. Tests are bounded, cancellable, rate-limited, SSRF-safe,
credential-redacted, audited where required, and never persist runtime values. Command tests keep
explicit authorization and confirmation.

## HTTP and frontend workflow

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

Aggregate routes read or write complete YAML; lists return summaries and mapping/point counts.
`GET /api/points` is the read-only catalogue for flow authoring and operators. Standalone point
write routes do not exist. Invalid YAML, aggregates, or operations return `400`; missing resources
return `404`; concurrency/dependency conflicts return `409`; forbidden commands or network access
return `403`; and upstream failures return `502`.

The frontend is source-led: the source list opens one schema-backed aggregate editor with examples
for all kinds, save/concurrency handling, source testing, and nested point tests. The point list is
a catalogue linked to the owning source. Guidance accepts the complete aggregate and optional
selected point, and covers mappings, templates, conversion, credentials, TLS/network policy,
capabilities, and safe testing.

## Verification contract

Backend and frontend tests consume the same canonical fixtures and reject removed shapes. The
validator matrix covers all 25 source-kind/value-type combinations in positive and negative forms,
plus identity, security, mapping discrimination, aliases, operations, templates, limits, safety,
and uniqueness. Persistence tests cover atomic replacement, concurrency, dependency protection,
projection, and restart behavior; endpoint and browser tests cover the complete source-led
lifecycle. The implementation has no legacy ownership model, standalone point write path, or
duplicate handwritten editor schema.
