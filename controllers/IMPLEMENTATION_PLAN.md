# Controller communications implementation plan

## Purpose

This plan tracks only unfinished work for the portable communications
foundation. Implemented capabilities are documented in [`FEATURES.md`](FEATURES.md),
the bespoke wire contract is defined in [`PROTOCOL.md`](PROTOCOL.md), and build
and commissioning instructions are in [`README.md`](README.md).

The flow evaluator and remote firmware updates remain outside this plan.
Compiled flow artifacts can be transferred and activated, but the current
schema is opaque and activation does not execute it.

## Architectural rules

- The platform entry point calls the shared `controller_main`, which performs
  bounded initialization, starts the controller runtime, and returns.
- Every communications subsystem is independently supervised. A failed
  subsystem reports degraded status and retries without blocking unrelated
  controller work.
- Queues, retries, payloads, topic lengths, diagnostic messages, sessions, and
  transfers are bounded.
- Credentials are provisioned through the authenticated terminal, loaded from
  typed persistent settings, and excluded from logs and committed defaults.
- Shared services depend only on platform and board contracts. Framework,
  processor, operating-system, and board headers remain in adaptation layers.
- State transitions expose read-only health information for diagnostics and
  future flow-runtime consumers.
- All code follows [`AGENTS.md`](AGENTS.md) and [`.clang-format`](.clang-format).

## Remaining implementation order

1. Complete Phase 8 on-target RS485 validation.
2. Complete Phase 10 integrated resilience and soak testing.

## Phase 8 — RS485 on-target validation

Implementation and portable tests are complete. The remaining work is physical
bus validation on the KC868-A16v3.

### Validation work

- Confirm and document the onboard transceiver's automatic direction
  behaviour; do not invent an RTS GPIO if the board controls direction
  automatically.
- Verify transmit and receive at every supported UART format using a loopback
  or second adapter.
- Exercise disconnect, malformed-frame, continuous-noise, collision, and peer
  restart cases and verify recovery and health counters.
- Run RS485 traffic while repeatedly interrupting MQTT and networking to prove
  subsystem isolation.

### Exit criteria

- RS485 reliably sends and receives framed data without blocking other runtime
  work.
- Controller diagnostics clearly expose RS485 state and errors.
- Physical failure and recovery results are recorded in the board commissioning
  documentation.

## Phase 10 — Integrated resilience and soak testing

### Deliverables

- Add one consolidated, immutable controller health snapshot consumed by
  diagnostics, MQTT status publishing, and the future flow runtime.
- Define watchdog ownership and prove all supervised tasks remain responsive.
- Add reason-coded counters for network, MQTT, RS485, protocol,
  authentication, transfer, queue, memory, storage, and task failures.
- Document field troubleshooting using the board diagnostic transport and
  observable MQTT status.

### Tests

- Run a minimum 24-hour soak with Wi-Fi, Ethernet, MQTT publish/subscribe,
  authenticated protocol, point reads, flow transfer, and RS485 traffic active.
- Inject access-point loss, cable loss, DHCP failure, DNS failure, broker
  restart, invalid MQTT credentials, RS485 disconnect/noise, and diagnostic
  reconnects.
- Verify the main task heartbeat never stops, watchdogs do not fire, queues
  stay within limits, subscriptions return, and free heap does not trend
  downward.
- Power-cycle at each degraded state and verify deterministic recovery.

### Exit criteria

- Every external dependency can fail and recover independently.
- The controller remains observable through its diagnostic transport
  throughout failure testing.
- The communications foundation is ready for flow-runtime implementation.

## Completion requirements

- Production code and focused automated tests are included together.
- Every supported board builds from the command line and VS Code tasks.
- On-target steps and expected diagnostics are documented.
- Failure paths are tested, not only successful startup.
- Configuration has safe empty defaults and contains no committed secret.
- Resource ownership, shutdown, retry, and queue limits are explicit.
- Completed work is moved to [`FEATURES.md`](FEATURES.md).
