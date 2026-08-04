# Developer environment setup

This guide prepares a clean development machine or a newly cloned repository
to build, test, format, flash, and monitor the flow-controller firmware.
Machine-specific ESP-IDF paths, board selection, credentials, and build output
are intentionally not committed.

## Prerequisites

Install the following tools:

- Git.
- Bash. The repository task helper is a Bash script.
- VS Code with the recommended **Espressif IDF** extension, or a command-line
  ESP-IDF installation.
- ESP-IDF 6.0.2. The required version is recorded in `dependencies.lock`.
- CMake and a C compiler if host tests will be built.

The first firmware build needs internet access to download the component
versions pinned in `dependencies.lock`, including the W5500 driver.

## Prepare a new clone

Open a terminal in the cloned repository and enter the controller workspace:

```sh
cd controllers
```

Open this `controllers` directory directly in VS Code. Accept the recommended
extension installation when prompted.

Use the Espressif extension's setup or configuration command to install or
select ESP-IDF 6.0.2. The selected installation is local to each developer;
do not add an absolute `idf.currentSetup` path to the committed
`.vscode/settings.json` file.

## Select the controller board

Before the first firmware build, run **Tasks: Run Task**, choose **Set board**,
and select `kincony-kc868-a16`.

The command-line equivalent is:

```sh
./scripts/controller-task.sh . set-board kincony-kc868-a16
```

This creates the ignored local `.controller-board`, selects the ESP32-S3
target, generates `sdkconfig`, and prepares the board-specific build directory.
Run the task again whenever switching hardware.

## Configure local credentials

Use ESP-IDF menuconfig or the extension's SDK Configuration Editor to set any
device-local configuration and secrets under **Flow controller**. Examples
include Wi-Fi credentials, MQTT credentials, API or service tokens, and other
values that differ between installations.

The generated `sdkconfig` is ignored because it can contain credentials. Never
put real credentials in `sdkconfig.defaults` or another committed file.

## Build the firmware

Run the VS Code **Build** task, or use:

```sh
./scripts/controller-task.sh . build
```

The task helper finds versioned ESP-IDF installations under `~/.espressif`.
The first build downloads pinned managed components and can therefore take
longer than subsequent builds.

## Run host tests

Host tests do not require the ESP32 toolchain, but they do require CMake and a
host C compiler:

```sh
cmake -S tests -B build-host
cmake --build build-host
ctest --test-dir build-host --output-on-failure
```

Both host and firmware builds run the repository source-policy check and fail
when a policy violation is found.

## Format before committing

Run the VS Code **Format** task or:

```sh
./scripts/controller-task.sh . format
```

The command formats all tracked `.c` and `.h` files using `.clang-format` and
the `clang-format` binary supplied by the ESP-IDF toolchain. Review the diff,
run the host tests, and build the firmware before committing.

## Flash and monitor

Use the VS Code **Flash**, **Monitor**, or combined tasks. Their command-line
equivalents are:

```sh
./scripts/controller-task.sh . flash /dev/ttyACM0
./scripts/controller-task.sh . monitor /dev/ttyACM0
```

Replace `/dev/ttyACM0` with the controller's serial port. Access to the port
may require local operating-system permissions.

## Generated local files

The following are regenerated locally and should remain uncommitted:

- `.controller-board`
- `sdkconfig` and `sdkconfig.old`
- `build/`, `build-host/`, and `build-<board>/`
- `managed_components/`

## Troubleshooting

If VS Code reports that `/tools/cmake/project.cmake` cannot be found, reload the
window or run **CMake: Delete Cache and Reconfigure**. Generic CMake tooling is
configured to use the `tests/` host project; the Espressif extension handles
the firmware project and supplies `IDF_PATH`.

If a firmware task cannot find ESP-IDF, configure ESP-IDF 6.0.2 through the
Espressif extension or install it in a versioned directory under
`~/.espressif`.
