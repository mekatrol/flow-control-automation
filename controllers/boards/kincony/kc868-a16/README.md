# KinCony KC868-A16v3 board

This directory contains only KC868-A16v3 board documentation. The shared
firmware, build project, tests, and workspace tasks are at the
[`controllers`](../../..) root. Open that directory directly in VS Code and
select `kincony-kc868-a16` with the **Set board** task.

The generic phased communications roadmap is in
[`IMPLEMENTATION_PLAN.md`](../../../IMPLEMENTATION_PLAN.md).

## Hardware target

- ESP32-S3-WROOM-1U-N16R8 (16 MiB flash and 8 MiB octal PSRAM)
- USB-C connected to the ESP32-S3 USB Serial/JTAG peripheral
- Boot/Download button on GPIO0 and a separate Reset button

The board must be powered from its specified 12/24 V DC input. Do not assume
that USB-C powers the controller's field I/O circuitry.

## Install ESP-IDF

Install a current stable ESP-IDF release and its tools by following Espressif's
setup guide. In each new shell, load the ESP-IDF environment before running the
commands below. For a typical Linux installation this is:

```sh
. "$HOME/esp/esp-idf/export.sh"
```

For the embedded workflow, open the `controllers` directory as the VS Code
workspace. The repository recommends the VS Code `Espressif IDF` extension. Its
**ESP-IDF: Configure ESP-IDF Extension** command can install or select the SDK
and tools without requiring `idf.py` to be globally available.

## Build

Run commands from the `controllers` directory:

```sh
./scripts/controller-task.sh . set-board kincony-kc868-a16
./scripts/controller-task.sh . build
```

Run the platform-independent Phase 1 tests without ESP-IDF using:

```sh
cmake -S tests -B build-host
cmake --build build-host
ctest --test-dir build-host --output-on-failure
```

The VS Code tasks call `scripts/controller-task.sh`, which locates the selected
ESP-IDF installation and adds its compiler, CMake, Ninja, and Python tools to
the task environment. This is necessary because task shells do not inherit the
environment configured in another terminal.

**Set board** creates a root, ignored `sdkconfig`. The board's checked-in
`sdkconfig.defaults` configures the N16R8 memory and USB console.

Wi-Fi credentials and the device hostname are stored only in authenticated SD
settings and provisioned through the terminal. The hostname is shared by Wi-Fi
and Ethernet so boards running the same firmware can use distinct network
identities. The current commissioning runtime deliberately does not
initialize or start Wi-Fi. It uses
the onboard W5500 Ethernet port instead. Ethernet is enabled by default and its
DHCP hostname defaults to `flow-controller`; both settings are under **Flow
controller** in menuconfig.

MQTT username and password are stored only in authenticated SD settings and
provisioned through the terminal. Broker host, port, client ID, TLS, keepalive,
and reconnect timing remain non-secret build configuration under **Flow
controller**. An empty broker host disables MQTT, and diagnostics never expose
credentials.

## Flash and monitor

Connect the board's USB-C port, then locate the serial device if needed:

```sh
./scripts/controller-task.sh . flash /dev/ttyACM0
./scripts/controller-task.sh . monitor /dev/ttyACM0
```

Leave the monitor with `Ctrl+]`. If automatic download does not start, hold the
Download button, tap Reset, start flashing, and then release Download.

On Linux, USB access may require the udev rules shipped with ESP-IDF. On
Windows, install Espressif's USB Serial/JTAG driver through the ESP-IDF tools
installer.

### Phase 1 smoke test

Boot once with both Wi-Fi settings empty, then once with locally configured
values. In both cases, capture the USB console and check that:

- `startup/banner`, `startup/processor`, `startup/memory`, and
  `startup/configuration` records appear immediately;
- configuration reports only `wifi=disabled` or `wifi=enabled` and a redacted
  credential state—the SSID and password themselves never appear;
- `runtime/started` says that the platform entry is returning;
- a `runtime/heartbeat` record appears every five seconds and includes uptime,
  free heap, and explicit Wi-Fi, Ethernet, MQTT, and RS485 states.

Repeat with no access point, Ethernet cable, broker, or RS485 device attached.
The expected result is unchanged periodic heartbeat output and no reset. A
typical status payload is:

```text
status uptime_ms=5000 free_heap_bytes=... wifi=disabled ethernet=disabled mqtt=disabled rs485=disabled rs485_errors=0 rs485_queue_drops=0
```

Repeated subsystem errors added in later phases should use
`diagnostics_emit_limited`; it bounds identical records per time window and
reports the number suppressed when the next window begins.

### Phase 3 Wi-Fi smoke test

This historical smoke test applies when Wi-Fi runtime support is explicitly
restored. Wi-Fi is dormant in the current Ethernet-only commissioning build.

With local credentials configured, boot while the access point is unavailable.
The heartbeat must continue while Wi-Fi reports bounded `backoff` retries. Start
the access point and confirm Wi-Fi reaches `online` without a controller reset.
Then remove and restore the access point and confirm association and address
recovery occur automatically.

Repeat with DHCP unavailable and confirm the 30-second DHCP timeout returns the
link to supervised backoff. During a longer test, cycle connectivity at least
100 times while sampling `free_heap_bytes` and task count. Authentication,
association, address acquisition/loss, and driver failures have distinct
redacted diagnostic event codes; no event contains the configured SSID or
password.

### Phase 4 Ethernet smoke test

Connect the first W5500 Ethernet port to a DHCP-enabled network, then boot the
controller. Diagnostics should progress through `driver_started`,
`link_up_waiting_for_address`, and `address_ready`. The address-ready record
contains `ipv4=<allocated-address>` and `dns_ready=1` when DHCP supplied DNS.
The heartbeat should report `wifi=disabled ethernet=online`.

Remove and restore the cable and confirm the heartbeat continues while
Ethernet enters bounded backoff and automatically recovers. Also boot once
without a cable and once with DHCP unavailable; neither case should reset or
block the controller runtime.

### Phase 5 MQTT smoke test

Configure the broker host, port, a unique client ID, optional username and
password, TLS policy, session policy, link policy, last will, and reconnect
timing under **Flow controller**. Leave the host empty for the disabled test.
No broker value or credential is committed in defaults.

For the recovery test, run a broker reachable through the selected Ethernet
interface. Start the controller before the broker and confirm the heartbeat
continues with `mqtt=backoff`; then start the broker and confirm
`mqtt=online mqtt_transport=ethernet mqtt_error=none`. Restart the broker and remove
and restore the Ethernet cable. Each failure must increment
`mqtt_reconnect_count`, enter bounded backoff, and recover without stopping the
runtime heartbeat. An address change must also recreate the broker session.

For route binding, select Ethernet and verify the broker connection uses the
W5500 interface. When dual-link runtime operation is restored, repeat with
Wi-Fi selected and with automatic policy on isolated subnets. A session bound
to one link must not reach the broker through the other link; automatic policy
may select the next eligible link only after supervised failure and backoff.

For negative security tests, enable TLS and separately test an untrusted CA, a
hostname mismatch, and invalid broker credentials. Health must report `tls` or
`authentication` as appropriate, diagnostics must contain only stable error
codes, and neither the username nor password may appear in captured output.
When a last-will topic is configured, forcibly remove controller power and
confirm the broker publishes the configured offline payload with the selected
QoS and retain policy.

### Phase 6A settings-storage smoke test

The raw settings adapter owns exactly 16 consecutive 512-byte sectors beginning
at `CONTROLLER_SETTINGS_FIRST_RESERVED_SECTOR`. Keep this range outside every
partition and filesystem; zero disables the adapter. The firmware never guesses
an unused range and never formats other SD content. The card-detect input is
active low.

Provision a random 64-hex-character `CONTROLLER_SETTINGS_MASTER_KEY_HEX` only in
the ignored local `sdkconfig`. AES-256-GCM protects each slot, and its key is
derived from that provisioned secret plus the ESP32-S3 factory identity. Moving
the card to another controller or using the wrong secret must fail authentication
without exposing or overwriting its contents. Losing this key makes the settings
unrecoverable; it must not be committed, logged, or stored on the SD card.

For a dedicated blank 2 GB card, sectors 2048 through 2063 may be reserved for
controller settings. Keep that range excluded from every future filesystem or
application-persistence partition. Set the active configuration to sector 2048:

```sh
sed -i \
  's/^CONFIG_CONTROLLER_SETTINGS_FIRST_RESERVED_SECTOR=.*/CONFIG_CONTROLLER_SETTINGS_FIRST_RESERVED_SECTOR=2048/' \
  sdkconfig
```

Generate and install the device-local encryption key without printing it or
placing the key itself in shell history:

```sh
settings_key=$(openssl rand -hex 32)
sed -i \
  "s|^CONFIG_CONTROLLER_SETTINGS_MASTER_KEY_HEX=.*|CONFIG_CONTROLLER_SETTINGS_MASTER_KEY_HEX=\"$settings_key\"|" \
  sdkconfig
unset settings_key
```

Verify the active sector and key length without displaying the key:

```sh
awk -F= '
  /^CONFIG_CONTROLLER_SETTINGS_FIRST_RESERVED_SECTOR=/ { print "reserved_sector=" $2 }
  /^CONFIG_CONTROLLER_SETTINGS_MASTER_KEY_HEX=/ {
    value=$2
    gsub(/^"|"$/, "", value)
    print "master_key_hex_length=" length(value)
  }
' sdkconfig
```

The expected values are `reserved_sector=2048` and
`master_key_hex_length=64`. Check the active `sdkconfig`, not
`sdkconfig.old`; ESP-IDF retains the latter only as a backup during target or
configuration regeneration. The board-selection task preserves existing
`CONFIG_CONTROLLER_*` values when it regenerates `sdkconfig`.

Build and flash the provisioned configuration before monitoring:

```sh
./scripts/controller-task.sh . build
./scripts/controller-task.sh . flash /dev/ttyACM0
./scripts/controller-task.sh . monitor /dev/ttyACM0
```

Reserve the sector range, configure the key and start sector, insert an SD card,
and boot. A new or PC-formatted card commonly contains filesystem data in the
reserved sectors and enters the recovery menu with
`media_invalid_or_foreign`. Select `Initialize settings storage`, then type the
exact confirmation `ERASE SETTINGS`. The controller clears and verifies only
the 16 reserved settings sectors, cleanly releases the SD SPI device, and
reboots; it does not erase the remainder of the card. The terminal should then present first-run setup when its
credentials are `null`. After authenticating, enter Diagnostics mode to observe
`settings/state state=ready schema=2 generation=1` and heartbeat records. Reboot
or reflash and verify the persisted values remain unchanged. Remove the card and boot again; the terminal reports settings storage
as unavailable while networking and heartbeats continue internally. Also test a
card whose reserved range contains unrelated data and a card encrypted for
another controller; neither may be erased or reseeded.

When settings storage is unavailable, the terminal enters a constrained
recovery menu. It always exposes redacted System Info and confirmed Reboot
device operations. When the card is accessible but its reserved sectors are
foreign or cannot be authenticated, the menu also exposes confirmed settings
storage initialization. Automatic erasure is intentionally prohibited because
the media might contain settings encrypted for another controller or a damaged
generation worth recovering. Settings and Diagnostics remain unavailable until
persistent credentials can be loaded and verified.
If the USB host attaches after the initial prompt was sent, press Enter on an
empty line to redraw the active prompt or menu.

For power-loss testing, interrupt each of the initializing-marker, value, and
ready-marker writes. A subsequent boot must either use the previous authenticated
slot or restart incomplete first-time initialization. Repeat while atomically
changing a complete credential pair; recovery must expose the old pair or the new
pair, never one value from each. Capture the reserved sectors and all console
output to confirm plaintext credentials are absent.

### Phase 6 authenticated-terminal smoke test

Boot with uninitialized terminal credentials. USB application output must show
the first-run username prompt rather than the diagnostic stream. Complete the
username and masked-password flow, then verify the stable four-entry main menu.
Reboot and confirm the persisted credentials are required and an incorrect
password is delayed and counted without being echoed.

Exercise System Info and confirm it contains only redacted portable snapshots.
Update each credential pair through Settings, cancelling once and confirming
once. Terminal password replacement must invalidate the current login. Enter
Diagnostics and confirm structured events appear only in that mode; enter
`/menu` to leave it. Stall the reader while network and MQTT events occur and
confirm the controller remains responsive and output drops remain bounded.

Cancel and then confirm Reboot device, checking the next reset reason is the
normal software-reset reason. Finally, populate all settings, confirm Reset
configuration, and power-cycle. The terminal must return to first-run setup;
none of the old credential values may return.

The board defaults disable the ESP-IDF primary and secondary consoles, compile
out SDK component logs, silence the second-stage bootloader, and use silent
panic reboot. The terminal service still owns USB Serial/JTAG directly. A few
first-stage ROM lines can appear immediately at reset because they execute
before firmware configuration; do not burn an eFuse merely to suppress them.
Once application startup begins, only terminal-service output should use USB.

### Phase 8 RS485 adapter test

The KC868-A16v3 uses GPIO16 for TX and GPIO17 for RX behind its onboard
automatic-direction RS485 transceiver. There is no software RTS/DE pin. Connect
the Waveshare adapter `A+` to controller `A`, `B-` to controller `B`, and, for a
short bench setup with a non-isolated adapter, GND to GND. Do not connect either
RS485 signal to the controller supply terminals. Use termination only at the
two physical ends of a longer bus.

On Linux Mint, identify the adapter without assuming a stable device number:

```sh
ls -l /dev/serial/by-id/
```

Configure the firmware and adapter identically. Defaults are raw protocol,
115200 baud, 8 data bits, no parity, one stop bit, and a 20 ms inter-byte frame
timeout. A simple receive test from Linux is:

```sh
port=/dev/serial/by-id/usb-your-waveshare-adapter
stty -F "$port" 115200 cs8 -parenb -cstopb -ixon -ixoff raw
printf 'mint-to-controller' >"$port"
```

The commissioning firmware echoes every complete raw frame. Test both
directions from one Mint shell without opening the adapter in another program:

```sh
port=/dev/serial/by-id/usb-1a86_USB_Single_Serial_586D012048-if00
stty -F "$port" 115200 cs8 -parenb -cstopb -ixon -ixoff raw
printf 'rs485-echo-test' >"$port"
timeout 2 od -An -tc -N 15 <"$port"
```

The output should contain `rs485-echo-test`. Send groups separated by more
than the configured receive timeout to create distinct raw frames. Repeat with
A/B swapped if no bytes arrive because some vendors label differential
polarity oppositely.

Use the authenticated USB terminal on `/dev/ttyACM0`, then select `Settings`
and `RS485 configuration` to change the 16-bit controller address or baud rate.
The defaults are address 0 and 115200 bps. System Info displays both values.
After changing baud, immediately re-run `stty` on the Waveshare port with the
new rate; the terminal itself remains on the separate ESP32 USB connection.

During disconnect, malformed-format, continuous-input, and peer-restart tests,
the heartbeat must continue. `rs485_errors` and `rs485_queue_drops` must rise
when faults are injected, and normal traffic must resume without rebooting
networking, MQTT, or the controller.

## Debug

The ESP32-S3 has built-in JTAG, and the A16v3 exposes USB Serial/JTAG through
USB-C. No external debug probe should be required. With `controllers` open as
the VS Code workspace, build the selected board before starting an ESP-IDF
debug session. The OpenOCD board configuration is:

```text
board/esp32s3-builtin.cfg
```

Set a breakpoint in `app_main`, connect and power the board, and start the
debugger. Debugging requires Espressif's OpenOCD and Xtensa GDB from the ESP-IDF
tool installation; a generic system OpenOCD build is not sufficient.

Do not burn JTAG-related eFuses. The built-in USB JTAG route is enabled by
default and changing those eFuses can be irreversible.

## Known board connections

The bring-up program does not drive field outputs. These definitions are
recorded here for later board-support work:

| Function | Connection |
| --- | --- |
| I2C | SDA GPIO9, SCL GPIO10 |
| Outputs 1-8 / 9-16 | PCF8574 at `0x24` / `0x25`, active low |
| Inputs 1-8 / 9-16 | PCF8574 at `0x21` / `0x22` |
| EEPROM / RTC / display | I2C `0x50` / `0x68` / `0x3c` |
| Analog A1-A4 | GPIO4, GPIO6, GPIO7, GPIO5 |
| RS485 | TX GPIO16, RX GPIO17 |
| W5500 Ethernet | SCLK 42, MOSI 43, MISO 44, CS 15, IRQ 2, reset 1 |
| SD card | MOSI 12, SCLK 13, MISO 14, CS 11, detect 21 |
| 1-Wire | GPIO47, GPIO48, GPIO38 |
| Free GPIO | GPIO39, GPIO40, GPIO41 |

Keep hardware access behind a board-support layer when the flow runtime is
added. In particular, the PCF8574 input expanders have no interrupt connection,
so digital inputs must be polled and are not suitable for lossless fast pulse
counting without a hardware modification.
