# Executable-flow schema 1 baseline

Recorded 12 August 2026 before Flow IL v2 implementation began.

## Byte compatibility baseline

`testdata/contracts/flow-executable-v1/manifest.json` is the authoritative list
of the eight schema-1 artifacts, their exact lengths, SHA-256 digests, and
stable validation results. The largest checked-in artifact is 425 bytes. The
canonical and source-order-permuted valid artifacts are both 425 bytes with
SHA-256 `140a75775e5764f3554ec82a4578689706a5ece3cfb34a70af8e0518dc20736e`.

Run this non-writing verification from the repository root:

```sh
node tools/generate-flow-contract-fixtures.mjs --check
```

The command recompiles all eight source graphs in memory and compares every
generated artifact and companion file with the checked-in bytes. CI runs it
before the portable C contract suite. The C and .NET suites independently
verify the manifest digests, validation outcomes, scheduling invariance, tick
snapshots, and failure atomicity.

## Fixed-capacity resource baseline

The schema-1 implementation has the following target-independent limits:

| Resource | Baseline |
| --- | ---: |
| Artifact buffer | 8,192 bytes |
| Nodes | 128 |
| Ports | 384 |
| Connections | 384 |
| Point bindings | 64 |
| Proposed outputs | 64 |
| Serialized debug snapshot buffer | 16,384 bytes |
| Tick working Boolean images | 256 bytes |
| Prepare scheduling edges | 384 bounded `kahn_edge_t` records |
| Prepare scheduling degree/selection workspace | 384 bytes |

`flow_resource_baseline_tests` asserts these invariant capacities and prints
the ABI-specific sizes of `flow_executable_t`, `flow_runtime_t`,
`flow_tick_snapshot_t`, and `flow_debug_t`. Run it with the full controller host
suite, or directly from its configured build directory. Structure sizes are
reported rather than frozen because alignment and pointer width legitimately
differ between an x64 host and ESP32-S3 firmware; capacity and behavior are the
portable contract.

The prepare path stores its decoded representation in a caller-owned
`flow_executable_t`. Its only maximum-size scheduling workspace is the bounded
edge, degree, and selection arrays above. A tick uses two 128-byte Boolean
working images and constructs one bounded replacement `flow_tick_snapshot_t`
before publishing. It performs no heap allocation. The runtime retains current,
next, and visible 128-byte Boolean images plus its latest snapshot. Snapshot
serialization must fit the independent 16,384-byte debug buffer or preparation
fails with `FLOW_REASON_SNAPSHOT_TOO_LARGE`.

These measurements are the comparison point for Flow IL v2. V2 work must state
changes to artifact capacity, prepared storage, tick workspace, retained
runtime state, and snapshot capacity explicitly rather than inheriting schema-1
layout accidentally.
