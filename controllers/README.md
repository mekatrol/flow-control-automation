# Flow controller firmware

This directory is the firmware workspace for all flow-controller hardware.
Portable runtime and communication services live in `shared/`; processor and
operating-system adaptations live in `platforms/`; hardware definitions live
in `boards/`. The shared [`main.c`](main.c) is the controller entry point on
every platform. A thin platform entry (for example ESP-IDF's `app_main`) calls
it.

For a clean machine or newly cloned repository, follow
[`SETUP_DEV.md`](SETUP_DEV.md) before building.

See [`FEATURES.md`](FEATURES.md) for the implemented firmware capabilities and
[`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md) for work that remains.

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
Microsoft base style, permits lines up to 130 columns, and aligns assignment
operators in each contiguous block. A blank line or comment ends an alignment
block.

Before committing, run **Tasks: Run Task** and choose **Format**. The equivalent
command-line invocation is:

```sh
./scripts/controller-task.sh . format
```

The command formats every tracked or untracked non-ignored `.c` and `.h` file,
so new sources are formatted before staging or committing. Generated and
ignored files remain excluded. Review the resulting diff, then run the host
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

## Test FCP over RS485 from Linux

Use the dependency-free `scripts/fcp-client.py` utility to send FCP version 1
requests through a USB-to-RS485 adapter. Prefer the stable path under
`/dev/serial/by-id/` rather than a changeable `/dev/ttyACM*` name:

```sh
ls -l /dev/serial/by-id/
```

For the Waveshare adapter currently used during commissioning, set:

```sh
FCP_PORT=/dev/serial/by-id/usb-1a86_USB_Single_Serial_586D012048-if00
```

The Linux client currently supports these operations:

| Command         | Opcode | Purpose                                      |
| --------------- | ------ | -------------------------------------------- |
| `echo`          | `0x01` | Return the supplied test payload             |
| `discover`      | `0x02` | Discover controllers on the RS485 bus        |
| `capabilities`  | `0x03` | Read supported protocol capabilities         |
| `info`          | `0x04` | Read controller identity and version         |
| `health`        | `0x05` | Read protocol health counters                |
| `list-points`   | `0x10` | Enumerate input and output point IDs         |
| `read-point`    | `0x12` | Read one named input or output               |
| `subscribe`     | `0x13` | Subscribe to an output bitmap                |
| `changes`       | `0x14` | Collect the pending subscription event       |
| `read-io`       | `0x15` | Read all 16 inputs and outputs               |
| `set-output`    | `0x18` | Authenticated arbitrated output command      |
| `relinquish`    | `0x19` | Relinquish the caller's arbitrated command   |
| `set-outputs`   | `0x1a` | Replace the complete 16-output bitmap        |
| `close-session` | `0x32` | Close a newly authenticated session          |
| `list-flows`    | `0x40` | List committed flow metadata                 |
| `flow-metadata` | `0x41` | Read committed flow metadata                 |
| `upload`        | `0x42` | Upload, validate, and atomically commit a file |
| `upload-status` | `0x43` | Read volatile upload progress                |
| `download`      | `0x48` | Download the committed artifact exactly      |
| `activate`      | `0x4a` | Atomically activate the committed flow       |
| `deactivate`    | `0x4b` | Atomically deactivate the committed flow     |
| `remove-flow`   | `0x4c` | Remove an inactive committed flow            |
| `flow-runtime`  | `0x4d` | Read current committed/active metadata       |

The client generates a fresh random 16-bit transaction ID for every invocation
so separate commands cannot be mistaken for retransmissions. Use
`--transaction 0x1234` only when deliberately testing duplicate-request
handling.

Discover attached controllers using the default 115200 bps link:

```sh
./scripts/fcp-client.py "$FCP_PORT" discover
```

Read device information, capabilities, and health from the factory-default
controller address `0`:

```sh
./scripts/fcp-client.py "$FCP_PORT" info --address 0
./scripts/fcp-client.py "$FCP_PORT" capabilities --address 0
./scripts/fcp-client.py "$FCP_PORT" health --address 0
```

Read all 16 inputs and 16 outputs as one coherent bitmap, or read one named
point:

```sh
./scripts/fcp-client.py "$FCP_PORT" read-io --address 0
./scripts/fcp-client.py "$FCP_PORT" read-point --address 0 --point input-01
./scripts/fcp-client.py "$FCP_PORT" read-point --address 0 --point output-16
./scripts/fcp-client.py "$FCP_PORT" list-points --address 0
```

In the block result, bit 0 is channel 1 and bit 15 is channel 16. A set bit
means logically active. Write one output or the complete output bitmap:

```sh
./scripts/fcp-client.py "$FCP_PORT" set-output --address 0 --point output-01 --state on --key "$FCP_KEY"
./scripts/fcp-client.py "$FCP_PORT" set-output --address 0 --point output-01 --state off --key "$FCP_KEY"
./scripts/fcp-client.py "$FCP_PORT" set-outputs --address 0 --outputs 0x0005 --key "$FCP_KEY"
```

Both output commands require `--key` and are unicast-only. Authentication does
not prevent observation or denial of service by someone with physical bus
access, so secure the cabinet and physical bus wiring.

Provision a unique 32-byte protocol credential through the authenticated
terminal's **Settings > Protocol key** option. The value is write-only and is
entered as 64 hexadecimal characters. Generate one with OpenSSL:

```sh
openssl rand -hex 32
```

Python's standard library is an equivalent fallback:

```sh
python3 -c 'import secrets; print(secrets.token_hex(32))'
```

Enter the generated value in the terminal settings and store it in an
appropriate secret manager. Do not paste the key directly into a shell command,
where it would be retained in shell history. Load it into a protected shell
variable interactively when using the client:

```sh
read -rsp "FCP key: " FCP_KEY; echo
```

The client uses the variable to establish a fresh HMAC session for each
protected invocation:

```sh
./scripts/fcp-client.py "$FCP_PORT" set-output --address 0 --point output-01 --state on --key "$FCP_KEY" --priority 8
./scripts/fcp-client.py "$FCP_PORT" relinquish --address 0 --point output-01 --source-id fcp-client --key "$FCP_KEY"
```

Generate and provision a replacement immediately if a key is exposed. Clear
the variable when the session is finished:

```sh
unset FCP_KEY
```

Upload an immutable compiled artifact, activate it separately, and verify an
exact download. Schema 1 is currently an opaque bounded artifact; activation
does not yet execute it because evaluator bytecode is deliberately outside
Phase 9.

```sh
./scripts/fcp-client.py "$FCP_PORT" upload --address 0 --key "$FCP_KEY" --file flow.fca --flow-id plant-1 --revision 1 --schema 1
./scripts/fcp-client.py "$FCP_PORT" activate --address 0 --key "$FCP_KEY"
./scripts/fcp-client.py "$FCP_PORT" download --address 0 --key "$FCP_KEY" --file downloaded.fca
cmp flow.fca downloaded.fca
./scripts/fcp-client.py "$FCP_PORT" deactivate --address 0 --key "$FCP_KEY"
./scripts/fcp-client.py "$FCP_PORT" remove-flow --address 0 --key "$FCP_KEY"
```

Verify a transaction-correlated echo:

```sh
./scripts/fcp-client.py "$FCP_PORT" echo --address 0 --text "RS485 test"
```

Pass `--baud` after the command when the controller uses another configured
rate, for example `health --address 42 --baud 57600`. Run
`./scripts/fcp-client.py --help` for all options. If opening the adapter fails
with `Permission denied`, add the Linux user to the group owning the device
(commonly `dialout`) and start a new login session. Connect RS485 A to A, B to
B, and signal ground to ground before testing; swap A and B if the adapter and
controller use opposite terminal naming.

Implemented capabilities are summarized in [`FEATURES.md`](FEATURES.md), the
remaining roadmap is in [`IMPLEMENTATION_PLAN.md`](IMPLEMENTATION_PLAN.md), and
the normative bespoke wire contract is in [`PROTOCOL.md`](PROTOCOL.md).
Board-specific wiring and commissioning notes belong under that board's
directory.
