# Deployed flow debug exclusion implementation plan

## Purpose

Prevent a deployed flow and a debugger context for that flow from executing at
the same time. Starting a debugger context must temporarily suspend an enabled
deployment, expose that state to every client, and reserve the flow so a second
debugger cannot start. Stopping the context, or explicitly re-enabling the flow,
must release the reservation and leave the system in a deterministic state.

This coordination is backend-owned. Browser memory, local storage,
`BroadcastChannel`, and assumptions about one user or one computer are not
authoritative.

## Required outcomes

1. The flow list displays the last successful deployed execution date and time,
   using the same presentation style as **Updated**.
2. Creating a debugger execution context for a flow with an enabled deployment
   atomically stops that deployment and marks it temporarily disabled.
3. A deployment which was already manually disabled remains manually disabled;
   debugging does not create a temporary-disable marker for it.
4. Temporary disable is distinct from the existing `Flow.Disabled` setting and
   does not change the user's persistent enable/disable choice.
5. Stopping, expiring, or otherwise terminating the debugger context clears its
   temporary-disable marker. If the marker represented a previously enabled
   deployment, the deployment resumes.
6. Leaving the flow workspace while a debugger context is active prompts the
   user. Confirming stops the context before navigation; cancelling remains on
   the page.
7. A temporarily disabled flow shows **Reenable** in the flow list. Re-enabling
   it terminates the owning debugger context and resumes the deployment.
8. Forced termination is observable by the debugger UI in another tab,
   browser, computer, or user session.
9. Creating a second debugger context for the same flow returns a conflict. It
   never replaces the first context.
10. Simulator contexts remain independent unless a later safety requirement
    explicitly brings them into this exclusion policy.

## Existing implementation and gaps

- `Flow.Disabled` is stored in the serialized `Flow` record and is the user's
  durable enable/disable setting. It must not be reused for debugger suspension.
- `FlowExecutionContextRegistry` is a singleton in-memory registry. Its
  check-then-add flow lookup is not an atomic per-flow reservation, and it
  cannot be queried by the database-backed flow list.
- `CreateFlowExecutionContext.ReplaceExisting` currently permits replacement,
  and `AppFlowDesignerView.vue` sends `replaceExisting: true`. This contradicts
  the required second-debugger conflict.
- `FlowRuntimeService` owns the deployed runtime and can stop and deploy flows,
  but it has no temporary-suspension operation tied to an execution-context ID.
- `useRuntimeContext` polls only while a context is running. A ready or paused
  context therefore cannot promptly learn that another client re-enabled the
  flow and stopped it.
- `AppFlowDesignerView.vue` currently stops a context silently during route
  changes and unmount. Its navigation prompt only protects unsaved edits.
- Runtime snapshots contain scan timestamps, but `Flow` has no persisted
  `LastExecutedAt` value for the list API.

## State model

### Flow fields returned by the API

Extend `Server.Common.Models.Flow` and the frontend flow DTO/domain model with:

```text
lastExecutedAt: string | null
temporaryDisable: null | {
  contextId: string
  startedAt: string
}
```

The list only needs to know whether temporary disable is active, but returning
the owning `contextId` lets the API and UI correlate state without exposing a
user identity. Do not expose private operator details in list responses.

`disabled` continues to mean manually disabled. Effective deployment execution
is allowed only when:

```text
has deployed version && !disabled && temporaryDisable == null
```

Prefer naming such as `TemporaryDisable`/`TemporarilyDisabled` throughout the
contracts. Avoid a second ambiguous `Enabled` boolean.

### Authoritative debug lease

Persist a debug lease in the backend database, either as a dedicated
`FlowDebugLeaseEntity` with a unique `FlowId`, or as equivalent normalized
columns on the flow entity. A dedicated entity is preferred because it gives a
database-enforced one-active-debugger-per-flow constraint and keeps transient
lifecycle data out of the versioned flow JSON.

Minimum fields:

```text
FlowId (unique/primary key)
ExecutionContextId (unique)
StartedAt
LastHeartbeatAt
SuspendedEnabledDeployment (bool)
RowVersion
```

The lease is the authoritative reservation and temporary-disable marker. The
in-memory registry remains the owner of live VM/transport objects, but it is no
longer the authority for exclusivity. This supports different users and
computers and leaves a clean path to multiple API processes. If multi-process
hosting is supported now, add an instance-owner ID and route/dispatch context
operations to that owner; otherwise document single API-process execution as a
deployment constraint while still enforcing reservations in the database.

### Lifecycle transitions

| Trigger | Backend result |
| --- | --- |
| Create debugger, enabled deployed flow | Acquire lease, stop deployment, create context, expose temporary disable |
| Create debugger, manually disabled flow | Acquire lease and create context; do not mark `SuspendedEnabledDeployment` |
| Create simulator | No debug lease or deployment change |
| Stop/expiry/fault cleanup of debugger | Stop context, delete lease, resume only when `SuspendedEnabledDeployment` is true and `disabled` is still false |
| Reenable from list | Stop owning context, delete lease, resume deployment; manual `disabled` remains unchanged unless this endpoint explicitly represents normal Enable |
| Second debugger create | `409 flow_already_being_debugged`; existing context and deployment state are unchanged |
| Backend restarts with stale lease | Reconcile lease, clear it, and resume an eligible deployment before accepting new work |

The create and release paths must be idempotent. A retry must not create two
contexts, resume twice, or release another context's lease. Lease deletion must
match both `FlowId` and `ExecutionContextId`.

## Backend design

### 1. Persistence and migration

- Add the debug-lease entity, unique indexes, concurrency token, EF mapping, and
  migration under `Server.Data`.
- Add `LastExecutedAt` as a nullable persisted flow metadata field. Prefer a
  real column over rewriting the complete flow JSON on every scan. Map it into
  `Flow` when reading/listing.
- Add repository methods to atomically acquire, heartbeat, query, and
  compare-and-release a lease. Translate the unique constraint into the
  stable conflict code `flow_already_being_debugged`.
- Keep debug lifecycle writes separate from `UpdatedAt` and `Revision`:
  starting/stopping debugging and recording execution must not make a draft
  dirty, change its revision, or claim that its authored content was updated.

### 2. Runtime suspension coordinator

Introduce a backend coordinator used by execution-context creation, stop,
expiry cleanup, flow enable/disable, delete, and deployment. It owns the ordered
transition between the persisted lease and `FlowRuntimeService`.

Create debugger context:

1. Load and validate the saved flow/revision and target.
2. Begin the per-flow/database transaction and acquire the unique debug lease.
3. Record whether a deployed version exists and is effectively enabled.
4. Stop and drain the deployed runtime before loading the debugger VM. No new
   deployed scan may start after this point.
5. Create/register the debugger context with the same context ID as the lease.
6. Commit/publish the active temporary-disable state and return `201`.
7. On any failure, stop partial debugger resources, release the matching lease,
   and restore the deployment when it had been suspended.

The database transaction must not be held across slow controller I/O. Implement
this as a short persisted state machine (`acquiring`, `active`, `releasing`) or
an equivalent per-flow coordinator with compensating cleanup. Other create,
deploy, enable, and disable operations must reject or wait on an in-progress
transition; they must never observe both runtimes active.

Release/reenable:

1. Mark the matching lease `releasing` so no new debug context can acquire it.
2. Stop and dispose the debugger VM/controller session.
3. Remove the context from the active registry while retaining a terminal
   tombstone long enough for clients to observe `stopped` and its reason.
4. Resume the latest deployed snapshot only when the flow is not manually
   disabled and the lease suspended an enabled deployment.
5. Delete the matching lease and publish the state change.

Use a machine-readable terminal reason such as `stopped_by_reenable`, alongside
a user-facing diagnostic. `GET /api/execution-contexts/{id}` should return the
terminal context during the tombstone window rather than immediately becoming
404.

### 3. Execution-context API behavior

- Make debugger create non-replacing. Remove `ReplaceExisting` from the public
  request if compatibility permits; otherwise ignore/reject `true` for debugger
  mode and remove it in the next contract revision.
- Preserve replacement semantics for simulator mode only if it is still a
  deliberate product requirement.
- Return structured errors, at minimum:
  `flow_already_being_debugged`, `debug_transition_in_progress`, and
  `execution_context_stopped`.
- Ensure every debugger termination path calls the coordinator: explicit
  DELETE/stop, lease expiry, creation rollback, target disconnect, service
  disposal, flow deletion, and administrative re-enable.
- Change registry expiration so it asynchronously invokes coordinated cleanup;
  do not perform blocking `.GetAwaiter().GetResult()` disposal while bypassing
  database state.
- Heartbeat the persisted lease from keepalive/context access. Reconcile expired
  leases with both the registry and runtime so a crashed/abandoned debugger
  cannot leave a flow suspended forever.

### 4. Reenable endpoint and normal enable/disable

Add an explicit command, for example:

```http
POST /api/flows/{flowId}/reenable
```

This is clearer and safer than overloading `/enable` because **Reenable** means
“terminate the debugger which owns the temporary suspension, then restore the
deployment.” It must be an atomic/idempotent backend workflow and return the
updated flow representation. If no lease exists, return the current flow (or a
stable `409` if strict operator confirmation is preferred) without stopping an
unrelated context.

Normal `/disable` remains a durable operator choice. If invoked during debug,
set `Disabled = true`; when debug later stops, do not resume deployment.
Normal `/enable` clears the durable flag but must not bypass an active debug
lease. It leaves the flow temporarily disabled until `/reenable` or normal
debug stop terminates the context.

Deployment and scan-once endpoints must check the lease and reject operations
that could run the deployed flow during debugging.

### 5. Last executed timestamp

Define `LastExecutedAt` as the UTC completion time of the most recent successful
deployed production scan, after outputs have been published. Debugger and
simulator steps do not update it. Failed or cancelled scans do not update it.

`FlowRuntimeService.ExecuteScanAsync` is the source event. Persist through a
small execution-metadata service rather than mutating the authored `Flow`
record. To avoid a database write on every high-frequency scan, update the
in-memory value every scan and checkpoint at a bounded cadence (for example,
once per 5 seconds per flow), flushing on stop/shutdown. Document that the list
timestamp is eventually consistent within that bound. If exact per-scan crash
durability is required, persist every scan and measure the write cost before
release.

Add `lastExecutedAt` to list/get DTO parsing and tests. Null renders as an em
dash and sorts before actual timestamps if sorting is later enabled.

### 6. Cross-client state propagation

The backend is authoritative; the frontend needs a transport to observe it.
Use the execution-context event stream proposed by the unified execution
context architecture (`GET /api/execution-contexts/{id}/events`) and publish
lifecycle/diagnostic updates when reenable or cleanup stops a context. Include
event IDs and send the current snapshot on connection/reconnection.

If SSE is deferred, poll active contexts in every non-terminal lifecycle
(`ready`, `running`, `paused`, and `stepping`), not only while running. Polling
must treat a terminal response or 404-after-tombstone as forced termination.
SSE is preferred because requirement 6 expects prompt cross-tab behavior and
avoids permanent 250 ms polling.

The flow list may refresh after its own mutation. Live list updates in other
clients are useful but not required for debugger correctness; if added, publish
a flow-state event using the same backend event infrastructure.

## Frontend changes

### Flow list

- Extend `flowDto.ts`, flow domain types, stores, and fixtures for nullable
  `lastExecutedAt` and temporary-disable metadata.
- Add a **Last executed** column to `AppFlowTable.vue`, formatted with the same
  locale date/time helper as **Updated** and showing `—` when absent.
- Choose the action label with this precedence:
  **Reenable** when temporary disable exists; otherwise **Enable** when
  `disabled`; otherwise **Disable**.
- Emit a distinct reenable action and call the new endpoint. Do not simulate it
  by clearing `disabled` in the browser.
- Disable the action while the request is pending and surface backend conflict
  or transition errors without optimistic state divergence.

### Debugger workspace

- Send debugger create with replacement disabled/omitted. Present a `409
  flow_already_being_debugged` as “This flow is already being debugged.”
- After create, consume the returned temporary-disable/context lifecycle state.
  Do not independently call normal Disable.
- Subscribe to context events (or continuously poll all active lifecycles).
  When another client reenables the flow, clear debugger controls, stop local
  polling/stream resources, and show that the context was stopped because the
  deployed flow was re-enabled.
- Treat stop as complete only after the backend acknowledges it. Do not clear
  the local context first.

### Navigation prompt

Unify the existing dirty-draft route guard with an active-debug guard so only
one dialog is shown. A context is active for navigation purposes in
`preparing`, `ready`, `running`, `paused`, or `stepping`.

- Message: “This flow is currently being debugged. Leaving will stop the debug
  context and re-enable its deployed version.” Adjust the last clause when no
  deployment was suspended.
- Cancel keeps the user in the workspace.
- Confirm awaits backend stop; navigate only after success. If stop fails,
  remain on the page and display the error.
- When both unsaved changes and active debugging exist, the dialog must explain
  both effects and confirmation must perform both selected actions.
- Same-flow navigation between designer/debugger workspace routes should not
  prompt or stop merely because the component route mode changes, unless that
  navigation truly destroys the context owner.
- `beforeunload` can only display browser-owned text. Set `returnValue` whenever
  debugging is active (or the draft is dirty), but do not fire-and-forget a stop
  during unload. The backend lease timeout handles abandoned tabs; an optional
  `sendBeacon` is best effort only, never the correctness mechanism.
- Remove unconditional stop calls from generic unmount/route watchers where
  they can bypass the confirmation outcome.

## Concurrency and recovery rules

- Enforce exclusivity with a database unique constraint, not only a process
  semaphore or `ConcurrentDictionary` search.
- Serialize deploy, manual enable/disable, debug acquire/release, reenable, and
  flow delete for the same flow. Operations for different flows remain
  concurrent.
- Every mutation carries the expected context/lease identity. Late cleanup from
  context A must never release context B.
- A second create during `acquiring`, `active`, or `releasing` returns 409; it
  does not wait and surprise the user by starting later.
- Startup reconciliation examines persisted leases before normal deployment
  activation. Stale leases are cleaned and eligible deployments restored.
- Lease expiry is based on backend time (`TimeProvider`) and heartbeat, not
  browser clocks.
- Reenable is idempotent across retries and concurrent clicks. All callers
  converge on one stopped context and one resumed deployment.

## Implementation phases

### Phase 1: Contract and persistence (complete)

- Add `LastExecutedAt`, temporary-disable response metadata, structured error
  codes, debug lease entity/repository, EF migration, and serialization tests.
- Add database uniqueness/concurrency tests and startup lease reconciliation.

Completed 2026-09-20. Implemented persisted `LastExecutedAt` metadata, API and
frontend temporary-disable contracts, a database-enforced `FlowDebugLeaseEntity`
with heartbeat and optimistic concurrency support, identity-safe lease repository
operations, startup cleanup of leases abandoned by a prior API process, and the
`flow_already_being_debugged` conflict code. Focused backend persistence,
architecture, and flow tests pass, as do frontend DTO tests, type checking, lint,
format checking, and the production build.

### Phase 2: Backend lifecycle integration

- Add the suspension coordinator and integrate debugger create/stop/expiry.
- Remove debugger replacement behavior.
- Gate deployment, enable/disable, scan-once, delete, and shutdown through the
  coordinator.
- Add `/reenable`, terminal tombstones/reasons, and last-executed checkpointing.

### Phase 3: Client synchronization and UI

- Implement execution-context SSE, or the bounded all-active-state polling
  fallback.
- Update API parsers/stores and add the list column/action.
- Update debugger conflicts, forced-stop presentation, and the combined route
  and unload guards.

### Phase 4: Hardening and rollout

- Exercise controller and server debugger targets, process restart, lease
  expiry, failed deploy resume, and slow/failing stop paths.
- Add metrics/logs for lease acquisition conflicts, stale-lease recovery,
  suspend/resume failures, forced stops, and execution timestamp checkpoint
  failures.
- Deploy the migration before or with code that reads the new fields. Treat
  missing timestamps as null and no lease as not temporarily disabled.

## Verification plan

### Backend unit/integration tests

- Enabled deployed flow: debugger create drains/stops runtime, creates one
  lease, and reports temporary disable.
- Manually disabled deployed flow: debugger create succeeds without a
  resumable temporary-disable marker; stop leaves it disabled.
- Draft with a previous deployed version uses that deployed runtime state when
  deciding suspension, while debugging the saved requested revision.
- Two simultaneous debugger creates for one flow yield exactly one `201` and
  one `409 flow_already_being_debugged`.
- `replaceExisting: true` cannot replace an active debugger.
- Context stop, DELETE, expiry, target failure, rollback, and shutdown release
  only their own lease and restore only eligible deployments.
- Reenable stops the owning context, returns it as terminal with
  `stopped_by_reenable`, resumes once, and is idempotent under concurrent calls.
- Manual disable during debug prevents automatic resume.
- Deploy/scan-once cannot run while a debug lease is active.
- Stale lease startup reconciliation restores an eligible deployment.
- A successful production scan advances `LastExecutedAt`; debug/simulator and
  failed scans do not; checkpoint throttling and flush behavior use fake time.

### Frontend component/unit tests

- Flow DTO validation accepts null/valid last-executed values and validates
  temporary-disable metadata.
- Table formats **Last executed**, renders `—`, and gives **Reenable** precedence
  over Enable/Disable.
- Reenable calls the dedicated endpoint and refreshes returned server state.
- Debug create omits replacement and renders the already-debugging conflict.
- A server-pushed/polled forced stop disables controls and shows the reason.
- Route guard covers active debug only, dirty draft only, and both together;
  cancellation stays, confirmation awaits stop, and stop failure blocks leave.
- `beforeunload` prompts for active debug without treating best-effort browser
  cleanup as authoritative.

### End-to-end scenarios

1. Deploy an enabled flow, start debugging, verify production scans stop and the
   list in a second browser shows **Reenable**.
2. Click **Reenable** in the second browser and verify the first browser exits
   active debugging and production execution resumes.
3. Open two debugger tabs and create concurrently; verify one succeeds and the
   other receives the already-debugging message without disturbing the first.
4. Start debug as user A and reenable as user B on another computer/session;
   verify the same outcome as scenario 2.
5. Close/crash the debugger browser, allow the lease to expire, and verify the
   backend clears temporary disable and resumes the deployment.
6. Manually disable a flow, debug and stop it, and verify it never auto-resumes.
7. Navigate away with an active context, cancel once, then confirm; verify the
   context remains active after cancel and is stopped before confirmed leave.
8. Verify **Last executed** advances only for deployed execution and survives a
   backend restart within the documented checkpoint bound.

## Completion criteria

- No deployed and debugger runtime for the same flow can execute concurrently,
  including under simultaneous requests.
- Exclusivity and temporary-disable state are backend-enforced and visible
  across users, browsers, and computers.
- Reenable reliably terminates the owning debugger and the debugger client
  observes a terminal state without relying on same-browser messaging.
- A second debugger cannot replace or disturb the first.
- Temporary suspension never mutates the durable manual disable preference or
  flow revision.
- Last-executed data has defined semantics, bounded persistence behavior, and a
  consistent list presentation.
- Backend, frontend, and cross-client E2E suites cover all eight requested
  behaviors and the principal crash/concurrency paths.
