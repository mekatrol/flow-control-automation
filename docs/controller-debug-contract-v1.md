# Controller debug lifecycle and snapshot contract v1

## 1. Safety and ownership

A debug session is volatile, authenticated, single-owner, and distinct from
durable upload staging and the committed generation. Debug operations must not
call commit, activate, deactivate, remove, durable storage, or physical output
arbitration. Schema 1 supports shadow mode only.

Exactly one session may exist. A new authenticated load may replace it only
when `replace_existing` is true; replacement first performs the same bounded
cleanup as stop. Controller reboot always starts in `empty`.

## 2. Lifecycle

States and wire values are `empty=0`, `loading=1`, `ready=2`, `stepping=3`,
`paused=4`, `fault=5`, `stopped=6`, and `running=7`.

```text
empty --load--> loading --complete+validate+prepare--> ready
ready --step--> stepping --tick committed--> paused
ready|paused --run--> running --pause--> paused
loading|stepping --failure--> fault
ready|paused --preparation/evaluation failure--> fault
loading|ready|stepping|paused|running|fault --stop/expiry/replacement--> stopped
stopped --cleanup complete--> empty
```

`stepping` is never externally interruptible halfway through a tick; stop is
recorded and applied after the bounded tick finishes. Requests invalid for the
current state fail with FCP `wrong_state` and do not change it. A step is valid
from `ready` or `paused`. Fault retains its last complete snapshot, if any,
until stop or expiry. No operation resumes a faulted session. Pause freezes
memory and all evaluator state; inputs are not latched while paused, so the
next step or scheduled tick samples a fresh coherent input image.

## 3. Authentication, lease, and identity

Every operation uses the FCP authenticated envelope and requires the debug
permission. The session ID is a controller-generated non-zero `u64`, unique
within a boot. Requests bind both the authenticated FCP session and debug
session ID; another principal receives `forbidden`, while a stale or absent ID
receives `not_found` without revealing the owner.

The lease is 30000 ms. Successful authenticated load, status, prepare, step,
snapshot read, or renewal by the owner resets the deadline. Failed requests,
transport traffic, and unauthenticated reads do not. Disconnect alone changes
nothing. At deadline, the controller enters stopped, zeroes artifact, prepared
state and snapshots, then enters empty. Cleanup completes within 100 ms. The
lease value is returned by load/status and cannot be extended beyond the fixed
bound by one request.

## 4. FCP operations

These opcodes extend FCP version 1. All are authenticated, unicast-only, and
mutating except status and snapshot reads.

| Opcode | Name | Request body | Success body |
| --- | --- | --- | --- |
| `0x50` | debug load begin | `client_request_id:u32, replace_existing:bool, artifact_length:u32, artifact_sha256:32` | `debug_session_id:u64, chunk_limit:u16, lease_ms:u32` |
| `0x51` | debug load chunk | `session_id:u64, offset:u32, data:bytes` | `offset:u32, accepted_length:u16` |
| `0x52` | debug prepare | `session_id:u64` | status payload |
| `0x53` | debug status | `session_id:u64` | status payload |
| `0x54` | debug step | `session_id:u64` | `tick_number:u64, snapshot_length:u32, snapshot_sha256:32` |
| `0x55` | snapshot header | `session_id:u64, tick_number:u64` | snapshot header below |
| `0x56` | snapshot chunk | `session_id:u64, tick_number:u64, chunk_index:u16` | chunk payload below |
| `0x57` | renew lease | `session_id:u64` | `lease_ms:u32` |
| `0x58` | debug stop | `session_id:u64` | `stopped_session_id:u64` |
| `0x59` | debug run | `session_id:u64, interval_ms:u32` | debug status |
| `0x5a` | debug pause | `session_id:u64` | debug status |

Load chunks permit exact idempotent overlap and reject conflicting overlap.
Prepare requires full coverage, verifies whole-artifact SHA-256, then validates
and prepares it. Status is:

```text
session_id:u64, state:u8, covered_bytes:u32, artifact_length:u32,
flow_revision:u32, tick_number:u64, lease_remaining_ms:u32,
last_reason_code:u16, last_reason_path:string8(63)
```

The operation body plus the authenticated envelope must fit FCP's 241-byte
payload. Consequently the negotiated `chunk_limit` may not exceed 180 bytes.

## 5. Immutable snapshot wire schemas

The snapshot byte stream is canonical and immutable. A header response is:

```text
session_id:u64, tick_number:u64, total_length:u32, chunk_count:u16,
chunk_data_limit:u16, snapshot_sha256:32
```

A chunk response is:

```text
session_id:u64, tick_number:u64, chunk_index:u16, chunk_count:u16,
absolute_offset:u32, data:bytes
```

Chunks are zero-indexed, fixed at the header's `chunk_data_limit` except the
last, and together cover the stream exactly once. Session, tick, count, offset,
length, and digest must all agree. Wrong ticks return `not_found`; snapshots
are never substituted or truncated. The controller retains the latest complete
snapshot only. A new step may begin after the prior snapshot is published; a
reader using the old tick then gets `not_found`, never mixed data.

The reassembled stream begins:

```text
schema:u16 (=1 or 2), session_id:u64, flow_id:string8(63), revision:u32,
lifecycle_state:u8, mode:u8, tick_number:u64, sampled_at_ms:u64,
completed_at_ms:u64, execution_duration_us:u32, input_validity:u8,
node_count:u16, proposed_output_count:u16, overrun_count:u32,
evaluation_failure_count:u32, last_reason_code:u16,
last_reason_path:string8(63)
```

Schema 2 appends `execution_high_water_us:u32` and
`missed_deadline_count:u32` to this header before the node records. Schema 1
remains decodable for compatibility. Continuous snapshots use
`mode=fixed_interval(2)` and lifecycle `running(7)`; manual snapshots use
`mode=manual(1)`.

It is followed by `node_count` node records, then output records. Input-validity
bits are bit 0 coherent, bit 1 all present, and bit 2 all good; bits 3-7 are
zero. Controller monotonic timestamps are used and completed is not earlier
than sampled.

Node record:

```text
node_id:string8(63), state:u8, quality:u8, value_type:u8, value_present:bool,
typed_value:variant
```

Output record:

```text
point_id:string8(63), state:u8, quality:u8, proposed_value:bool
```

Node/output state is `idle=0`, `evaluated=1`, `fault=2`, or `unavailable=3`.
Quality is `good=0`, `uncertain=1`, `bad=2`, or `unavailable=3`. Value type uses
the FCP vocabulary; schema 1 snapshot values are digital (`2`) and encode one
strict Boolean byte only when present. A good evaluated node must have a value;
a missing value must not be rendered as false.

## 6. Backend JSON and UI rendering

JSON uses camelCase field names matching the conceptual fields above. The
`debugSessionId` is the wire `u64` rendered as a canonical unsigned decimal
string so JavaScript cannot lose precision. Flow/node/point IDs, states,
qualities, and reason names are strings. Revisions, ticks, timestamps,
durations, and counters are JSON numbers limited to JavaScript's safe integer
range (wire values above that bound are rejected). A typed value is a
discriminated object, never an untyped scalar:

```json
{"type":"digital","value":true}
```

`typedValue` is `null` when absent. `nodes` and `proposedOutputs` are arrays in
artifact order. The backend rejects duplicate IDs, an unknown value type,
non-finite numbers, inconsistent session/tick metadata, digest failure, excess
counts, or a partial chunk set before publishing JSON.

The UI renders digital values as `On`/`Off` by default, optionally substituting
the point's configured digital labels. It renders missing values as an em dash,
never `Off`. Uncertain and bad quality remain visibly labelled. Proposed
outputs use `Proposed On`/`Proposed Off` and an always-visible `Shadow — not
physical` label. A snapshot is applicable only when session ID, flow ID, and
flow revision match the currently displayed graph; otherwise it is stale and
must not decorate nodes.

## 7. Backend JSON Schema

The normative machine-readable JSON Schema is
`testdata/contracts/debug-snapshot.schema.v1.json`. It additionally freezes all
string, array, integer, enum, and typed-value bounds stated here.
