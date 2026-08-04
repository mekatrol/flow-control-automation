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

Wi-Fi settings may remain in the ignored `sdkconfig`, but the current
commissioning runtime deliberately does not initialize or start Wi-Fi. It uses
the onboard W5500 Ethernet port instead. Ethernet is enabled by default and its
DHCP hostname defaults to `flow-controller`; both settings are under **Flow
controller** in menuconfig.

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
