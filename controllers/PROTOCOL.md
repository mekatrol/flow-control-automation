# Flow Controller Protocol version 1

## 1. Status and purpose

This document is the normative design for the bespoke Flow Controller Protocol
(FCP). It supports discovery and diagnostics, typed point access, authenticated
commands, and resumable transfer of compiled flow deployments.

FCP version 1 is implemented incrementally by Phase 9 of
`IMPLEMENTATION_PLAN.md`. Phase 9A framing, discovery, device interrogation,
health, echo, and read-only point dispatch are implemented. Authentication,
flow transfer, commands, and subscriptions remain planned and must not be
advertised until their dispatcher and provider satisfy this contract.

The words **must**, **must not**, **should**, and **may** are normative. Numeric
multi-byte fields use little-endian order. Receivers decode bytes explicitly and
must not cast unaligned wire buffers to native C structures.

## 2. Design principles

- The protocol codec is independent of RS485, UART, ESP-IDF, TCP, CAN, MQTT,
  storage media, physical I/O drivers, and the flow evaluator.
- Point definitions, runtime values, commands, editable flow graphs, compiled
  deployments, active generations, and evaluator state remain distinct.
- Runtime point data carries type, quality, reliability, service state, and
  timestamps with its value. Missing or bad data never becomes an implied zero.
- Editable YAML, browser JSON, credentials, and live UI state are not deployed
  to controllers. The backend produces an immutable validated artifact.
- CRC detects accidental corruption. Authentication and authorization protect
  mutations. CRC is not a security mechanism.
- Frames, fields, queues, sessions, transfers, retries, diagnostics, and provider
  work are bounded and observable.

```text
RS485/TCP/CAN transport
        |
        v
frame codec -> authentication/session service -> dispatcher
                                                  |-- device provider
                                                  |-- point provider
                                                  `-- flow deployment store
```

## 3. RS485 transport profile

The initial profile uses 8 data bits, no parity, one stop bit, automatic
hardware direction, and configurable baud defaulting to 115200 bps. Controller
addresses are unsigned 16-bit values. Address `0` is valid unicast and is the
factory default. Address `0xffff` is broadcast.

An inter-frame silent interval delimits transport frames. The length and CRC
remain authoritative; a timeout boundary alone does not make a valid message.
The version-1 maximum encoded frame is 256 bytes. Larger logical objects use
chunk operations. A transport with larger native frames retains this limit
unless a later negotiated profile explicitly changes it.

One initiator sends requests. A controller must not respond to another unicast
address. Invalid magic, impossible length, truncated data, trailing data, or bad
CRC receives no response because address and transaction fields are untrusted.
Such failures increment local counters.

Broadcast is allowed only for operations marked broadcast-safe. Discovery uses
collision avoidance. Other broadcast requests do not receive responses, and
mutations must never be broadcast.

The initial physically connected RS485 profile permits unauthenticated local
output commands as an explicit deployment policy. This does not apply to
routable transports or flow/configuration mutations. Operators must treat
physical RS485 access as control access and secure the cabinet and bus wiring.

## 4. Frame format

| Offset | Size | Field          | Meaning                                     |
| ------ | ---- | -------------- | ------------------------------------------- |
| 0      | 2    | magic          | `0x46 0x43` (`FC`)                          |
| 2      | 1    | version        | Major protocol version, initially `1`       |
| 3      | 1    | flags          | Request/response/error/authentication flags |
| 4      | 2    | destination    | Destination address                         |
| 6      | 2    | source         | Sender address                              |
| 8      | 2    | transaction    | Caller-selected correlation value           |
| 10     | 1    | operation      | Operation code                              |
| 11     | 2    | payload length | Payload byte count                          |
| 13     | N    | payload        | Operation-specific data                     |
| 13+N   | 2    | CRC-16         | CRC over header and payload                 |

The minimum frame is 15 bytes and maximum payload is 241 bytes. CRC is
CRC-16/Modbus: polynomial `0xa001`, initial value `0xffff`, reflected input and
output, no final XOR. The low CRC byte is encoded first.

### 4.1 Flags

| Bit | Name          | Rule                                         |
| --- | ------------- | -------------------------------------------- |
| 0   | response      | Zero for requests and one for responses      |
| 1   | error         | Valid only with `response`                   |
| 2   | authenticated | Payload has the authenticated envelope       |
| 3   | more          | More pages, chunks, or events are available  |
| 4-7 | reserved      | Must be zero                                 |

Responses copy request transaction and operation. Error responses set response
and error and use the error payload in section 7.

## 5. Primitive encodings

- `u8`, `u16`, `u32`, `u64`, `i64`: fixed-width little-endian integers.
- `f64`: IEEE-754 binary64, little-endian, finite values only.
- `bool`: one byte containing exactly `0` or `1`.
- `bytes8`: `u8` byte length followed by bytes.
- `bytes16`: `u16` byte length followed by bytes.
- `string8` and `string16`: corresponding byte length followed by valid UTF-8
  without NUL. Length counts bytes, not characters.
- `timestamp`: signed 64-bit Unix epoch milliseconds; `INT64_MIN` means absent.
- `digest256`: exactly 32 SHA-256 bytes.

Strings are bounded by the payload and their field-specific limit. Invalid
UTF-8 and unknown enum values are rejected, not coerced.

## 6. Point vocabulary

### 6.1 Value types

| Value | Canonical type | Value encoding             |
| ----- | -------------- | -------------------------- |
| 1     | `analog`       | `f64`                      |
| 2     | `digital`      | `bool`                     |
| 3     | `multi_state`  | `string8` stable state key |
| 4     | `integer`      | `i64`                      |
| 5     | `text`         | `string16`                 |

### 6.2 Quality

| Value | Meaning   |
| ----- | --------- |
| 0     | good      |
| 1     | uncertain |
| 2     | bad       |

Reliability is a separate stable `u16` reason. Zero means no detected fault.
New reasons require a published registry; clients preserve unknown numbers for
diagnostics.

### 6.3 Service flags

| Bit | Meaning     |
| --- | ----------- |
| 0   | enabled     |
| 1   | in service  |
| 2   | readable    |
| 3   | commandable |
| 4   | overridden  |
| 5   | in alarm    |
| 6-7 | reserved    |

## 7. Error responses

The payload is:

```text
error_code:u16, detail_code:u16, retry_after_ms:u32, diagnostic:string8
```

Diagnostics are bounded and contain no secrets, keys, authentication tags, raw
corrupt payload, or platform paths.

| Code | Name                  |
| ---- | --------------------- |
| 1    | malformed             |
| 2    | unsupported_version   |
| 3    | unsupported_operation |
| 4    | wrong_state           |
| 5    | invalid_argument      |
| 6    | not_found             |
| 7    | not_ready             |
| 8    | unsupported           |
| 9    | unauthorized          |
| 10   | forbidden             |
| 11   | replay                |
| 12   | busy                  |
| 13   | queue_full            |
| 14   | storage_unavailable   |
| 15   | storage_full          |
| 16   | revision_conflict     |
| 17   | digest_mismatch       |
| 18   | validation_failed     |
| 19   | safety_rejected       |
| 20   | internal_error        |

## 8. Device operations

| Opcode | Name                   | Authentication | Broadcast |
| ------ | ---------------------- | -------------- | --------- |
| `0x01` | echo                   | no             | no        |
| `0x02` | discover               | no             | yes       |
| `0x03` | get capabilities       | no             | no        |
| `0x04` | get device information | policy         | no        |
| `0x05` | get health             | policy         | no        |

Echo returns the exact payload and does not interpret an embedded address.

Discovery request fields are `nonce:u32, slot_count:u8, slot_time_ms:u16`.
Each controller calculates `CRC16(stable_device_identity || nonce) %
slot_count`, waits that many slots, and responds with configured address,
non-secret device ID, hardware model, and firmware version.
Slot count must be nonzero and bounded. A host retries with more slots after a
collision. Factory identity is not returned unless identity policy permits it.

Capabilities return protocol minor version, frame/chunk limits, authentication
algorithms, operation bitmap, point types, artifact versions, and controller
limits. Extension fields are ignored only when their capability schema permits
extension.

### 8.1 Implemented Phase 9A payloads

The discovery request uses this payload:

| Offset | Size | Field        | Meaning                              |
| ------ | ---- | ------------ | ------------------------------------ |
| 0      | 4    | nonce        | Caller-selected discovery nonce      |
| 4      | 1    | slot count   | Number of collision-avoidance slots  |
| 5      | 2    | slot time ms | Duration of each slot in milliseconds |

Discovery and device-information responses use this payload:

| Offset   | Size | Field            | Meaning                         |
| -------- | ---- | ---------------- | ------------------------------- |
| 0        | 2    | address          | Configured controller address   |
| 2        | N    | device ID        | Non-secret `string8` identity   |
| 2+N      | M    | hardware model   | `string8` board model           |
| 2+N+M    | K    | firmware version | `string8` firmware version      |

Capabilities use this fixed payload:

| Offset | Size | Field            | Meaning                                      |
| ------ | ---- | ---------------- | -------------------------------------------- |
| 0      | 1    | minor version    | Implemented protocol minor version           |
| 1      | 2    | frame limit      | Maximum encoded frame size                   |
| 3      | 2    | payload limit    | Maximum payload size                         |
| 5      | 1    | bitmap size      | Number of operation-bitmap bytes             |
| 6      | 3    | operation bitmap | Bit `opcode % 8` in byte `opcode / 8`        |
| 9      | 1    | point-type mask  | Bit `type - 1` for each supported point type |

Health returns eleven consecutive `u32` counters in this order: accepted
frames, bad magic, bad version, bad flags, bad length, bad CRC, address misses,
unsupported operations, provider errors, response drops, and duplicate
transactions.

## 9. Point operations

| Opcode | Name                 | Authentication | Mutation |
| ------ | -------------------- | -------------- | -------- |
| `0x10` | list points          | policy         | no       |
| `0x11` | get point definition | policy         | no       |
| `0x12` | get point value      | policy         | no       |
| `0x13` | subscribe changes    | policy         | no       |
| `0x14` | point change event   | session        | no       |
| `0x15` | get I/O block        | policy         | no       |
| `0x18` | command point        | no on RS485    | yes      |
| `0x19` | relinquish command   | required       | yes      |
| `0x1a` | command output block | no on RS485    | yes      |

Point IDs are canonical bounded strings, not numeric register aliases.
Pagination uses an opaque bounded continuation token and stable order within a
definition revision.

A runtime-value response contains:

```text
point_id:string8
definition_revision:u32
value_type:u8
typed_value:variant
units:string8
quality:u8
reliability:u16
service_flags:u8
source_timestamp:timestamp
updated_at:timestamp
sequence:u32
```

These fields form one snapshot. A bad or missing value must not become zero,
false, empty text, or good quality.

The implemented KC868-A16 provider exposes `input-01` through `input-16`, then
`output-01` through `output-16`. Each is a digital point. Inputs and outputs are
converted from the board's active-low electrical representation to logical
`false`/`true` values.

The get-I/O-block request has an empty payload. Its response is one coherent
cached sample:

| Offset | Size | Field          | Meaning                                      |
| ------ | ---- | -------------- | -------------------------------------------- |
| 0      | 2    | inputs         | Bits 0-15 represent logical inputs 1-16      |
| 2      | 2    | outputs        | Bits 0-15 represent logical outputs 1-16     |
| 4      | 1    | validity flags | Bit 0 inputs valid; bit 1 outputs valid      |
| 5      | 8    | sampled at     | Monotonic sample timestamp in milliseconds   |
| 13     | 4    | sequence       | Increasing cached sample sequence            |

The single-output command payload is `point_id:string8, value:bool`. Only
`output-01` through `output-16` are accepted. The response repeats the validated
payload. The block-output command payload and response are a `u16` bitmap where
bit 0 controls output 1 and bit 15 controls output 16. A set bit means active.
The two PCF8574 banks are written in channel order; because they are separate
devices, a bus fault can leave the first bank updated and the second unchanged.
The next block read reports the observed state.

Commands contain point ID, typed value, stable source ID, command class,
priority, correlation ID, issue timestamp, optional expiry, and reason. The
provider validates type, units, limits, permissions, service state, safety, and
command arbitration before success.

Subscriptions are bounded and events use increasing sequences. Overflow
creates an explicit gap; clients resynchronize by reading current values.

## 10. Authentication and replay protection

| Opcode | Name                     |
| ------ | ------------------------ |
| `0x30` | authentication challenge |
| `0x31` | authentication prove     |
| `0x32` | close session            |

Version 1 uses HMAC-SHA-256 with a separately provisioned device-bound protocol
key. Terminal and MQTT passwords must not be reused.

Challenge/prove establishes a bounded session ID, expiry, negotiated
capabilities, and initial sequences. Each authenticated payload begins:

```text
session_id:u32, sequence:u64, body:bytes, tag:32 bytes
```

The tag covers a protocol domain separator, complete header with final payload
length, session ID, sequence, and body. Responses are authenticated too.
Sequences strictly increase independently in each direction. Repeated or lower
sequences are rejected before mutation. Tags use constant-time comparison.
Challenges and sessions expire; session count and attempts are bounded. Address
or major-version changes invalidate a session.

Except for the explicitly local RS485 output-command policy in section 3,
mutations require a valid session plus operation, peer, and provider permission.
Read-only authentication policy is explicit and may be tightened by deployment.

## 11. Compiled deployment artifact

The backend compiles a validated immutable controller artifact. It does not
send editable YAML, browser graph state, backend persistence JSON, credentials,
live telemetry, or unsupported nodes for controller-side guesswork.

The header contains artifact magic/schema, flow ID/revision, controller-template
ID/revision, referenced point/source revisions, execution mode/interval,
node/connection counts, required capabilities/limits, payload length, and
SHA-256 digest.

The body uses a separately versioned deterministic encoding. Exact bytecode and
evaluator representation must receive a specification and golden fixtures when
the controller evaluator is designed. FCP transfer treats it as opaque bytes;
the deployment validator owns semantics.

## 12. Flow operations

| Opcode | Name                 | Authentication |
| ------ | -------------------- | -------------- |
| `0x40` | list flows           | policy         |
| `0x41` | get flow metadata    | policy         |
| `0x42` | upload begin         | required       |
| `0x43` | upload status/resume | required       |
| `0x44` | upload chunk         | required       |
| `0x45` | upload validate      | required       |
| `0x46` | upload commit        | required       |
| `0x47` | upload abort         | required       |
| `0x48` | download begin       | required       |
| `0x49` | download chunk       | required       |
| `0x4a` | activate flow        | required       |
| `0x4b` | deactivate flow      | required       |
| `0x4c` | remove flow          | required       |
| `0x4d` | get flow runtime     | policy         |

Upload begin supplies flow ID, revision, artifact schema, total length,
SHA-256, and optional expected current revision. The response contains transfer
ID, accepted chunk limit, storage availability, and resume state.

Each chunk contains transfer ID, absolute offset, length, bytes, and chunk CRC.
Ranges stay within the declaration. Exact duplicate chunks are idempotent;
conflicting overlap rejects the transfer. Progress is persisted according to
advertised resume capability.

Validation verifies complete coverage and SHA-256 before invoking artifact
validation. Diagnostics carry bounded stable field/node paths and reason codes.
Commit atomically publishes a durable inactive generation. Activation is a
separate operation and atomically selects a validated generation. Power loss
leaves the previous generation or complete new generation, never partial data.

Download returns exact stored bytes and metadata in bounded chunks. Removal
cannot silently remove a running flow or referenced state.

## 13. Idempotency

Clients do not reuse an outstanding transaction ID. Controllers retain a
bounded recent-request cache per peer/session. Repeating the same transaction
with identical authenticated content returns its cached result without
repeating mutation. Reuse with different content is invalid or replay.

After cache expiry, transfer IDs, offsets, flow IDs, revisions, and digests keep
chunk and commit operations idempotent. After ambiguous timeout, clients query
status instead of assuming failure and creating a second deployment.

## 14. Compatibility

Major versions are incompatible and never guessed. Minor features use
capability and operation bitmaps. Reserved fields/bits are zero and rejected
when nonzero unless negotiated otherwise.

Released enum values and opcodes are permanent. Deprecated operations remain
decodable for their documented window. New point types, authentication
algorithms, artifact schemas, or frame profiles require negotiation.

## 15. Resource, security, and safety rules

- Protocol work never waits indefinitely for peers, storage, I/O, networking,
  MQTT, or evaluation.
- Slow providers run outside transport callbacks and return pending or busy when
  they exceed the service budget.
- Secrets and authentication material never enter diagnostics or health.
- A CRC-valid message may still be unauthenticated, unauthorized, unsafe, or
  semantically invalid.
- Unknown, unavailable, stale, and bad-quality points remain explicit.
- Output commands require runtime safety and arbitration; protocol support alone
  must never enable physical writes.
- Upload completion, durable commit, validation, activation, and execution are
  separate states and responses.

## 16. Conformance testing

C firmware and host clients share published golden frames, CRC vectors, and
artifact fixtures. Tests cover codec round trips, malformed data,
authentication/replay, duplicates, every point type/quality, interrupted and
resumed transfers, atomic commit, unknown versions, saturation, and multi-drop
collisions.

The RS485 hardware suite uses the Linux Mint Waveshare adapter for discovery,
capabilities, device information, health, point reads, authenticated flow
transfer, bad CRC, wrong address, reconnect, and peer restart while heartbeat,
networking, MQTT, and terminal diagnostics remain responsive.
