# ADR 0005: Virtual points are instance-global shared state

- Status: Accepted
- Date: 2026-08-22

## Context

Portable flow programs need to exchange values without embedding a concrete
server, controller installation, or controller template in flow source. The
runtime may schedule several programs concurrently, and one logical context may
be deployed to several heterogeneous targets.

Using flow ID, context ID, or controller-template ID as the runtime namespace
would either prevent intended communication or leak state between independent
installations. Allowing unsynchronized or last-writer-wins access would make
results depend on scheduling order.

## Decision

Virtual-point runtime identity is `(executionInstanceId, pointKey)`. Every
program on one execution instance shares the same compatible cell for a key;
the same key on another instance is independent.

A flow remains portable and declares analog or digital point requirements. A
logical execution context selects immutable flow revisions and merges their
contracts. A context deployment resolves physical bindings and materializes the
composition on one concrete execution instance.

The host owns the shared store. Programs read immutable start-of-scan snapshots,
stage writes privately, and atomically commit a complete output set after a
successful scan. Other programs observe the result on their next context scan.
Many readers are permitted, but one active program owns the writer role for a
point on an instance.

Retained values restore only for an exact execution-instance and contract match.
No migration, coercion, or fallback behavior is provided. Virtual points are
limited to analog and digital in the first release.

## Consequences

- Flow source stays target-neutral and may participate in multiple contexts.
- Contexts can be deployed independently to compatible server and controller
  instances.
- Deployment must reconcile contracts and writer ownership against all active
  programs on the instance.
- Host implementations require synchronized allocation, snapshot, commit,
  reset, ownership, and restore operations.
- Operator commands remain privileged runtime operations rather than an
  additional flow writer.
- Priority arbitration and additional value types require a future explicit
  decision and contract change.

