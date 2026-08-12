# ADR 0004: Flow IL v2 contains a compiler-scheduled instruction stream

- Status: accepted
- Date: 12 August 2026

## Context

Schema 1 transports graph topology and makes each target validate ports,
connections, cycles, and scheduling. Production targets should execute the
same compiler result without embedding a second graph compiler.

## Decision

The backend is the authoritative graph compiler. It resolves immutable source
dependencies, validates graph and target contracts, rejects combinational
cycles, applies deterministic Kahn ordering, allocates typed slots/state, and
emits canonical Flow IL v2.

V2 targets load an ordered instruction stream, explicit operands, slot and
state layouts, point bindings, commit plan, requirements, symbols, and optional
debug map. A loader checks framing, canonical representation, capabilities,
bounds, operand types, and ordering invariants. It does not reconstruct graph
topology or select an execution order.

## Consequences

Identical resolved inputs must compile byte-for-byte identically. Browsers and
controllers never compile designer graphs. The server, emulator, and hardware
controller execute one artifact through the same VM semantics, and unsupported
requirements fail before activation.
