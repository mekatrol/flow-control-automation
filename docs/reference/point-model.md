# Point model

A point is a typed value boundary used by flows and operators. Configuration ownership is simple:
every point is nested under exactly one `PointSource`, and the source aggregate is the unit of
validation, persistence, revision control, and deletion.

The owning source `kind` identifies virtual, physical, Home Assistant, MQTT, or HTTP/JSON
behavior. A point does not carry `sourceId`, `pointSourceType`, or an inline protocol object.
Catalogue projections may expose the owning `sourceKind`, but it is derived rather than persisted.

## Mappings

A source owns reusable mappings. Mapping communication fields are discriminated by source kind,
and `aliases` declares the raw values produced or accepted by that mapping. A point selects exactly
one value using `mapping-id/valueAlias`. Multiple points may share a mapping or alias; polling and
subscriptions therefore operate per mapping, not per point.

Deleting a source deletes its mappings and point definitions together, unless a flow or protected
dependency refers to a nested point. Nested points cannot be independently moved or deleted.

## Point contract

Each point has identity and display metadata, `direction`, `valueType`, `readable`, `commandable`,
`persistence`, and its mapping path. Type-specific fields include numeric units/limits, digital or
multi-state labels, text length, relinquish defaults, and command safety policy. The five value
types are analog, digital, multi-state, integer, and text.

Runtime values additionally carry quality, source timestamp, receipt timestamp, reliability,
communication state, and diagnostics. A transport value is converted through the consuming point
contract. Conversion failure produces bad quality and is never silently coerced.

The exact mapping fields, template contexts, escaping, and raw conversion rules are defined in
`docs/architecture/point-source-v1-contract.md` and exercised by the canonical fixtures.
