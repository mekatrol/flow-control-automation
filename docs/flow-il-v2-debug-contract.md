# Flow IL v2 debugger contract

## Identity and capabilities

Breakpoints use flow revision, stable node ID, phase (`before` or `after`), and
optional compiler discriminator. Browsers never submit byte offsets. The backend
resolves identities through the artifact debug map and rejects stale revisions,
unknown nodes, ambiguous lowering, and limits beyond the host's advertised
breakpoint/frame capacity.

Hosts independently advertise tick stepping, node stepping, instruction
stepping, continue, asynchronous pause, run-to, inspectable slot count, maximum
breakpoints, and paused-frame bytes. The UI displays unsupported operations; it
does not emulate controller instruction stepping on a different host.

## Paused frame and commands

At tick start the VM freezes one coherent input frame and current state. A
paused frame contains artifact identity, tick identity, instruction pointer,
working typed slots, current state, staged next state, proposed commands, and
the last completed snapshot. Inputs do not change while paused.

`step tick` runs through commit. `step instruction` executes exactly one encoded
instruction. `step node` runs through the final discriminator for that source
node. Continue stops before/after a configured breakpoint, on pause request, on
fault, or at the tick boundary. Run-to installs a bounded temporary breakpoint.

Inspection is read-only and returns type, value, quality, and stable identity.
Conditional and data breakpoints are not part of capability version 1.

## Commit safety

Pausing never publishes a runtime snapshot, advances committed state, or sends
a command. Only successful execution of opcode 255 followed by the host commit
call publishes state, commands, and a completed snapshot atomically. Abort,
stop, replacement, lease expiry, disconnect policy, debugger fault, host
shutdown, or controller reboot discards the uncommitted frame and relinquishes
debug-owned outputs. Live-output debugging remains commit-only and shadow mode
is the default.

Snapshot identity includes session, flow revision, tick, and content digest so
chunk consumers cannot combine frames. Diagnostics and inspection responses are
bounded and cannot expose credentials, physical addresses, or native pointers.
