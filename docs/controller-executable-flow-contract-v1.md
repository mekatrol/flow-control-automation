# Controller executable flow contract v1

## 1. Status and compatibility

This schema is frozen as the graph-shaped controller-debug compatibility
format. It proves deterministic compilation and portable evaluation, but its
preparation step still constructs a Kahn schedule on the receiver. Production
cross-target execution will use scheduled Flow IL v2 as planned in
[`portable-flow-il-architecture.md`](portable-flow-il-architecture.md).
Schema 1 must not be changed or reinterpreted to obtain v2 behavior.

This document is the normative contract for executable flow envelope schema 1
and body schema 1. The controller decoder is the reference implementation.
All multi-byte integers are unsigned little-endian. Receivers decode fields
explicitly and reject trailing bytes, non-zero reserved fields, invalid UTF-8,
embedded NULs, unknown enum values, and non-canonical order.

The complete artifact is at most 8192 bytes. Envelope and body versions are
negotiated independently. A receiver that does not advertise both versions
must reject the artifact with `unsupported_schema`.

## 2. Primitive and identifier encoding

- `u8`, `u16`, `u32`, and `u64` are fixed-width little-endian integers.
- `bool` is one byte and is exactly 0 or 1.
- `string8(max)` is a `u8` byte count followed by that many UTF-8 bytes.
- Stable flow, node, port, and point IDs contain 1-63 bytes and match
  `[A-Za-z0-9][A-Za-z0-9._:-]*`.
- Controller-template IDs contain 1-31 bytes and use the same grammar.
- IDs compare by their unsigned UTF-8 bytes, not locale or Unicode collation.

No identifier is rewritten by the compiler. In particular, each source node
ID is encoded verbatim and is the node ID returned in snapshots and paths.

## 3. Envelope schema 1

The envelope is exactly 192 bytes. Its SHA-256 covers only the exact body bytes;
FCP transfer metadata separately carries SHA-256 of the complete artifact.

| Offset | Size | Field | Required value or bound |
| ---: | ---: | --- | --- |
| 0 | 4 | magic | ASCII `FCEX` |
| 4 | 2 | envelope schema | 1 |
| 6 | 2 | body schema | 1 |
| 8 | 2 | envelope length | 192 |
| 10 | 2 | flags | 0 |
| 12 | 4 | artifact length | 192-8192 |
| 16 | 4 | flow revision | 1-`UINT32_MAX` |
| 20 | 4 | template revision | 1-`UINT32_MAX` |
| 24 | 1 | execution mode | section 6 |
| 25 | 1 | input-quality policy | section 7 |
| 26 | 2 | reserved | 0 |
| 28 | 4 | interval ms | 0 in manual mode; otherwise 10-60000 |
| 32 | 2 | node count | 1-128 |
| 34 | 2 | port count | 1-384 |
| 36 | 2 | connection count | 0-384 |
| 38 | 2 | point-reference count | 0-64 |
| 40 | 4 | required capabilities | section 8 |
| 44 | 4 | maximum snapshot bytes | 1-16384 |
| 48 | 1 | flow ID length | 1-63 |
| 49 | 63 | flow ID bytes and zero padding | canonical ID, then zeroes |
| 112 | 1 | template ID length | 1-31 |
| 113 | 31 | template ID bytes and zero padding | canonical ID, then zeroes |
| 144 | 16 | reserved | all zero |
| 160 | 32 | body SHA-256 | digest of bytes 192 through artifact end |

`artifact length` must equal the received byte count. Declared counts must
equal body table counts. `maximum snapshot bytes` is the compiler's exact
upper-bound estimate using the snapshot wire encoding; it must not exceed the
controller limit.

## 4. Body schema 1

The body begins with a 24-byte directory followed by four packed tables.
Offsets are relative to the first body byte, point to their table count, are
strictly increasing, and are at least 24. The end of one parsed table must be
the start of the next, and the point table must end at `body_length`.

| Offset | Size | Field |
| ---: | ---: | --- |
| 0 | 4 | body length, including directory |
| 4 | 4 | node-table offset |
| 8 | 4 | port-table offset |
| 12 | 4 | connection-table offset |
| 16 | 4 | point-table offset |
| 20 | 4 | reserved, zero |

Every table starts with `count:u16`, followed immediately by its records. No
alignment padding is used.

### 4.1 Node records

`node_id:string8(63), opcode:u8, configuration_length:u16, configuration:bytes`

Nodes are sorted by node ID. Configuration bytes and port shape are exact:

| Opcode | Kind | Ports | Configuration |
| ---: | --- | --- | --- |
| 1 | digital input | `value` output | `point_index:u16` |
| 2 | digital constant | `value` output | `value:bool` |
| 3 | Boolean NOT | `in` input, `value` output | empty |
| 4 | Boolean AND | `a`,`b` inputs, `value` output | empty |
| 5 | Boolean OR | `a`,`b` inputs, `value` output | empty |
| 6 | one-tick memory | `in` input, `value` output | `initial_value:bool` |
| 7 | proposed digital output | `in` input | `point_index:u16, source:u8, priority:u8, expiry_ms:u32` |

For schema 1, output source is 1 (`debug`), priority is 8, and expiry is 0
(`shadow/no command`). These fields are nevertheless encoded so a future live
profile cannot silently inherit defaults.

### 4.2 Port records

`node_index:u16, port_id:string8(63), direction:u8, value_type:u8, arity:u8,
reserved:u8`

Direction is 1 input or 2 output. Value type is 2 (`digital`, matching FCP).
Arity is 1. Records are sorted by node index, direction (input first), then port
ID. A node's records must exactly match the opcode table above.

### 4.3 Connection records

`source_node_index:u16, source_port_index:u16, target_node_index:u16,
target_port_index:u16`

Port indices address the complete port table. Source ports must be outputs,
target ports inputs, and types must match exactly. Each input has exactly one
connection. Connections are sorted by target node ID, target port ID, source
node ID, then source port ID. Duplicate records and fan-in to one input are
invalid.

### 4.4 Point-reference records

`point_id:string8(63), direction:u8, value_type:u8, quality_policy:u8,
reserved:u8`

Direction is 1 read or 2 proposed-write. Type is digital. Quality policy is 1
(`require_good`) in schema 1. Records are sorted by point ID, then direction.
Node point indices address this table. A digital-input node references a read
point and an output node references a proposed-write point. Referenced points
must exist in the target template with matching direction and type.

## 5. Deterministic preparation and tick semantics

Preparation validates the entire artifact before allocating runtime state.
Memory outputs are cycle-breaking sources. Remove edges whose target is a
memory input, then apply Kahn topological sorting with node-ID byte order as the
only ready-queue tie breaker. A remaining strongly connected component is a
`combinational_cycle`.

Schema-1 compatibility execution follows the PLC Scan Cycle defined in
[`plc-scan-cycle.md`](plc-scan-cycle.md). One tick is one all-or-nothing scan:

1. **Read Inputs:** capture one coherent input image, timestamp, and current
   memory state.
2. **Execute Logic:** publish every memory node's current value (using its
   encoded initial value on scan 1) and evaluate the fixed schedule into a
   next-value buffer. Boolean operators use
   ordinary truth tables. Proposed-output nodes record a value but never call
   physical output arbitration.
3. Still within Execute Logic, capture each memory input into a next-memory
   buffer. Missing or bad required input faults the whole tick.
4. Construct one immutable completed-scan snapshot from evaluated node values.
5. **Write Outputs:** atomically publish the snapshot and next memory state,
   then increment the tick number. On failure neither changes.

Thus a memory node snapshot shows the value used during that tick; the value
captured at the end of tick N first appears in tick N+1. Preparation and stop
restore encoded initial values. Evaluation performs no allocation and is
bounded by the declared counts.

## 6. Execution modes

| Value | Name | Schema-1 debug acceptance |
| ---: | --- | --- |
| 1 | manual | accepted; interval must be 0 |
| 2 | fixed interval | encoded but rejected as `unsupported_mode` initially |

## 7. Input quality

Quality uses FCP values `good=0`, `uncertain=1`, and `bad=2`; snapshot-only
`unavailable=3` represents no sample. Schema 1 accepts only artifact policy 1,
`require_good`. An uncertain, bad, unavailable, wrong-type, or incoherent input
causes `input_quality_rejected` and leaves the tick uncommitted.

## 8. Capability bits and fixed limits

| Bit | Capability |
| ---: | --- |
| 0 | manual step |
| 1 | digital nodes |
| 2 | one-tick memory |
| 3 | shadow proposed outputs |
| 4 | chunked snapshots |

Required bits are 0, 1, and 4, plus bit 2 or 3 when those node kinds occur.
Unknown bits are rejected. Limits are artifact 8192 bytes, 128 nodes, 384
ports, 384 connections, 64 point references, 64 proposed outputs, 16384
snapshot bytes, and 63 bytes for every runtime ID and diagnostic path.

## 9. Stable validation results

Validation returns the first error in this order: envelope framing, directory
framing, table decoding, canonical encoding, references/type/shape, target
resolution, graph/schedule, resource estimate. Paths use ASCII JSON Pointer
syntax over decoded tables, for example `/nodes/not-main/configuration` or
`/connections/3/target`. IDs in paths use their artifact bytes without aliases.

| Code | Name | Typical path |
| ---: | --- | --- |
| 0 | ok | empty |
| 1 | malformed | offending field |
| 2 | unsupported_schema | `/envelopeSchema` or `/bodySchema` |
| 3 | length_mismatch | `/artifactLength` or body directory field |
| 4 | digest_mismatch | `/bodySha256` |
| 5 | limit_exceeded | declared field |
| 6 | invalid_identifier | record ID |
| 7 | non_canonical_order | offending table record |
| 8 | unknown_node_kind | node path |
| 9 | invalid_configuration | node configuration path |
| 10 | invalid_port_shape | node ports path |
| 11 | missing_connection | input port path |
| 12 | duplicate_driver | input port path |
| 13 | incompatible_type | connection path |
| 14 | missing_point | point path |
| 15 | point_direction_mismatch | point path |
| 16 | combinational_cycle | lexically first node in cycle |
| 17 | unsupported_mode | `/executionMode` |
| 18 | unsupported_capability | `/requiredCapabilities` |
| 19 | snapshot_too_large | `/maximumSnapshotBytes` |
| 20 | input_quality_rejected | runtime node path |
| 21 | evaluation_failed | runtime node path |

Codes and meanings are permanent within schema 1. Fixtures identify results by
both number and name; implementations must preserve unknown future codes.
