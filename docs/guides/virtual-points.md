# Virtual points and execution contexts

Virtual points let portable flow programs exchange analog or digital values
without binding the flow source to a particular server or controller. Programs
running on the same execution instance share a virtual point by key. The same
key on another instance identifies an independent value.

For example, these are three independent runtime values:

```text
server/temp-setpoint
controller-east/temp-setpoint
controller-west/temp-setpoint
```

Two programs on `controller-east` that declare compatible `temp-setpoint`
contracts use the same host-owned cell.

## Concepts

A **flow program** is portable source code plus its declared point
requirements. It does not identify a controller template, installed controller,
or server VM.

A **logical execution context** selects immutable flow revisions and merges
their point contracts. It remains target-neutral and can be deployed more than
once.

An **execution instance** is one concrete VM host: the built-in server or an
installed controller. A controller template describes capabilities and limits;
it is not an instance or a runtime namespace.

A **context deployment** materializes one context on one instance. It resolves
physical bindings and records the exact context, instance, template, program,
mapping, compiler, and artifact revisions.

The complete relationship is defined in the
[portable runtime architecture](../architecture/portable-flow-runtime.md#programs-contexts-instances-and-shared-virtual-points).

## Declaring and using a virtual point

Virtual points in the first release are analog or digital. A declaration
contains:

- A stable program-visible key.
- Analog or digital value type.
- Units for analog values when applicable.
- Readable and commandable capabilities.
- Volatile or retained persistence.
- An optional typed relinquish default.

Analog Input, Digital Input, Analog Output, and Digital Output are the only
point-access nodes. Their selected point determines whether access is physical
or virtual. There are no separate virtual-input or virtual-output node kinds.

Declarations with the same key on one instance must agree on type, units,
persistence, default, and capabilities. Conflicts prevent deployment. Multiple
readers are allowed, but only one active program may own the writer role for a
point on an instance.

## Designer workflow

The flow designer edits a portable program and never writes a concrete
execution-instance ID into the graph.

1. Select an Analog or Digital Input or Output node.
2. Search for a point by key or display name, or enter a key manually.
3. Review its physical/virtual type, units, and read/write capability.
4. Optionally select an execution context as a validation preview.
5. Resolve physical roles later in the context-deployment screen.

The selector filters by node requirements:

| Node | Required contract |
| --- | --- |
| Analog Input | Analog and readable |
| Analog Output | Analog and commandable |
| Digital Input | Digital and readable |
| Digital Output | Digital and commandable |

Manual input is validated locally for syntax and authoritatively through the
point-resolution API. A network or service failure is reported as unavailable
validation, not as a missing point. An invalid or unresolved reference blocks
save and deployment.

If a key does not exist, the designer can create a typed virtual declaration,
merge it into every containing context, and select it after creation.

## Runtime behavior

- Runtime identity is `(executionInstanceId, pointKey)`.
- Every program reads an immutable start-of-scan snapshot.
- Successful writes are committed atomically after execution.
- Other programs observe committed values on their next context scan.
- A failed scan never publishes part of its output set.
- A point without a committed value uses its relinquish default when present;
  otherwise it reports unavailable quality.
- Volatile values reset with their execution instance.
- Retained values are restored only for the exact same instance and contract.
- Disabling or undeploying a writer releases ownership.

See [PLC scan cycle](../architecture/plc-scan-cycle.md) for the common execution
model and [virtual-point operations](../operations/virtual-points.md) for
retention, security, backup, and observability.

## Scope and constraints

- Virtual points are analog or digital.
- One active writer is allowed per point per execution instance.
- Priority and multi-writer arbitration are not supported.
- Manual commands are privileged operations, not an additional flow writer.
- A context supports at most 128 virtual points and 64 retained points.
- An execution instance supports at most 128 allocated virtual points.
- Flow Input and Flow Output node kinds are not supported.
- The feature is clean-slate: obsolete flow-interface data is neither imported
  nor converted.

