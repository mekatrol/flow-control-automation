# Executable flow contract v1 fixtures

Each fixture directory contains the canonical compiler source graph, exact compiled
`artifact.bin`, a reviewable `artifact.hex` mirror, the decoded structure, and
the expected stable validation result. Valid execution fixtures additionally
contain coherent input frames and expected tick snapshots.

The `source-flow.json` files conform to the normative source language in
[`docs/controller-flow-source-contract-v1.md`](../../../docs/controller-flow-source-contract-v1.md).

`valid-source-order-permutation` deliberately reverses source node and
connection order and must remain byte-identical to `valid-two-button-and`.
`noncanonical-node-order` instead permutes the encoded node table and must be
rejected.

Regenerate all artifacts deterministically from the repository root:

```text
node tools/generate-flow-contract-fixtures.mjs
```

CI and reviewers can detect stale generated files without changing them:

```text
node tools/generate-flow-contract-fixtures.mjs --check
```

The artifact lengths and SHA-256 digests in `manifest.json` are the frozen
schema-1 byte baseline. Changing one is a compatibility event, not routine
fixture maintenance. The check command recompiles every source fixture in
memory and fails rather than rewriting any differing artifact or companion
file.
