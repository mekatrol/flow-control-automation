# Deployed flow debug exclusion operations

## Rollout

The API applies `AddFlowDebugLeaseAndExecutionMetadata` automatically during
startup. Deploy the database migration before, or in the same release as, the
API and frontend which read `LastExecutedAt` and `FlowDebugLeases`. Do not run a
new frontend against an older API/database during a rolling deployment.

This release supports one API process. The database enforces one debugger per
flow across requests, but live debugger sessions are process-local and are not
routed between API instances.

Before allowing normal traffic, startup migrates the database and removes
debug leases left by a previous process. Existing flow rows receive a null last
execution timestamp, which the UI renders as an em dash. The absence of a lease
means the flow is not temporarily disabled.

After deployment, exercise both the `server` and a controller debugger target:

1. Deploy an enabled flow and confirm its production scan timestamp advances.
2. Start debugging and confirm production scans stop and a second create gets
   `409 flow_already_being_debugged`.
3. Stop debugging and confirm the deployment resumes.
4. Start debugging again, use **Reenable** from a second client, and confirm the
   debugger observes `stopped_by_reenable`.
5. Repeat with a manually disabled flow and confirm it does not auto-resume.
6. Terminate the API while debugging, restart it, and confirm the stale lease is
   removed before traffic is accepted.

## Observability

The `FlowControl.FlowDebug` meter publishes these counters:

- `flow_debug.lease_acquisition_conflicts`
- `flow_debug.stale_lease_recoveries`
- `flow_debug.suspension_failures`
- `flow_debug.resume_failures`
- `flow_debug.forced_stops`, tagged with `reason=reenable` or
  `reason=lease_expired`
- `flow_debug.execution_timestamp_checkpoint_failures`

The same exceptional transitions emit structured logs containing the flow and
execution-context identifiers where available. Alert on any suspension or
resume failure. Investigate sustained checkpoint failures as storage health
problems; they do not stop production scans, and checkpointing retries on the
next cadence. Lease conflicts are normally user contention, but a sudden rise
can indicate duplicate clients or retry storms.

## Failure-path checks

When validating a release candidate, inject a controller disconnect and a slow
or failing debugger stop. The owning lease must remain identity-safe: cleanup
for context A must never release context B. If resume fails, the lease remains
present so production cannot overlap debugging; resolve the deployment failure
and retry stop or **Reenable**. Confirm failed and cancelled production scans do
not advance `LastExecutedAt`.
