# ADR 0002: The .NET server loads the Flow VM through a narrow native boundary

- Status: accepted
- Date: 12 August 2026

## Context

`Server.Api` is the first production Flow IL host, while the normative VM is
portable C. Native failure must not corrupt managed process state or turn a
failed replacement into an outage of the prior flow generation.

## Decision

`Server.Services` owns one narrow managed wrapper over a versioned C ABI. The
wrapper validates managed lengths before each call, pins or copies only for the
duration documented by the ABI, translates result codes into bounded managed
diagnostics, and owns every native handle and buffer with deterministic cleanup.
No native pointer is exposed to API or frontend layers.

Library identity and ABI version are checked at startup. Prepare occurs in new
isolated runtime state; the active generation is swapped only after preparation
and initialization succeed. Expected load, validation, and execution failures
are contained at the affected flow boundary. Process-corrupting native faults
are not treated as recoverable in-process errors.

## Consequences

Deployment environments must ship the correct library for their runtime
identifier. Missing or mismatched libraries prevent new VM deployments and are
reported by health/diagnostics. The prior prepared generation is retained for
ordinary prepare failures, while process supervision remains responsible for
recovery from access violations or equivalent native corruption.
