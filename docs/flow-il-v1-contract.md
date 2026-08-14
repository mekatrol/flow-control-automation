# Flow IL v1 binary and semantic contract

> Designer recovery: supported artifacts are decompiled by the backend
> `FlowDecompiler`, never by the VM or firmware. Current artifacts provide
> lossless recovery of executable behavior, labels, groups, and canvas layout.

## 1. Status, version handling, and canonical encoding

This document is normative for Flow IL version 1. Version 1 is a scheduled,
target-neutral instruction format and is the only supported IL version. Loaders
validate the explicit magic/version and reject every other value; they do not
contain fallback decoders or migration logic.

All integers are unsigned little-endian. Records are packed without implicit
alignment. Reserved bytes and fields are zero. Booleans are exactly zero or
one. Identifiers are `string8`: a one-byte UTF-8 length followed by 1-63 bytes
matching `[A-Za-z0-9][A-Za-z0-9._:-]*`. IDs compare by unsigned UTF-8 bytes.
No table contains trailing bytes. An artifact is at most 16384 bytes.

The canonical compiler resolves immutable source dependencies, sorts all
identity tables, removes edges into stateful memory nodes, and applies Kahn
ordering with node ID as its only ready-set tie breaker. It then allocates
transient node-result slots in schedule order and state slots in memory-node ID
order. Identical resolved inputs therefore produce byte-identical artifacts.

## 2. Envelope

The envelope is exactly 128 bytes.

| Offset | Size | Field | Contract |
| ---: | ---: | --- | --- |
| 0 | 4 | magic | ASCII `FIL1` |
| 4 | 2 | IL version | 2 |
| 6 | 2 | envelope length | 128 |
| 8 | 4 | artifact length | exact received length, at most 16384 |
| 12 | 4 | flags | bit 0: debug map present; other bits zero |
| 16 | 4 | flow revision | nonzero |
| 20 | 4 | template revision | nonzero |
| 24 | 2 | minimum host ABI | 1 |
| 26 | 2 | section count | 8 |
| 28 | 1 | execution profile | 1 manual, 2 fixed interval |
| 29 | 3 | reserved | zero |
| 32 | 4 | maximum work per tick | instruction count, nonzero |
| 36 | 8 | required capabilities | section 8 |
| 44 | 4 | VM working bytes | exact compiler estimate |
| 48 | 4 | maximum snapshot bytes | 1-16384 |
| 52 | 1 | flow ID length | 1-63 |
| 53 | 63 | flow ID and padding | ID then zeroes |
| 116 | 4 | section-directory offset | 128 |
| 120 | 8 | reserved | zero |

The directory immediately follows the envelope. Each 48-byte entry is
`section_id:u16, section_version:u16, offset:u32, length:u32, count:u32,
sha256:bytes[32]`. The digest covers the section's exact bytes, including an
empty section's standard SHA-256 value.
Entries have IDs 1 through 8 in ascending order, version 1 except the required
symbol section,
contiguous offsets,
and non-overlapping ranges. The final range ends at artifact length.

## 3. Sections

### 3.1 Typed constants — section 1

Records begin `type:u8, flags:u8, reserved:u16`. Type 1 appends no bytes and
stores its Boolean in `flags`; type 2 has zero flags and appends one canonical
little-endian finite IEEE-754 binary64 value. Values sort by type then value and
duplicates are invalid.

### 3.2 Point bindings — section 2

Records are `direction:u8, type:u8, quality_policy:u8,
binding_kind:u8, binding_id:string8, units:string8`. Direction is 1 read or 2
proposed write. Type 1 is Boolean and type 2 is finite binary64. Units are
bounded and empty only for unitless values.
Quality policy 1 requires good input. Binding kind 0 identifies an automation
point and kind 1 identifies a persisted flow-interface terminal. Records sort
by binding kind, binding ID, then direction. Interface writes are committed
shadow outputs and hosts must never publish them as physical point commands.
Bindings contain no credentials, driver address, or physical handle.

### 3.3 Slot layout — section 3

Each eight-byte record is `kind:u8, type:u8, flags:u16, slot_index:u16,
initial_constant:u16`. Kind 2 is a transient register, kind 3 Boolean memory,
kind 4 timer state, and kind 5 prior-value event state. Type 1 is Boolean and
type 2 is binary64. Indices are contiguous from zero.
Transient initial constant is `0xffff`; state references a compatible constant.
Slots are caller-owned and fully allocated before execution.

### 3.4 Scheduled instructions — section 4

Each 12-byte instruction is `opcode:u8, flags:u8, result:u16, operand0:u16,
operand1:u16, auxiliary:u16, reserved:u16`. Unused indices are `0xffff`. An
instruction may read only a compatible slot made available by an earlier
instruction or current state. Result slots are written exactly once.

| Opcode | Name | Result | Operands / auxiliary |
| ---: | --- | --- | --- |
| 1 | read point | Boolean transient | auxiliary: read binding |
| 2 | Boolean constant | Boolean transient | auxiliary: constant |
| 3 | Boolean NOT | Boolean transient | operand0 |
| 4 | Boolean AND | Boolean transient | operand0, operand1 |
| 5 | Boolean OR | Boolean transient | operand0, operand1 |
| 6 | load current state | Boolean transient | auxiliary: state slot |
| 7 | propose output | Boolean transient | operand0, auxiliary: write binding |
| 8 | stage next state | none | operand0, auxiliary: state slot |
| 9 | Boolean NAND | Boolean transient | operand0, operand1 |
| 10 | Boolean NOR | Boolean transient | operand0, operand1 |
| 11 | Boolean XOR | Boolean transient | operand0, operand1 |
| 12 | Boolean XNOR | Boolean transient | operand0, operand1 |
| 13 | binary64 constant | numeric transient | auxiliary: constant |
| 14 | binary64 add | numeric transient | operand0, operand1 |
| 15 | compare | Boolean transient | numeric operands; auxiliary: LT/LTE/EQ/GTE/GT/NE |
| 16 | level shift | numeric transient | operand0; gain and offset constants |
| 17 | quality is good | Boolean transient | operand0 |
| 18 | on-delay timer | Boolean transient | operand0; auxiliary: timer state |
| 19 | rising-edge event | Boolean transient | operand0; auxiliary: prior-value state |
| 20 | minimum | numeric | operand0, operand1 |
| 21 | maximum | numeric | operand0, operand1 |
| 22 | clamp | numeric | operand0; operand1: minimum constant; auxiliary: maximum constant |
| 23 | select | typed value | operand0: Boolean condition; operand1/auxiliary: value slots |
| 24 | copy | typed value | operand0 |
| 255 | commit tick | none | all unused; final instruction exactly once |

Opcode flags are zero. Unknown opcodes, wrong operand shapes/types, forward
transient reads, duplicate writes, work beyond the envelope bound, and any
instruction after commit are invalid.

All Boolean combinators are total, unitless, propagate the already-admitted
good quality of their operands, allocate no state bytes, perform one bounded
work unit, and write one Boolean slot into snapshots. NAND and NOR negate AND
and OR respectively; XOR is true exactly when operands differ and XNOR is true
exactly when operands are equal. These definitions contain no overflow or
platform-dependent behavior.

Numeric values are finite IEEE-754 binary64 values. Addition requires identical
units. Level shifting produces `input * gain + offset`, with unitless gain and
an offset in the input unit. NaN, infinity, and non-finite arithmetic results
fault the scan before commit. Comparisons are exact binary64 comparisons.

Quality is `0 good, 1 bad`; unary operations preserve quality and binary
operations use the worse operand quality. Envelope policy 1 rejects bad input;
policy 2 propagates it. Opcode 17 produces a good-quality Boolean describing
its operand quality. Snapshots carry type, quality, Boolean, and binary64
storage explicitly. Each stateless opcode costs one work unit.

On-delay uses captured monotonic milliseconds, a bounded 64-bit start timestamp,
and a 64-bit duration. It resets while false and cannot expire when time moves
backward. Rising-edge stores one prior Boolean and is true for exactly the scan
where false changes to true. Their next state publishes only at commit, each
costs one work unit, and each snapshot result is Boolean.

### 3.5 Commit plan — section 5

Each eight-byte record is `kind:u8, flags:u8, target:u16, source_slot:u16,
policy:u16`. Kind 1 stages next state and targets a state slot. Kind 2 stages a
proposed point command and targets a write binding. Flags are zero. Records
sort by kind then target and cover every state/output staged by instructions.

### 3.6 Symbols — section 6

Records are `instruction_index:u16, discriminator:u8, node_id:string8`.
Instruction indices are ascending. The compiler-assigned discriminator is zero
for a node's main instruction and nonzero for additional lowering instructions.
The commit instruction has an empty node ID encoded as length zero. Stable node
identity, not a byte offset, is the public diagnostic and breakpoint key.

The symbol record includes `label:string8, x:f64, y:f64, zOrder:f64,
group_id:string8` to every
record. Labels are bounded UTF-8 and coordinates are finite. The current
prerelease profile requires this metadata for lossless label and canvas
recovery, including optional stable group membership.

### 3.7 Debug map — section 7

Records are `instruction_index:u16, result_slot:u16, node_id:string8` and sort
by instruction index. `result_slot` may be `0xffff` for a non-value instruction.
The section is required by the current debug profile.

### 3.8 Source dependencies — section 8

Records are `kind:u8, dependency_id:string8, revision:u32` and sort by kind then
ID. Kind 1 identifies the controller template and kind 2 identifies a resolved
point definition. Future function/template dependencies receive new kinds.
Every revision is nonzero and makes artifact
reproduction auditable.

## 4. Capabilities and limits

Required-capability bits are: bit 0 Boolean slots, bit 1 point reads, bit 2
proposed outputs, bit 3 one-tick state, bit 4 debug maps, bit 5 expanded
Boolean combinators, bit 6 numeric values, bit 7 comparisons, bit 8 level
shifting, bit 9 quality, bit 10 timers, and bit 11 events. Unknown required bits are
rejected. A host advertises IL versions, ABI version, capabilities,
maximum artifact/section/instruction/slot/state/point/debug-map sizes, maximum
work per tick, breakpoint count, paused-frame bytes, and snapshot bytes.

The initial profile limits are 16384 artifact bytes, 8 sections, 256
instructions, 256 total slots, 128 state slots, 64 point bindings, 192 commit
records, 8192 debug-map bytes, 32 breakpoints, 32768 paused-frame bytes, and
16384 snapshot bytes. Maximum work is 256 instructions per tick. A target may
advertise smaller values but never larger values without a later capacity
profile.

Load rejects requirements before activation. Limits are checked again while
parsing; a compiler estimate never authorizes an out-of-bounds allocation.

## 5. Tick and state semantics

Flow IL uses the normative PLC Scan Cycle from `plc-scan-cycle.md`. One `tick`
in this binary/API contract means one complete scan:

1. **Read Inputs:** capture one coherent typed input frame and committed current
   state and freeze them for the scan.
2. **Execute Logic:** run instructions in encoded order into private working
   slots while state writes and output commands remain staged.
3. **Write Outputs:** the final commit atomically publishes next state, proposed
   commands, and one immutable completed-scan snapshot.

Missing/bad required input, invalid adapter response, cancellation, fault, or
abort before commit publishes nothing. State initialization uses its typed
constant; reset and cold activation restore it. Hosts never overlap scans for
one runtime. Inputs that change during Execute Logic are visible in a later scan.

Boolean operations use ordinary truth tables. There is no implicit coercion or
default for unavailable values. Arithmetic, strings, timers, and events require
future section/opcode versions with explicit determinism and bounds.

## 6. Stable loader results

Initial stable reasons and numeric values are:

| Value | Reason | Value | Reason |
| ---: | --- | ---: | --- |
| 0 | `ok` | 9 | `invalid_binding` |
| 1 | `malformed` | 10 | `invalid_slot` |
| 2 | `unsupported_version` | 11 | `unknown_opcode` |
| 3 | `length_mismatch` | 12 | `invalid_operand` |
| 4 | `non_canonical_order` | 13 | `invalid_commit_plan` |
| 5 | `unknown_section` | 14 | `unsupported_requirement` |
| 6 | `limit_exceeded` | 15 | `snapshot_too_large` |
| 7 | `invalid_identifier` |  |  |
| 8 | `invalid_constant` |  |  |

Paths use JSON-pointer-like locations such as
`/instructions/0/resultSlot`. Numeric values and meanings are never reused.

## 7. Golden fixtures

`testdata/contracts/flow-il-v1` contains valid Boolean, memory, source-order
permutation, maximum-count, truncated, bad-operand, unknown-section, and
noncanonical-directory artifacts. `tools/generate-flow-il-v1-fixtures.mjs
--check` recompiles without writing. The canonical and permuted graphs must be
byte-identical. C and .NET independently decode envelope/directory metadata and
agree with each fixture's `metadata.json` and validation result.
