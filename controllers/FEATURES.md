# Implemented features

This document describes capabilities currently implemented in the controller
firmware. Work that is not yet complete remains in
[`../docs/portable-flow-il-architecture.md`](../docs/portable-flow-il-architecture.md).

## Portable runtime and diagnostics

- A shared, non-blocking controller runtime starts its heartbeat before
  external communications are available.
- Structured diagnostics include severity, component, event code, monotonic
  timestamp, startup and reset information, and redacted configuration data.
- Diagnostic output, repeated-error rate limiting, suppressed-message counts,
  and bounded health snapshots keep failures observable without unbounded
  memory growth.

## Network management

- Wi-Fi and Ethernet use neutral link states and independent supervisors with
  bounded exponential backoff, jitter, stable-period retry reset, and explicit
  transition reasons.
- Each link retains its own interface identity, addresses, DNS state, retry
  count, and route eligibility. Both links can be represented concurrently
  without collapsing them into one connected flag.
- The Wi-Fi station adapter handles association, authentication, DHCP, address
  loss, manual reconnect, and empty-SSID disablement.
- The KC868-A16v3 Ethernet adapter supports its onboard W5500, including cable
  and DHCP loss recovery and address reporting. The current commissioning
  configuration is Ethernet-only; the Wi-Fi adapter remains available for a
  future explicit return to dual-link operation.

## MQTT

- MQTT sessions consume transport-independent route snapshots and reconnect
  after route, broker, DNS, TLS, or address changes without depending directly
  on Wi-Fi or Ethernet types.
- Typed broker settings cover identity, credentials, TLS, keepalive, session
  policy, queue limits, and reconnect timing.
- The bounded bidirectional API supports publishing, delivery results,
  telemetry coalescing, explicit offline policy, subscription registration,
  owned inbound payloads, resubscription, command correlation, availability,
  and health publishing.

## Persistent settings and terminal

- A typed settings service separates shared consumers from the selected
  storage implementation and preserves the distinction between missing,
  `null`, explicitly empty, valid, corrupt, and incompatible values.
- Settings initialization, credential updates, and configuration reset are
  transactional. Interrupted writes recover a complete old or new generation,
  and corrupt or foreign media is not silently initialized.
- The KC868-A16v3 adapter stores controller settings in its reserved SD-card
  area and reports absence, removal, write protection, full media, corruption,
  and I/O failures without blocking startup.
- The bounded ASCII terminal presents first-run credential setup or login over
  USB, followed by stable System Info, Settings, Diagnostics, and Reboot menus.
  It supports atomic credential changes, hostname and RS485 configuration,
  confirmed configuration reset, session timeout, login throttling, secret
  masking, live diagnostic streaming, and portable reboot dispatch.

## RS485 transport

- The KC868-A16v3 RS485 service uses the board-described UART, separates byte
  transport from protocol framing, and has bounded transmit and receive queues.
- UART format, timeouts, frame limits, and queue depths are configurable.
  Framing, parity, overflow, timeout, collision/protocol, and queue-drop health
  counters are exposed to diagnostics.
- Portable and on-target validation covers FCP discovery, information, health,
  coherent I/O, normal request/response traffic, wrong-address rejection,
  wrong-baud recovery, CRC rejection, truncated frames, and bounded malformed
  input. The ESP-IDF UART event task has a bounded stack sized for maximum-frame
  processing under load.

## Flow Controller Protocol

The normative FCP version 1 wire contract is documented in
[`PROTOCOL.md`](PROTOCOL.md). Its codec and dispatcher are independent of
ESP-IDF and UART types so another bounded transport can carry the protocol.

Implemented protocol capabilities include:

- versioned binary framing, CRC-16, discovery, device information,
  capabilities, health, echo, duplicate detection, and reason-coded errors;
- typed point enumeration and reads, coherent 16-channel input/output bitmap
  reads, and stable `input-01` through `input-16` and `output-01` through
  `output-16` identifiers;
- device-bound HMAC-SHA-256 challenge/session authentication, bounded sessions,
  expiry, throttling, sequence validation, replay protection, and session
  invalidation after credential changes;
- authenticated output commands with source ownership, command class,
  priority arbitration, correlation, issue and expiry times, replacement, and
  caller-only relinquish;
- bounded output-change subscriptions with sequence numbers, coalescing, gap
  reporting, and explicit read-based resynchronization; and
- resumable authenticated Flow IL v1 upload, VM validation, atomic commit, exact
  download, metadata/list operations, activation, deactivation, removal, and
  reboot-safe recovery. Active v1 artifacts execute through the portable VM
  production host and controller point adapters.

## Verification and tools

The shared flow library includes a portable Flow IL v1 loader and VM core.
It executes scheduled Boolean instructions through the PLC Scan Cycle, uses
fixed-capacity typed slots and current/next state, stages commands privately,
and publishes only at atomic scan commit. Its version-1 host ABI supports
requirements, prepare, initialize, instruction stepping, commit, abort, reset,
retained-state export, snapshot retrieval, and clear operations. Static firmware
and shared native-library targets build the same sources; fixture tests cover
Boolean scans, one-scan feedback, paused execution, abort safety, invalid
artifacts, and Flow IL v1 host equivalence.

- Portable host suites cover diagnostics, supervisors, MQTT, settings,
  terminal behaviour, protocol framing and dispatch, authentication, point
  arbitration and subscriptions, flow transfer, and direct I/O behaviour.
- The production ESP32-S3 image builds and boots on the KinCony KC868-A16v3.
  Core protocol, authentication, output, and transactional artifact operations
  have been exercised through a Linux host and USB-to-RS485 adapter.
- [`scripts/fcp-client.py`](scripts/fcp-client.py) provides dependency-free
  Linux commands for discovery, inspection, I/O, authenticated output control,
  subscriptions, and flow-artifact management.
