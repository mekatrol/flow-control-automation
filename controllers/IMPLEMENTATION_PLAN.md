# Controller communications implementation plan

## Purpose

This plan tracks only unfinished work for the portable communications
foundation. Implemented capabilities are documented in [`FEATURES.md`](FEATURES.md),
the bespoke wire contract is defined in [`PROTOCOL.md`](PROTOCOL.md), and build
and commissioning instructions are in [`README.md`](README.md).

The flow evaluator and remote firmware updates remain outside this plan.
Compiled flow artifacts can be transferred and activated, but the current
schema is opaque and activation does not execute it.

## Architectural rules

- The platform entry point calls the shared `controller_main`, which performs
  bounded initialization, starts the controller runtime, and returns.
- Every communications subsystem is independently supervised. A failed
  subsystem reports degraded status and retries without blocking unrelated
  controller work.
- Queues, retries, payloads, topic lengths, diagnostic messages, sessions, and
  transfers are bounded.
- Credentials are provisioned through the authenticated terminal, loaded from
  typed persistent settings, and excluded from logs and committed defaults.
- Shared services depend only on platform and board contracts. Framework,
  processor, operating-system, and board headers remain in adaptation layers.
- State transitions expose read-only health information for diagnostics and
  future flow-runtime consumers.
- All code follows [`AGENTS.md`](AGENTS.md) and [`.clang-format`](.clang-format).

## Planned work

No implementation phases are currently planned. Add the next feature plan in
this section when its scope and deliverables are defined.

## Completion requirements

- Production code and focused automated tests are included together.
- Every supported board builds from the command line and VS Code tasks.
- On-target steps and expected diagnostics are documented.
- Failure paths are tested, not only successful startup.
- Configuration has safe empty defaults and contains no committed secret.
- Resource ownership, shutdown, retry, and queue limits are explicit.
- Completed work is moved to [`FEATURES.md`](FEATURES.md).
