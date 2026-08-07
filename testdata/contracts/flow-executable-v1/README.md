# Executable flow contract v1 fixtures

Each fixture directory contains the editable source graph, exact compiled
`artifact.bin`, a reviewable `artifact.hex` mirror, the decoded structure, and
the expected stable validation result. Valid execution fixtures additionally
contain coherent input frames and expected tick snapshots.

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
