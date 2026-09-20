# Deployed flow and debugger exclusion

## Purpose

A production deployment and a debugger for the same flow must never execute at
the same time. They can address the same inputs and outputs, so concurrent
execution could duplicate commands, race state changes, and make observed
debugger behavior differ from production behavior.

The backend therefore owns a per-flow debug reservation. Browser state is not
authoritative: the rule continues to hold across tabs, browsers, users, and API
requests. Simulator contexts are excluded from this policy because they use
simulated I/O and do not replace a production runtime.

## State model

The persisted `FlowDebugLeases` row is both the exclusive reservation and the
source of temporary-suspension state. `FlowId` is its primary key and
`ExecutionContextId` is unique, so the database permits at most one debugger
for a flow and binds cleanup to the context that acquired the lease. The lease
also records its start and heartbeat times and whether it stopped an enabled
deployment.

This state is deliberately separate from `Flow.Disabled`:

- `disabled` is the operator's durable enable/disable choice.
- `temporaryDisable` reports that a debugger suspended a previously enabled
  deployment. It contains the owning `contextId` and `startedAt`.
- A debug lease may exist without `temporaryDisable` when the flow was already
  manually disabled. It still prevents a second debugger but must not cause a
  deployment to resume later.

A deployed flow may execute only when it has a deployed version, is not
manually disabled, and has no debug lease. Deploy and scan-once operations
check the reservation and fail rather than bypassing it.

## Lifecycle coordination

Debugger creation acquires the unique lease before starting the debugger. The
per-flow suspension coordinator then drains and stops an enabled deployed
runtime and starts the debugger using the lease's execution-context ID. If any
part of creation fails, compensating cleanup stops partial debugger resources,
releases only the matching lease, and restores a deployment that was suspended.
A concurrent debugger creation receives `409 flow_already_being_debugged` and
does not replace or disturb the first context.

Stopping or expiring a debugger, deleting its flow, and shutting down the API
all use the same coordinated release path. Cleanup is identity-safe: late work
for context A cannot release a lease owned by context B. The deployment resumes
only if the lease says it suspended an enabled deployment, the flow is still
not manually disabled, and a deployed snapshot still exists.

`POST /api/flows/{flowId}/reenable` is a distinct, idempotent command. It stops
the lease-owning debugger, retains a terminal context snapshot with reason
`stopped_by_reenable`, releases the reservation, and restores the eligible
deployment. It is not the same as normal enable: `/enable` changes the durable
operator preference but cannot bypass an active debug reservation.

The coordinator serializes lifecycle transitions for a flow inside one API
process, while database uniqueness makes lease acquisition atomic. The current
deployment supports one API process because live VM and debugger objects remain
process-local; requests are not routed to an instance owner. At startup the API
removes leases abandoned by the prior process before normal deployment
activation.

## Client behavior

Flow list responses expose `temporaryDisable` so every client sees the
backend-owned state. The list gives **Reenable** precedence over the normal
**Enable** or **Disable** action. A debugger client polls its execution context
every 500 ms in every active lifecycle, including ready and paused states.
This lets a different client's re-enable operation promptly produce the
terminal reason and disable local debugger controls. Terminal snapshots remain
queryable so clients can observe the stop reason; a later `404` is treated as
an already-stopped context.

Leaving a workspace with an active debugger requires confirmation. Confirming
awaits the backend stop before navigation; cancelling leaves the context
running. The browser's unload prompt is advisory only—lease expiry and backend
cleanup, rather than a best-effort browser request, provide the safety rule.

## Last-executed metadata

`lastExecutedAt` is the UTC completion time of the latest successful production
scan, after outputs have been published. Debugger and simulator execution, and
failed or cancelled production scans, do not update it. The value is stored as
flow metadata rather than authored flow content, so it does not change the
flow's revision or `updatedAt` value.

The runtime checkpoints this metadata at most once every five seconds per flow
to avoid a database write for every high-frequency scan. Consequently, the
persisted list value is eventually consistent within that interval during
normal operation. A checkpoint failure is logged and counted but does not fault
the production scan; the next eligible scan retries it. A missing value is
returned as `null` and displayed as an em dash.

Operational rollout, telemetry, and failure-path checks are documented in the
[deployed flow debug exclusion runbook](../operations/deployed-flow-debug-exclusion.md).
