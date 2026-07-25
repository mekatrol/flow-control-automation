# Version 1 configuration vocabulary

This file is the compact parity reference for the fixtures under
`testdata/contracts`. The detailed semantics remain in `point-types.md`,
`flows.md`, and `implementation-plan.md`.

## Canonical enum spellings

| Contract | Values |
| --- | --- |
| Point implementation | `virtual`, `bound` |
| Point direction | `input`, `output`, `input_output`, `value` |
| Point value type | `analog`, `digital`, `multi_state`, `integer`, `text` |
| Point persistence | `volatile`, `retained` |
| Point source kind | `home_assistant`, `mqtt`, `http_json` |
| Connector data type | `any`, `boolean`, `event`, `number`, `string` |
| Connector direction | `input`, `output` |
| Execution mode | `event`, `interval` |

Enum values are case-sensitive wire values. User-facing labels may differ, but
YAML, normalized JSON, Go, and TypeScript use these spellings.

## Point-to-connector mapping

| Point value type | Connector data type |
| --- | --- |
| `analog` | `number` |
| `integer` | `number` |
| `digital` | `boolean` |
| `multi_state` | `string` |
| `text` | `string` |

Matching connector primitives do not imply compatible point contracts. Analog
and integer values remain distinct, and numeric point units must match exactly
unless an explicit conversion function is introduced.

## Fixture rules

- YAML files are canonical user configuration.
- JSON files represent normalized internal persistence.
- Revisions and timestamps appear only in normalized JSON.
- Fixtures deliberately cover standalone and grouped points, all initial point
  value types, virtual and bound implementations, and all initial source kinds.
- Files under `invalid/` must remain rejected as the strict parsers are added.

