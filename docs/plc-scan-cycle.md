# PLC Scan Cycle execution model

Flow Control Automation executes every deployed flow using a strict PLC Scan
Cycle. This is the normative runtime model for the server VM, controller VM,
controller emulator, debugger, tests, and future Flow IL versions.

The scan repeats continuously for a running flow. Event-driven flows execute
one scan for each admitted event batch; interval flows execute one scan at each
scheduled interval. A runtime never overlaps two scans of the same flow.

## The three phases

### 1. Read Inputs

Capture one coherent, immutable input image containing all required point
values, qualities, timestamps, event inputs, and the flow's committed current
state. The image is frozen for the entire scan. An input that changes while
logic is executing is observed in a later scan, never halfway through this one.

If a required input is missing, stale, bad quality, or incoherent under its
declared policy, the scan fails before publishing any state or output.

### 2. Execute Logic

Evaluate the compiler-scheduled Flow IL instruction stream against only the
frozen input image and committed current state. Instructions write private
working slots, staged next state, and proposed output commands. They do not
modify live points or committed state.

Logic order is deterministic. The backend rejects combinational cycles and
produces a stable schedule before deployment. Feedback is represented by an
explicit stateful instruction such as memory, delay, latch, or timer. It reads
its committed value during the current scan and stages its replacement for the
next scan. This permits intentional feedback without recursion, arbitrary cycle
breaking, or order-dependent results.

### 3. Write Outputs

After every instruction succeeds, validate the staged result and atomically
commit next state, proposed point commands, and one immutable completed-scan
snapshot. Host command arbitration then determines the effective physical or
virtual output without changing VM logic semantics.

If execution faults, is cancelled, overruns a hard limit, or is aborted by the
debugger before commit, discard the entire working frame. Previous state and
outputs remain authoritative, subject to the host's safe-output policy.

## Observable consequences

- A scan is the same unit also called an atomic tick. Current APIs may use
  `tick` and `tickNumber`, while documentation should describe the complete
  operation as a PLC scan/tick.
- Logic never reads live I/O during Execute Logic.
- State staged during scan N is observed as current state during scan N+1.
- Debug instruction stepping pauses inside Execute Logic and cannot commit.
  Step Scan/Step Tick runs through Write Outputs.
- Input sampling, instruction execution, and output publication have separate
  timing and diagnostic fields.
- Server, emulator, and controller conformance tests run identical artifacts
  through the same phases and compare completed-scan snapshots exactly.

## Scheduling and safety

The host owns scan triggering and monotonic scheduling. An overrun never starts
an overlapping scan or an unbounded catch-up loop. The next admitted scan uses
a newly captured input image. Output drivers, command priority, expiry,
relinquishment, physical interlocks, and fail-safe behavior remain host adapter
responsibilities, but commands are issued only from a successful Write Outputs
phase.
