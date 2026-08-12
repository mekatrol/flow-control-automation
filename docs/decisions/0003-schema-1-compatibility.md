# ADR 0003: Executable-flow schema 1 baseline

- Status: superseded before production
- Date: 12 August 2026

## Context

Schema 1 was shared by the early backend compiler, controller loader, portable
evaluator, debugger, and golden fixtures. It is graph-shaped and
constructs a Kahn schedule on the target, so extending it into the production
instruction format would blur compatibility and compiler/runtime boundaries.

## Decision

This decision is superseded because the product has no deployed flows and has
not reached its production compatibility milestone. Schema 1 is an historical
measurement/reference only, not a supported input. The compiler, decompiler,
and production hosts support only the current Flow IL version and reject other
versions. Remaining schema-1 controller code is removed when the current IL
controller path replaces it; no dual-format window is retained.

## Consequences

Pre-release format changes update all producers, consumers, and fixtures
together. Version fields remain explicit so unknown formats fail clearly.
Backward-compatibility policy begins only after a separately recorded production
release milestone.
