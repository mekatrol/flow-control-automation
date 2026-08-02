# KC868-A16v3 firmware

Native C firmware for the KinCony KC868-A16v3, built with ESP-IDF. The firmware
starts a non-blocking controller runtime and reports structured startup and
health diagnostics over USB Serial/JTAG. Networking, MQTT, and RS485 are shown
as disabled until their later implementation phases are installed.

The phased communications roadmap is in
[`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md).

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

For the embedded workflow, open this `kc868-a16` directory as the VS Code
workspace. The repository recommends the VS Code `Espressif IDF` extension. Its
**ESP-IDF: Configure ESP-IDF Extension** command can install or select the SDK
and tools without requiring `idf.py` to be globally available.

## Build

Run commands from this directory:

```sh
idf.py set-target esp32s3
idf.py build
```

Run the platform-independent Phase 1 tests without ESP-IDF using:

```sh
cmake -S tests -B build-host
cmake --build build-host
ctest --test-dir build-host --output-on-failure
```

The VS Code tasks call `scripts/esp-idf-task.sh`, which locates the selected
ESP-IDF installation and adds its compiler, CMake, Ninja, and Python tools to
the task environment. This is necessary because task shells do not inherit the
environment configured in another terminal.

`set-target` creates a local, ignored `sdkconfig`. The checked-in
`sdkconfig.defaults` configures the N16R8 memory and USB console.

Before adding the Wi-Fi runtime, configure credentials with **ESP-IDF: SDK
Configuration editor (menuconfig)** under **KC868-A16v3 controller**. The SSID
and password are stored only in the ignored `sdkconfig`; they are never placed
in checked-in defaults. The **configure target** VS Code task regenerates the
ESP32-S3 configuration while preserving both credential settings, matching the
LED-controller firmware workflow.

## Flash and monitor

Connect the board's USB-C port, then locate the serial device if needed:

```sh
idf.py -p /dev/ttyACM0 flash monitor
```

Leave the monitor with `Ctrl+]`. If automatic download does not start, hold the
Download button, tap Reset, start flashing, and then release Download.

On Linux, USB access may require the udev rules shipped with ESP-IDF. On
Windows, install Espressif's USB Serial/JTAG driver through the ESP-IDF tools
installer.

### Phase 1 smoke test

Boot once with both Wi-Fi settings empty, then once with locally configured
values. In both cases, capture the USB console and check that:

- `startup/banner`, `startup/chip`, `startup/memory`, and
  `startup/configuration` records appear immediately;
- configuration reports only `wifi=disabled` or `wifi=enabled` and a redacted
  credential state—the SSID and password themselves never appear;
- `runtime/started` says that `app_main` is returning;
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

## Debug

The ESP32-S3 has built-in JTAG, and the A16v3 exposes USB Serial/JTAG through
USB-C. No external debug probe should be required. With this directory open as
the VS Code workspace, build first and then select **debug: KC868-A16v3** in the
Run and Debug view. The configuration uses:

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
