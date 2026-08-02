# Controller communications implementation plan

## Purpose

This plan builds a portable communications foundation for the future flow
runtime across ESP32, Raspberry Pi, STM32, and later controller families. It
covers diagnostics, non-blocking networking, bidirectional MQTT, and RS485.
The KinCony KC868-A16v3 is the first board implementation; hardware-specific
details are board properties rather than shared-service assumptions.

The flow evaluator, physical input/output support, persistent flow storage, and
remote firmware updates are deliberately outside this plan. Communications
APIs introduced here must remain usable by those later components.

## Architectural rules

- The platform entry point calls the shared `controller_main`, which performs
  bounded initialization, starts the controller runtime, and returns. It never
  waits for Wi-Fi, Ethernet, MQTT, or RS485 traffic.
- The main controller task loop starts before attempting network association.
- Every communications subsystem is an independently supervised state machine.
  A failed subsystem reports degraded status and retries without restarting or
  blocking unrelated controller work.
- MQTT depends on an abstract network service, never directly on Wi-Fi or an
  Ethernet driver.
- Wi-Fi and Ethernet are separate network links. They may be active
  simultaneously and must not be assumed to share a subnet, gateway, DNS
  server, or broker reachability.
- Queues, retries, payloads, topic lengths, and diagnostic messages are bounded.
  No reconnect loop may grow memory usage indefinitely.
- Credentials remain in the ignored local `sdkconfig` or a later secure
  provisioning store. Secrets must never appear in committed defaults,
  diagnostics, MQTT logs, crash output, or test fixtures.
- Runtime code consumes configuration through typed modules rather than reading
  platform facilities such as Kconfig, device tree, or environment variables
  throughout the application.
- Shared services depend only on platform and board contracts. Framework,
  processor, operating-system, and board headers remain in adaptation layers.
- All state transitions expose a read-only health snapshot for diagnostics
  and the future flow runtime.
- All controller code follows the repository coding rules in `AGENTS.md`:
  declarations document their contracts, non-obvious logic explains what and
  why, numeric and string values use documented constants or enums instead of
  magic values, Boolean predicates use `is_`, value accessors use `get_`, and
  source files contain one coherent feature or responsibility.

## Proposed component boundaries

```text
controller_main / controller_runtime
├── diagnostics
│   └── platform diagnostic transport
├── network_manager
│   ├── wifi_link
│   └── ethernet_link
├── mqtt_service
│   └── one supervised session per configured broker/link policy
└── rs485_service
    └── serial framing and request/receive queues

platform
├── timing, tasks, memory, logging, and reset information
└── architecture/OS drivers

board
└── identity, capabilities, pins, buses, and typed configuration sources
```

`network_manager` publishes link events and snapshots using neutral concepts
such as link ID, interface identity, addresses, DNS availability, reachability,
and state. It must not expose Wi-Fi event types to MQTT.

## Phase 1 — Portable diagnostic foundation (complete)

### Deliverables

- Replace the bring-up-only loop with a small `controller_runtime` component.
- Use the board's primary diagnostic transport. The KC868-A16v3 uses ESP32-S3
  USB Serial/JTAG.
- Add a diagnostics facade with severity, component, event code, monotonic
  timestamp, and formatted message.
- Print a startup banner containing firmware version, reset reason, chip,
  flash, PSRAM, and a redacted configuration summary.
- Start a controller heartbeat/status task before any networking is initialized.
- Add a `status` command or periodic structured status line reporting:
  - runtime uptime and free heap;
  - Wi-Fi and Ethernet states;
  - MQTT session state;
  - RS485 state and error counters.
- Ensure logging remains useful if a later subsystem repeatedly disconnects;
  rate-limit repeated errors and report suppressed-message counts.

### Tests

- Host/unit tests for diagnostic event formatting, redaction, rate limiting,
  and health snapshot formatting.
- Build with empty Wi-Fi/MQTT settings.
- On-target smoke test verifies the startup banner and heartbeat appear over
  the board's diagnostic transport after reset.
- On-target smoke test boots with no network hardware or broker available and
  confirms the heartbeat continues.

### Exit criteria

- Diagnostics work immediately after boot.
- The main runtime becomes active without waiting for any external service.
- No configured secret is present in captured console output.

## Phase 2 — Network abstraction and supervisor (complete)

### Deliverables

- Define neutral link states such as `disabled`, `starting`, `connecting`,
  `online`, `degraded`, `backoff`, and `stopped`.
- Define stable link IDs for Wi-Fi and Ethernet and a network snapshot carrying
  interface identity, IPv4/IPv6 addresses, DNS readiness, retry count, and last
  transition reason.
- Publish network events through a platform event loop or bounded queue.
- Add independent per-link supervisors with exponential backoff, jitter, a
  maximum delay, and retry-counter reset after a stable online period.
- Keep event callbacks short; callbacks enqueue state changes and never perform
  blocking connection work.
- Define route selection explicitly:
  - `automatic`: use any eligible link according to configured priority;
  - `wifi` or `ethernet`: bind a consumer to that link;
  - no automatic assumption that a service reachable on one link is reachable
    on the other.
- Support multiple simultaneous online links without treating the second link
  as a duplicate or error.

### Tests

- Unit tests for every valid state transition, stale/duplicate events, backoff
  growth, jitter bounds, stable-period reset, and shutdown.
- Unit tests for link selection with Wi-Fi only, Ethernet only, both links, and
  no links.
- Fault test repeatedly toggles synthetic link events while the controller
  heartbeat continues.

### Exit criteria

- Consumers can discover and select usable links without including Wi-Fi or
  Ethernet driver headers.
- Loss of one link does not mark another active link offline.

## Phase 3 — Resilient Wi-Fi station (complete)

### Deliverables

- Add typed Wi-Fi configuration sourced from the platform configuration layer.
  The first ESP-IDF board uses ignored `CONFIG_CONTROLLER_WIFI_SSID` and
  `CONFIG_CONTROLLER_WIFI_PASSWORD` settings.
- Initialize the platform persistence, network interfaces, and event system;
  on ESP-IDF, explicitly handle recoverable NVS initialization errors.
- Run Wi-Fi in station mode with power-save and hostname settings documented.
- Treat association, authentication, DHCP, address loss, and driver errors as
  distinct diagnostic events without logging credentials.
- Reconnect through the network supervisor with bounded exponential backoff and
  jitter. Do not reconnect recursively from an event callback.
- An empty SSID disables Wi-Fi cleanly rather than causing a reboot loop.
- Provide manual disable/enable/reconnect operations for later configuration
  and maintenance commands.

### Tests

- Unit tests for configuration validation and mapping platform events to
  neutral link events.
- On-target test boots with no access point and verifies the runtime heartbeat
  and increasing bounded retry delay.
- On-target test starts the access point after boot and verifies automatic
  connection without resetting the controller.
- On-target test removes and restores the access point and verifies recovery,
  new address reporting, and no unbounded heap loss.
- Extended test cycles connectivity at least 100 times while sampling free heap
  and task counts.

### Exit criteria

- Controller boot and its main task loop are independent of Wi-Fi availability.
- Wi-Fi recovers after access-point or DHCP loss without manual intervention.

## Phase 4 — Ethernet link

Current commissioning mode is Ethernet-only: the runtime must not initialize
or start Wi-Fi even when credentials remain configured in `sdkconfig`. The
Wi-Fi adapter remains available for a later explicit return to dual-link mode.

### Deliverables

- Add board-described Ethernet capabilities. The KC868-A16v3 first port uses a
  W5500 with this SPI definition:
  SCLK GPIO42, MOSI GPIO43, MISO GPIO44, CS GPIO15, interrupt GPIO2, and reset
  GPIO1.
- Encapsulate the SPI bus and W5500 driver in `ethernet_link`.
- Publish the same neutral link events and snapshots used by Wi-Fi.
- Handle cable loss, DHCP loss, W5500 reset, SPI errors, and recovery through
  the Ethernet supervisor.
- Permit Wi-Fi and Ethernet to be enabled and online concurrently.
- Record interface-specific addresses and DNS state; never collapse both links
  into one boolean `network_connected` flag.
- Emit the DHCP-allocated Ethernet IPv4 address and DNS readiness through
  redacted diagnostics when the interface becomes usable.

### Tests

- Unit tests for Ethernet event mapping and supervisor transitions.
- On-target boot tests with cable absent, cable present, and DHCP unavailable.
- Cable removal/restoration test confirms automatic recovery while Wi-Fi and
  the main runtime remain unaffected.
- Dual-link test places Wi-Fi and Ethernet on different subnets and verifies
  both interface snapshots remain correct.

### Exit criteria

- Wi-Fi-only, Ethernet-only, and simultaneous dual-link operation are supported
  by the same network-manager contract.

## Phase 5 — Transport-independent MQTT service

### Deliverables

- Add typed broker configuration for URI, client ID, credential references,
  TLS policy, keepalive, session policy, link policy, limits, and reconnect
  timing. Literal production secrets must not enter committed defaults.
- Implement a supervised MQTT session state machine such as `disabled`,
  `waiting_for_network`, `connecting`, `online`, `backoff`, and `stopping`.
- MQTT subscribes to neutral network events. It must not include or inspect
  Wi-Fi-specific events.
- Bind each MQTT session to its selected interface when a link policy is
  explicit. For `automatic`, select an eligible link and re-evaluate after
  reachability failure according to a documented priority/failover policy.
- If the same logical service must be reachable independently through two
  isolated networks, configure two MQTT sessions. Do not silently publish the
  same message twice through both links.
- Reconnect after broker disconnect, DNS failure, TLS failure, link loss, or
  address change using bounded backoff and jitter.
- Recreate subscriptions after every successful connection.
- Configure a unique client ID and optional last-will status so broker-side
  presence is meaningful.
- Expose session health, last error category, reconnect count, and queue depth
  without exposing credentials.

### Tests

- Unit tests for MQTT state transitions, network eligibility, failover policy,
  backoff, duplicate events, and subscription replay.
- Integration test with a local broker starts the controller before the broker,
  then verifies connection when the broker becomes available.
- Broker restart and network interruption tests verify automatic reconnection
  and subscription restoration.
- Dual-subnet test verifies a session bound to Wi-Fi does not accidentally use
  Ethernet, and vice versa.
- TLS-negative tests cover bad CA, hostname mismatch, and authentication failure
  with redacted diagnostics.

### Exit criteria

- MQTT reconnects after either network or broker failure without blocking the
  controller runtime.
- MQTT has no compile-time dependency on the Wi-Fi or W5500 implementation.

## Phase 6 — Bidirectional MQTT API

### Deliverables

- Provide a bounded publish API containing topic, payload, QoS, retain flag,
  correlation ID, and delivery result.
- Define offline publishing policy explicitly by message class:
  - discard replaceable telemetry or retain only its newest value;
  - queue bounded control/status messages only when their semantics permit it;
  - never use an unbounded offline queue.
- Provide a subscription registry with topic filters, QoS, owner ID, and
  callback/queue destination.
- Copy inbound payloads into owned bounded buffers before leaving the MQTT event
  callback; never retain platform-driver callback pointers.
- Validate topic and payload size, reject malformed commands, and isolate slow
  subscribers from the MQTT callback.
- Define command acknowledgement and correlation semantics for the future flow
  runtime.
- Publish controller availability and health using versioned topics and payload
  schemas.

### Tests

- Unit tests for topic validation, queue limits, telemetry coalescing, delivery
  results, subscription matching, oversized payload rejection, and callback
  ownership.
- Broker integration tests cover QoS 0 and QoS 1 publishing, retained status,
  inbound subscriptions, duplicate delivery, reconnect, and resubscription.
- Load test saturates publish and receive queues and verifies deterministic
  rejection without watchdog resets or memory growth.

### Exit criteria

- Independent components can safely publish and subscribe through MQTT.
- Offline and overload behaviour is bounded, observable, and tested.

## Phase 7 — RS485 service

### Deliverables

- Configure RS485 from board capabilities. The KC868-A16v3 first port uses TX
  GPIO16 and RX GPIO17.
- Confirm and document the onboard transceiver's automatic direction behaviour;
  do not invent an RTS GPIO if the board controls direction automatically.
- Add Kconfig settings for baud rate, data bits, parity, stop bits, receive
  timeout, frame size, queue depths, and optional protocol mode.
- Separate byte transport from protocol framing so Modbus RTU or another
  protocol can be added without replacing the UART service.
- Use bounded transmit and receive queues and explicit frame ownership.
- Track framing, parity, overflow, timeout, collision/protocol, and queue-drop
  counters in the diagnostic health snapshot.
- Ensure a stuck or noisy RS485 bus cannot block the main controller task,
  networking, MQTT, or diagnostics.

### Tests

- Unit tests for configuration validation, frame boundaries, CRC helper if
  introduced, timeout handling, and queue saturation.
- On-target loopback or second-adapter test verifies transmit and receive at
  configured UART formats.
- Disconnect, malformed-frame, continuous-noise, and peer-restart tests verify
  recovery and diagnostic counters.
- Concurrent test runs RS485 traffic while repeatedly interrupting MQTT and
  networking, confirming subsystem isolation.

### Exit criteria

- RS485 reliably sends and receives framed data without blocking other runtime
  work.
- Controller diagnostics clearly expose RS485 state and errors.

## Phase 8 — Integrated resilience and soak testing

### Deliverables

- Add one consolidated, immutable controller health snapshot consumed by diagnostics,
  MQTT status publishing, and the future flow runtime.
- Define watchdog ownership and prove all supervised tasks remain responsive.
- Add reason-coded counters for network, MQTT, RS485, queue, memory, and task
  failures.
- Document field troubleshooting using the board diagnostic transport and
  observable MQTT status.

### Tests

- Run a minimum 24-hour soak with Wi-Fi, Ethernet, MQTT publish/subscribe, and
  RS485 traffic active.
- Inject access-point loss, cable loss, DHCP failure, DNS failure, broker restart,
  invalid MQTT credentials, RS485 disconnect/noise, and diagnostic reconnects.
- Verify the main task heartbeat never stops, watchdogs do not fire, queues stay
  within limits, subscriptions return, and free heap does not trend downward.
- Power-cycle at each degraded state and verify deterministic recovery.

### Exit criteria

- Every external dependency can fail and recover independently.
- The controller remains observable through its diagnostic transport throughout
  failure testing.
- The communications foundation is ready for physical I/O and flow-runtime
  implementation.

## Completion requirements for every phase

- Production code and focused automated tests are included together.
- Every supported board builds from both command-line and VS Code tasks after
  selection with `Set board`.
- On-target test steps and expected diagnostic output are documented.
- Failure paths are tested, not only successful startup.
- New configuration has safe empty defaults and contains no committed secret.
- Diagnostics identify the affected subsystem without leaking sensitive data.
- Resource ownership, shutdown, retry, and queue limits are explicit.
- The phase does not weaken non-blocking boot or subsystem isolation.
