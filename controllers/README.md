# Flow controller firmware

This directory is the firmware workspace for all flow-controller hardware.
Portable runtime and communication services live in `shared/`; processor and
operating-system adaptations live in `platforms/`; hardware definitions live
in `boards/`. The shared [`main.c`](main.c) is the controller entry point on
every platform. A thin platform entry (for example ESP-IDF's `platform_main`) calls
it.

For a clean machine or newly cloned repository, follow
[`SETUP_DEV.md`](SETUP_DEV.md) before building.

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

The tasks are relative to this workspace and include **Format**, **Clean**, **Build**,
**Flash**, **Monitor**, **Flash and Monitor**, **Clean, Build, and Flash**, and
**Clean, Build, Flash, and Monitor**. Serial tasks prompt for a port and default
to `/dev/ttyACM0`.

The ESP-IDF dependency version is recorded in `dependencies.lock`. Configure
the Espressif extension or install the recorded ESP-IDF version before building.
The task helper finds versioned installations under `~/.espressif`.

Equivalent command-line use is:

```sh
./scripts/controller-task.sh . set-board kincony-kc868-a16
./scripts/controller-task.sh . format
./scripts/controller-task.sh . build
./scripts/controller-task.sh . flash /dev/ttyACM0
./scripts/controller-task.sh . monitor /dev/ttyACM0
```

Configure local credentials with ESP-IDF menuconfig under **Flow controller**.
The root `sdkconfig` is ignored. Board selection and build output are also
ignored, and no credential is stored in board defaults.

## Code formatting

The repository's [`.clang-format`](.clang-format) configuration uses the
Microsoft base style, permits lines up to 200 columns, and aligns assignment
operators in each contiguous block. A blank line or comment ends an alignment
block.

Before committing, run **Tasks: Run Task** and choose **Format**. The equivalent
command-line invocation is:

```sh
./scripts/controller-task.sh . format
```

The command formats every tracked `.c` and `.h` file. It deliberately excludes
untracked and generated files. Review the resulting diff, then run the host
tests before committing.

Firmware and host builds also enforce the source policy that unused callback
parameters must be unnamed C23 parameters, with an explanatory comment when
useful (for example, `void * /* context */`). A `(void)parameter;` suppression
statement fails the build.

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
