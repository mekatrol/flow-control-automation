# Version 1 configuration vocabulary

The canonical point-source fixtures are under `testdata/contracts/point-sources/valid`. Each file
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

Analog and integer map to numeric connectors, digital to boolean, and multi-state/text to string.
Matching connector primitives do not erase point limits, units, labels, or conversion rules.
