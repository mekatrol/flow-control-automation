# ADR 0003: The server Flow VM is managed .NET

- Status: accepted
- Date: 15 August 2026

## Context

The server previously loaded the controller C VM through P/Invoke. That made a
native build artifact part of server startup and simulator availability. The
server must be deployable, diagnosable, and executable as a native C#/.NET
application without loading controller code.

## Decision

`Server.Services` parses and executes Flow IL using a managed C# VM. It contains
no P/Invoke declarations, unsafe VM code, or dependency on `flow_vm_shared`.
Controller firmware may continue to execute the same Flow IL in portable C.
Both implementations are held to the shared Flow IL fixtures and scan-result
conformance tests.

## Consequences

The server no longer needs a platform-specific native library. Changes to Flow
IL semantics must be implemented in both managed server and controller runtimes
and verified against common fixtures. Atomic scan, state, quality, timing,
debugging, and output-proposal behavior remain part of the shared contract.
