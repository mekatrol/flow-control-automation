# Controller flow source contract v1

## 1. Status and role

This is the normative contract for controller flow source schema 1: the
bounded, target-independent graph accepted by the backend compiler. The
compiler lowers it to executable envelope/body schema 1 as specified by
[`controller-executable-flow-contract-v1.md`](controller-executable-flow-contract-v1.md).

This contract is distinct from persisted flow-designer JSON. The designer has
a larger legacy catalogue and presentation fields. An adapter must translate a
supported editable graph into this source contract. Unsupported nodes must
produce a diagnostic and must never be approximated silently. The golden
`source-flow.json` files under `testdata/contracts/flow-executable-v1/` are
schema-1 examples.

## 2. Document shape

The JSON object has exactly these fields:

```text
schemaVersion: 1
id: identifier
revision: uint32, 1..UINT32_MAX
controllerTemplateId: template identifier
controllerTemplateRevision: uint32, 1..UINT32_MAX
execution: execution object
nodes: node array
connections: connection array
```

Unknown fields are rejected. Property order is insignificant. Identifiers use
the byte bounds and grammar from executable contract section 2. Integers must
be JSON integers in range. The template ID and revision are assertions: the
resolved target must match both before compilation.

## 3. Execution

The execution object has exactly `mode`, `intervalMs`, and
`inputQualityPolicy`. Schema 1 accepts only `manual`, `0`, and `require_good`,
respectively. Other values are `unsupported_execution`.

## 4. Nodes

`nodes` contains 1-128 records with exactly `id`, `kind`, and `configuration`.
IDs are unique. Source order is insignificant; nodes are sorted by node-ID
UTF-8 bytes.

| Source kind | Exact configuration | Inputs | Outputs | Opcode |
| --- | --- | --- | --- | ---: |
| `digitalInput` | `pointId:string` | none | `value:digital` | 1 |
| `digitalConstant` | `value:boolean` | none | `value:digital` | 2 |
| `not` | empty | `in:digital` | `value:digital` | 3 |
| `and` | empty | `a:digital`, `b:digital` | `value:digital` | 4 |
| `or` | empty | `a:digital`, `b:digital` | `value:digital` | 5 |
| `memory` | `value:boolean` | `in:digital` | `value:digital` | 6 |
| `digitalOutput` | `pointId:string` | `in:digital` | none | 7 |

Missing and unknown configuration fields are rejected. `memory.value` is the
initial value restored by preparation and stop. `digitalOutput` always emits
source `debug`, priority 8, and expiry 0; source schema 1 cannot request a
physical output command.

An input point must be enabled, readable, digital, and input-direction. An
output point must be enabled, commandable, digital, and output-direction.
Multiple nodes may read one input; at most one output node may target a given
point.

## 5. Connections

A connection has exactly `source` and `target`; each endpoint has exactly
`nodeId` and `portId`. Endpoints must name the fixed ports above. Connections
run output-to-input, every input has exactly one driver, and duplicates or
fan-in are rejected.

Source order is insignificant. Connections are sorted by target node ID,
target port ID, source node ID, then source port ID using UTF-8 byte order.
Memory inputs break scheduling cycles; all remaining cycles are rejected.

## 6. Canonical lowering

Compilation is deterministic:

1. Validate the complete source and resolved target.
2. Sort nodes and connections canonically.
3. Collect unique point references and sort by point ID, then direction.
4. Emit fixed ports; source documents cannot redefine port shapes.
5. Calculate capability bits and the exact snapshot upper bound.
6. Encode and hash body schema 1, then encode envelope schema 1.
7. Return the complete artifact digest and unchanged node IDs mapped to their
   canonical node-table indices.

Labels, coordinates, connection IDs, and other presentation metadata never
enter the artifact. Inputs differing only in array or property order produce
byte-identical artifacts.

## 7. Bounds

Schema 1 permits 128 nodes, 384 ports, 384 connections, 64 unique point
references, 64 proposed outputs, an 8192-byte artifact, and a 16384-byte
snapshot. Counts are checked before encoding and sizes afterward. Compilation
must not perform persistence or controller transport.

## 8. Diagnostics

Failures contain stable `code` and JSON Pointer `path`, plus a human-readable
`message` whose wording is not contractual.

| Code | Typical path |
| --- | --- |
| `invalid_source` | offending field |
| `unsupported_source_schema` | `/schemaVersion` |
| `invalid_identifier` | `/nodes/0/id` |
| `duplicate_node` | `/nodes/1/id` |
| `unsupported_node` | `/nodes/0/kind` |
| `invalid_configuration` | `/nodes/0/configuration` |
| `invalid_endpoint` | `/connections/0/source` |
| `missing_connection` | `/nodes/node-id/ports/in` |
| `duplicate_driver` | `/connections/1/target` |
| `incompatible_type` | `/connections/0` |
| `missing_point` | `/points/point-id` |
| `point_direction_mismatch` | `/points/point-id` |
| `target_mismatch` | `/controllerTemplateId` or `/controllerTemplateRevision` |
| `unsupported_execution` | `/execution` or a child field |
| `combinational_cycle` | `/nodes/node-id` |
| `limit_exceeded` | offending collection or estimate |

Controller validation reason codes remain separate and are not replaced by
these compiler diagnostics.

## 9. Editable graph adapter

The current designer model is not source schema 1. Its existing Boolean nodes
use legacy generic connector shapes. It also lacks the complete point,
constant, and memory editor nodes. The adapter must preserve node IDs, map
supported nodes and ports exactly, discard presentation fields, obtain
revisions from authoritative backend state, and reject ambiguous mappings.

Until that adapter is specified and implemented, the compiler accepts only the
canonical source contract represented by the golden fixtures.
