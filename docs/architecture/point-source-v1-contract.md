# Point-source configuration contract, version 1

The normative examples live in `testdata/contracts/point-sources/valid`. A document is one
point-source aggregate; `sources` and standalone point documents are invalid. Identifiers use
`^[a-z][a-z0-9]*(?:-[a-z0-9]+)*$`. Source and point IDs are globally unique; mapping IDs are
source-local; aliases are mapping-local and case-sensitive. A point mapping is exactly
`<mapping-id>/<alias>` with no empty or additional segments.

## Kind-discriminated mappings

- `httpJson`: reads use relative `path`, safe `method`, `format`, and `template`; commands use a
  relative path, command method, format/content type, and template.
- `mqtt`: reads use `topic`, `qos`, and a payload template; commands add `retain`.
- `homeAssistant`: reads use `entityId` and `property`; commands use `service` and optional
  `serviceData`.
- `physical`: `physical` contains `controllerId`, `channel`, optional `address`, and optional
  `electricalType`; read/command presence declares supported operations.
- `virtual`: `virtual.persistence` selects volatile or retained local state. Local read and command
  sections contain no transport fields.

Fields belonging to another kind are errors. A readable point requires `read`; a commandable point
requires `command`.

## Template contexts and output

Read templates receive the adapter's response model directly. Public C# property names use the
standard member renamer (`Intensity` becomes `intensity`). They render one JSON object. Every
declared alias appears exactly once as a scalar or null and no undeclared key may appear. JSON-mode
rendering performs value escaping. Missing values and malformed or non-object output are errors.

Command templates receive a model whose selected mapping alias is exposed as a top-level property.
The standard member renamer applies, so a C# property such as `Intensity` is addressed as
`intensity`. Its value is the validated point command serialized according to the raw-text rules
below. Command responses are diagnostic only; readback requires a read operation.

## Raw-text conversion

- analog: finite invariant decimal.
- integer: invariant base-10 whole number in the JSON safe-integer range.
- digital: exactly lowercase `true` or `false`.
- multi-state: exact configured state key, case-sensitive.
- text: unchanged Unicode text, subject to `maximumLength`.

Null never converts. Whitespace is significant for text and trimmed for other types. Limits and
state labels apply after parsing. Failure produces bad quality and is never defaulted or coerced.
