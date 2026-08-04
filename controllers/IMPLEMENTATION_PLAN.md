# Controller communications implementation plan

## Purpose

This plan builds a portable communications foundation for the future flow
runtime across ESP32, Raspberry Pi, STM32, and later controller families. It
covers diagnostics, non-blocking networking, bidirectional MQTT, RS485, and an
authenticated ASCII terminal. The KinCony KC868-A16v3 is the first board
implementation; hardware-specific details are board properties rather than
shared-service assumptions.

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
- MQTT depends on an abstract transport-route provider. IP adapters may use the
  network manager, while CAN, RS485, RS232, and future adapters remain free to
  provide routes without compiling against networking.
- Wi-Fi and Ethernet are separate network links. They may be active
  simultaneously and must not be assumed to share a subnet, gateway, DNS
  server, or broker reachability.
- Queues, retries, payloads, topic lengths, and diagnostic messages are bounded.
  No reconnect loop may grow memory usage indefinitely.
- Credentials are provisioned through the authenticated terminal and never
  compiled into firmware. At runtime, typed settings are loaded from an abstract persistent settings store
  whose board implementation may use an SD card, processor flash, or a separate
  flash device. Secrets must never appear in committed defaults, diagnostics,
  MQTT logs, crash output, or test fixtures.
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
├── terminal_service
│   ├── ASCII shell and authentication
│   └── diagnostics stream
├── settings_service
│   └── board-selected persistent settings store
├── diagnostics
│   └── bounded diagnostic event source
├── network_manager
│   ├── wifi_link
│   └── ethernet_link
├── mqtt_service
│   └── one supervised session per configured broker/transport policy
└── rs485_service
    └── serial framing and request/receive queues

platform
├── timing, tasks, memory, logging, and reset information
└── architecture/OS drivers

board
└── identity, capabilities, pins, buses, terminal ports, and typed
    configuration sources
```

An IP MQTT adapter may translate `network_manager` snapshots into the opaque
transport-route contract. The MQTT service itself must not include network
manager, Wi-Fi, Ethernet, CAN, or serial types.

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
  The first ESP-IDF board reads Wi-Fi credentials from authenticated persistent
  settings provisioned through the terminal.
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

## Phase 4 — Ethernet link (complete)

Current commissioning mode is Ethernet-only: the runtime must not initialize
or start Wi-Fi even when credentials are present in persistent settings. The
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

## Phase 5 — Transport-independent MQTT service (complete)

### Deliverables

- Add typed broker configuration for URI, client ID, credential references,
  TLS policy, keepalive, session policy, limits, and reconnect timing. Keep
  transport-selection policy in the selected platform adapter. Literal
  production secrets must not enter committed defaults.
- Implement a supervised MQTT session state machine such as `disabled`,
  `waiting_for_transport`, `connecting`, `online`, `backoff`, and `stopping`.
- MQTT consumes opaque route availability and generation snapshots. It must not
  include or inspect network-manager, Wi-Fi, Ethernet, CAN, or serial events.
- Bind each MQTT session to its selected transport route. An IP adapter applies
  its configured Wi-Fi/Ethernet policy and re-evaluates reachability according
  to the documented network priority/failover policy.
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

- Unit tests for MQTT state transitions, transport eligibility, route changes,
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

- MQTT reconnects after either transport-route or broker failure without
  blocking the controller runtime.
- The shared MQTT service has no compile-time dependency on the network manager
  or on any Wi-Fi, Ethernet, CAN, RS485, or RS232 implementation.

## Remaining implementation order

Phase 6, the authenticated ASCII terminal port, is complete.
Its detailed scope appears below the pre-existing remaining phases so the
terminal requirements stay together. After Phase 6, implementation continues
in this order:

1. Phase 7 — Bidirectional MQTT API
2. Phase 8 — RS485 service
3. Phase 9 — Integrated resilience and soak testing

## Phase 7 — Bidirectional MQTT API (complete)

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

## Phase 8 — RS485 service

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

## Phase 9 — Integrated resilience and soak testing

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

## Phase 6 — Authenticated ASCII terminal port (complete)

The first terminal transport is the board's USB port. The shell and menu must
depend on a terminal transport contract rather than USB driver types so another
serial or network-backed terminal can be added later. This phase converts the
current always-on USB diagnostics output into an authenticated interactive
terminal while retaining diagnostics streaming as an explicit terminal mode.
After boot, the USB port defaults to the interactive terminal rather than the
diagnostics stream. Once persistent settings are ready, an attached terminal
receives either the authentication prompt or, when terminal credentials are
still `null`, the first-run credential setup flow. Successful authentication or
first-run setup leads directly to the main menu. Diagnostics are streamed only
after the user selects menu option 3.

### Phase 6A — Persistent settings foundation (complete)

Persistence is the first implementation step in Phase 6 because terminal
authentication and every writable Settings menu entry depend on it. The shared
settings service must not require a filesystem or expose SD-card, processor
flash, or external-flash types to its consumers.

#### Deliverables

- Define an abstract persistent settings-store contract for reading, staging,
  atomically committing, and removing versioned typed values. The contract must
  distinguish an unavailable store, a missing value, an explicitly stored empty
  string, valid data, and corrupt or incompatible data.
- Keep storage mechanics behind a board-selected adapter. Initial
  implementations may use raw records, a key/value library, or a filesystem,
  but no shared settings or terminal code may depend on files, paths, sectors,
  partitions, or a particular storage driver.
- Implement the first adapter using the KC868-A16v3 SD card connected with MOSI
  GPIO12, SCLK GPIO13, MISO GPIO14, CS GPIO11, and card detect GPIO21. Treat card
  absence, removal, write protection, initialization failure, full media, I/O
  failure, and corrupt records as reason-coded degraded states without blocking
  controller startup.
- Reserve a documented bootstrap record in the controller-owned storage area.
  It contains a storage magic marker, format version, settings-schema version,
  record generation, and integrity check. The storage adapter reads and
  validates this record before attempting to load any setting.
- Use the bootstrap marker to classify media as `uninitialized`, `ready`,
  `initialization_interrupted`, `incompatible`, `corrupt`, or `foreign`. Do not
  treat corrupt, incompatible, or foreign media as blank and do not silently
  erase or reseed it.
- When storage is uninitialized, initialize the controller-owned storage area
  before starting the terminal service. For the initial raw SD-card adapter,
  formatting means creating an empty record layout and bootstrap metadata; it
  does not require a partition table, directory, or filesystem. Do not format
  portions of the SD card outside the explicitly reserved controller storage
  area.
- Perform first-time initialization as a recoverable transaction:
  1. write an initialization-in-progress bootstrap record;
  2. create and verify the empty storage layout;
  3. create every nullable credential as `null` for terminal provisioning;
  4. read back and verify the seeded settings;
  5. write and verify the ready marker last.
  A reset or card removal before the final marker must resume or safely restart
  initialization and must never expose partially seeded settings as ready.
- Define credential settings as nullable values, separately from their string
  contents:
  - `null` means the value has never been set;
  - an empty string means the value has deliberately been set to empty;
  - a non-empty string is the configured value.
- On first initialization, create missing credential values as `null`. After
  terminal provisioning, preserve persisted null, empty, and non-empty values
  across firmware updates and reflashing.
- Define an atomic settings-service reset operation that writes a complete,
  valid, ready, user-reset settings generation rather than erasing bootstrap
  metadata or making storage appear uninitialized. The reset generation sets
  every nullable credential to `null`, clears every other sensitive value, and
  applies schema-defined blank values to non-sensitive settings. Persist an
  explicit reset-origin marker so subsequent boots, firmware updates, and
  reflashing retain the reset state. Terminal credentials
  being `null` intentionally returns the terminal to its constrained first-run
  credential setup flow.
- Do not expose Wi-Fi, terminal, or MQTT credential fields through Kconfig.
  Platform adapters consume nullable credential values only from the persistent
  settings snapshot.
- Keep per-setting presence metadata in addition to the storage bootstrap
  marker so a stored `null` cannot be mistaken for a missing or uninitialized
  key. Define forward migration, incompatible-version handling, and recovery
  from an interrupted first-time seed operation.
- Commit a complete credential update atomically so a reset or card removal
  cannot leave a partially updated username/password pair. Retain the previous
  valid record until the replacement is durably committed and verified.
- Define the protection policy for secrets stored on removable SD media,
  including whether the first implementation encrypts them at rest and how any
  device-bound key is obtained. Diagnostics may expose storage health and
  schema version but never stored values, usernames, passwords, or key
  material.
- Expose typed snapshots to Wi-Fi, terminal, and MQTT through the settings
  service. Those consumers must not read `sdkconfig` or the SD-card adapter
  directly.

#### Tests

- Contract tests run against an in-memory fake and cover missing, `null`, empty,
  non-empty, corrupt, and incompatible values plus unavailable storage.
- Initialization tests cover missing and `null` initial values and prove
  terminal-provisioned values are not replaced on later boots or after a
  simulated firmware reflash.
- Bootstrap tests cover erased media, a valid ready marker, initialization in
  progress, invalid integrity data, incompatible versions, and foreign media.
  They verify only genuinely uninitialized controller storage is formatted and
  seeded automatically.
- Atomicity tests inject reset, removal, short-write, full-media, and read-back
  failures at every initialization and commit stage. Initialization must remain
  resumable until the ready marker is durable, and updates must recover either
  the old or new complete credential set, never a mixture.
- Reset tests verify every credential and sensitive field is absent from the
  committed reset snapshot, the reset generation remains ready, and later
  boots or simulated reflashes do not repopulate any credential.
  Fault injection at every reset commit stage must recover the complete old or
  complete blank snapshot, never a partial reset.
- Migration tests cover supported schema upgrades, unknown future versions,
  corrupt initialization markers, and interrupted first-time seeding.
- On-target tests cover boot with no SD card, insertion and removal, repeated
  writes, power loss during a write, and restoration of the last valid settings
  without stopping the controller heartbeat.
- Security tests inspect SD contents, logs, diagnostics, crash output, and test
  artifacts according to the defined at-rest protection policy.

#### Exit criteria

- The selected controller loads and atomically persists typed settings through
  its SD-card adapter while shared consumers remain independent of the storage
  mechanism and any filesystem.
- `null`, explicitly empty, and non-empty credential values retain their exact
  meaning across restart, firmware update, and recovery from interrupted writes.
- Missing persisted credentials are initialized as `null`; existing persisted
  values are never silently reset by reflashing.
- A user-requested configuration reset atomically commits a complete blank
  snapshot, removes all persisted credentials and other sensitive values, and
  retains that blank state until settings are explicitly entered again.
- An uninitialized SD-card storage area is formatted, seeded, verified, and
  marked ready before settings are consumed; an interrupted initialization is
  recoverable and corrupt or foreign media is never silently formatted.
- Storage absence or failure is observable and cannot block controller startup
  or unrelated runtime work.

### Deliverables

- Add a non-blocking `terminal_service` with bounded input lines, output
  records, session timeouts, and queues. A disconnected, unauthenticated, or
  slow terminal must not delay the controller runtime or diagnostic producers.
- Add an ASCII-only, line-oriented shell over the board's USB terminal port.
  Reject control characters and overlength input cleanly, support backspace and
  common line endings, and do not require ANSI cursor-control support.
- Make the terminal login/setup prompt the default USB mode after boot. Do not
  emit the live diagnostics stream before authentication or menu selection;
  boot diagnostics remain available to internal bounded diagnostic storage and
  are displayed live only when the authenticated user enters Diagnostics mode.
- Require a terminal username and password before displaying or accepting menu
  commands. Do not echo passwords, compare credentials safely, rate-limit
  failed attempts, and clear session state on disconnect or idle timeout.
- When either required terminal credential is `null`, present a constrained
  first-run setup flow that can only establish and persist the missing terminal
  credentials. It must not expose the main menu or other settings until the
  credentials are committed successfully. An explicitly empty credential is a
  set value and must not trigger first-run setup.
- Define a secure initial terminal-credential provisioning and recovery policy.
  No universal or production default password may be committed to the
  repository, printed in diagnostics, or exposed by the system-information
  menu.
- Present this stable top-level menu after authentication:
  1. `System Info`
  2. `Settings`
  3. `Diagnostics`
  4. `Reboot device`
- Implement `System Info` as a read-only snapshot containing all information
  available on the device, including device name, firmware and hardware model
  information, uptime, network-interface state and IP addresses, and configured
  CAN, RS485, RS232, or other bus addresses. Mark unavailable or unsupported
  fields explicitly and redact secrets and credential-derived values.
- Implement a `Settings` submenu with these entries:
  1. `Wi-Fi credentials`
  2. `Terminal credentials`
  3. `MQTT credentials`
  4. `Device hostname`
  5. `Reset configuration`
- Validate settings before committing them, mask secrets during entry, request
  confirmation before replacing credentials, and write each credential set
  atomically through the Phase 6A typed settings-service contract. Never log,
  redisplay, or retain plaintext secrets longer than required.
- Persist one validated device hostname and apply it to both Wi-Fi and Ethernet
  so identical firmware images can retain distinct network identities.
- Apply changed settings through the owning subsystem without rebooting where
  practical. Wi-Fi and MQTT changes trigger controlled reconnection; terminal
  credential changes invalidate other active terminal sessions and take effect
  no later than the next authentication attempt. Report whether a restart is
  required when a platform cannot apply a change live.
- Implement `Reset configuration` as an explicitly confirmed destructive
  action through the Phase 6A atomic reset operation. The confirmation prompt
  must state that all credentials and settings will be cleared. Do not emulate
  reset by deleting bootstrap records, formatting storage, or invoking normal
  first-time initialization because those paths do not represent an explicit
  ready reset generation. After a successful reset, disconnect MQTT and Wi-Fi where
  applicable, invalidate every authenticated terminal session, clear sensitive
  input buffers, and enter the terminal first-run credential setup flow. A
  failed commit leaves the previous complete configuration active and reports
  failure without displaying stored values.
- Implement `Reboot device` through a portable platform reboot contract after
  explicit user confirmation. The selected board adapter should request a
  normal reboot using its underlying SDK. If a normal reboot API is unavailable,
  it may deliberately stop servicing a board watchdog only when watchdog
  ownership and the resulting reset are documented and bounded. If neither
  mechanism is supported, keep the menu entry stable and return exactly
  `System reboot not supported by this device.` without disrupting the session.
  Flush only already-queued non-sensitive terminal output for a bounded period
  before requesting reboot; never wait indefinitely for a slow terminal.
- Implement `Diagnostics` by attaching the session to the existing diagnostics
  event stream. Preserve the current USB diagnostics logger output, apply
  bounded buffering and drop accounting for slow readers, and provide a
  documented ASCII escape command that returns to the main menu without
  restarting the controller.
- Keep menu labels and command identifiers in a versioned terminal contract so
  host-side tests and future automation can rely on stable behaviour.
- Expose terminal state, authenticated-session count, failed-login count,
  output-drop count, and last disconnect reason in the controller health
  snapshot without exposing usernames or passwords.

### Tests

- Unit tests cover authentication success and failure, retry throttling,
  disconnect and idle-timeout logout, password non-echo, line editing, invalid
  characters, overlength input, menu navigation, and diagnostics-mode exit.
- Unit tests verify every `System Info` field for available, unavailable, and
  unsupported data and confirm that snapshots never contain configured
  secrets.
- Unit tests cover validation, confirmation, atomic commit failure, and
  rollback for Wi-Fi, terminal, and MQTT credential changes.
- Unit tests cover reset confirmation and cancellation, the complete blank
  snapshot, removal of every credential and sensitive field, reset commit
  rollback, session invalidation, first-run setup after reset, and proof that
  credentials are not restored after reboot or reflash.
- Unit tests cover reboot confirmation and cancellation, successful platform
  reboot dispatch, bounded output flushing, watchdog-reset fallback dispatch,
  and the exact unsupported-device response while retaining the terminal
  session.
- Integration tests verify that Wi-Fi and MQTT credential changes reconnect
  only their owning subsystems and that a terminal credential change prevents
  reuse of the previous password.
- On-target USB tests cover boot with no terminal connected, repeated connect
  and disconnect, first-run setup, authentication, the main menu as the default
  post-authentication view, all menu paths, sustained diagnostics output, a
  stalled terminal reader, and return from diagnostics mode to the menu.
- On-target USB tests reset a fully populated configuration, power-cycle the
  controller, and verify that no prior credential returns and
  that terminal first-run setup is presented. They also exercise `Reboot
  device`, verify the reported reset reason, and confirm normal controller
  startup after the SDK or documented watchdog reset path.
- Security tests capture terminal and diagnostic output during login and every
  settings operation and verify that no plaintext secret is emitted or left in
  reusable command history.
- Concurrent fault tests exercise the terminal while networking, MQTT, and
  RS485 repeatedly fail and recover, confirming that terminal traffic does not
  block their supervisors or the main heartbeat.

### Exit criteria

- An authenticated user can access system information, update each supported
  credential set, atomically reset all configuration, reboot a supported
  device, and enter or leave live diagnostics through the USB ASCII terminal.
- Resetting configuration removes all credentials and sensitive values without
  build-time reseeding. The reboot menu remains present on unsupported
  devices and reports that reboot is unavailable without ending the session.
- After boot, USB presents terminal authentication or first-run credential
  setup and then the main menu; diagnostics output is not the default USB mode.
- Terminal absence, disconnect, malformed input, failed authentication, and a
  slow reader cannot block the controller or grow memory without bound.
- Credentials and other secrets are absent from terminal output, diagnostics,
  crash output, committed defaults, and automated-test artifacts.
- The shell is independent of the USB implementation and can accept another
  terminal transport without changing its menu or authentication logic.

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
