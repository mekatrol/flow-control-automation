# Flow controller firmware

This directory is the firmware workspace for all flow-controller hardware.
Portable runtime and communication services live in `shared/`; processor and
operating-system adaptations live in `platforms/`; hardware definitions live
in `boards/`. The shared [`main.c`](main.c) is the controller entry point on
every platform. A thin platform entry (for example ESP-IDF's `app_main`) calls
it.

The first supported board is the KinCony KC868-A16v3 on ESP32-S3. Future ESP32,
Raspberry Pi, STM32, and other targets should add a board description and, only
when necessary, a platform adaptation. Shared services such as diagnostics,
network management, MQTT, RS485 framing, and the future flow runtime must not
include vendor board or SDK headers.

## Workspace and board selection

Open this `controllers` directory directly in VS Code. Run **Tasks: Run Task**
and choose **Set board** before the first build or whenever switching hardware.
The selection is stored locally in ignored `.controller-board`; each board has
an independent `build-<board>` directory.

Available board selections:

| Selection | Platform | Target |
| --- | --- | --- |
| `kincony-kc868-a16` | ESP-IDF | ESP32-S3 |

The tasks are relative to this workspace and include **Clean**, **Build**,
**Flash**, **Monitor**, **Flash and Monitor**, **Clean, Build, and Flash**, and
**Clean, Build, Flash, and Monitor**. Serial tasks prompt for a port and default
to `/dev/ttyACM0`.

The ESP-IDF dependency version is recorded in `dependencies.lock`. Configure
the Espressif extension or install the recorded ESP-IDF version before building.
The task helper finds versioned installations under `~/.espressif`.

Equivalent command-line use is:

```sh
./scripts/controller-task.sh . set-board kincony-kc868-a16
./scripts/controller-task.sh . build
./scripts/controller-task.sh . flash /dev/ttyACM0
./scripts/controller-task.sh . monitor /dev/ttyACM0
```

Configure local credentials with ESP-IDF menuconfig under **Flow controller**.
The root `sdkconfig` is ignored. Board selection and build output are also
ignored, and no credential is stored in board defaults.

## Host tests

Shared modules have platform-independent tests:

```sh
cmake -S tests -B build-host
cmake --build build-host
ctest --test-dir build-host --output-on-failure
```

The phased roadmap is in [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md).
Board-specific wiring and commissioning notes belong under that board's
directory.
