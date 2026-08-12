# ADR 0003: Executable-flow schema 1 is permanently frozen

- Status: accepted
- Date: 12 August 2026

## Context

Schema 1 is deployed and shared by the backend compiler, controller loader,
portable evaluator, debugger, and golden fixtures. It is graph-shaped and
constructs a Kahn schedule on the target, so extending it into the production
instruction format would blur compatibility and compiler/runtime boundaries.

## Decision

Schema 1 bytes, canonical encoding, reason-code numbers, validation behavior,
tick behavior, and snapshot semantics are frozen. The artifact hashes recorded
in `testdata/contracts/flow-executable-v1/manifest.json` are the byte baseline.
Both C and .NET tests continue consuming those fixtures.

Bug fixes may reject unsafe input that was never valid under the normative
contract, but must not reinterpret valid schema-1 bytes. Any incompatible
encoding or semantic change uses a new version. Schema 1 remains available only
for compatibility and the existing controller-debug path; new production work
targets Flow IL v2.

## Consequences

Fixture changes require explicit compatibility review. New opcodes, debugger
frames, types, and scheduled execution are not added to schema 1. Loaders route
by explicit version and never guess or upgrade bytes in place.
