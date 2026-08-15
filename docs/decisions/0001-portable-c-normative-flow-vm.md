# ADR 0001: Portable C is the normative Flow VM

- Status: superseded for server execution by ADR 0003; retained for controller firmware
- Date: 12 August 2026

## Context

Server and controller execution must produce identical results. Independent C#
and firmware evaluators would duplicate opcode, state, quality, overflow, and
commit semantics and could diverge despite shared fixtures.

## Decision

The portable C implementation under `controllers/shared/flow/` is the single
normative Flow VM. It is built as a native library for `Server.Services`, a
host-test library, and a firmware library. Hosts supply bounded adapters for
points, time, scheduling, persistence, commands, and diagnostics. They do not
reimplement opcode semantics.

The public ABI uses caller-owned buffers, fixed-width values, explicit lengths,
and result codes. VM operations do not throw across the ABI or allocate during
a tick.

## Consequences

Every semantic change is implemented once and exercised through shared
fixtures on all hosts. Native compilation and ABI packaging become required for
the server. If the native VM is unavailable, deployment fails visibly and an
existing runtime remains unchanged; there is no C# evaluator fallback.
