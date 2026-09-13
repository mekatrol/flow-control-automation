# Version 1 configuration vocabulary

The machine-readable JSON Schema is
`testdata/contracts/point-sources/point-source-v1.schema.json`; it validates YAML after parsing it
to the JSON data model and is also used by the browser editor. Canonical examples are under
`testdata/contracts/point-sources/valid`. Each file
contains one complete source aggregate with its connection, mappings, and points. There is no
`sources` wrapper and no standalone point document.

| Contract | Wire values |
| --- | --- |
| Source kind | `virtual`, `physical`, `homeAssistant`, `mqtt`, `httpJson` |
| Point direction | `input`, `output`, `inputOutput`, `value` |
| Point value type | `analog`, `digital`, `multiState`, `integer`, `text` |
| Point persistence | `volatile`, `retained` |

Enum values and aliases are case-sensitive. Source, mapping, and point identifiers are lowercase
hyphenated identifiers. Point IDs are globally unique. A mapping is reusable and declares its
aliases; each nested point selects one with `mapping-id/alias`.

Transport YAML/JSON contains `schemaVersion: 1` but excludes `revision`, `createdAt`, and
`updatedAt`. Those fields belong to the stored/API aggregate only. Unknown fields, legacy
ownership fields, aliases/anchors, duplicate keys, and other schema versions are rejected.

JSON Schema covers document shape, discriminated source/mapping fields, and point-type field
shapes. Cross-record rules—global point-ID uniqueness, mapping-path resolution, operation
compatibility, template output aliases, numeric ordering, and retained-value rules—remain
authoritative server validation and are described by the architecture contract.

Analog and integer map to numeric connectors, digital to boolean, and multi-state/text to string.
Matching connector primitives do not erase point limits, units, labels, or conversion rules.
