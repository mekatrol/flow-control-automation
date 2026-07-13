# Deploy and runtime-status API contract

Phase 7 uses snapshot endpoints so the browser can deploy a saved flow and refresh
runtime state without adding execution data to the persisted graph DTO.

## Endpoints

- `POST /api/flows/{flowId}/deploy` deploys the latest saved definition. A successful
  response is `200` with a runtime snapshot. Validation or startup failures use a
  non-2xx status and do not imply that a previous runtime stopped.
- `GET /api/flows/{flowId}/runtime` returns the current runtime snapshot. A missing
  flow uses `404`; a temporarily unavailable runtime service uses `503`.

Both endpoints return:

```json
{
  "flowId": "climate-control",
  "state": "running",
  "updatedAt": "2026-07-14T08:00:00+10:00",
  "nodes": {
    "temperature-average": {
      "state": "running",
      "value": "22.4 C",
      "updatedAt": "2026-07-14T08:00:00+10:00"
    }
  }
}
```

Flow state is `stopped`, `running`, or `error`. Node state additionally supports
`idle`. Node `value` is optional display text and must not be copied into node
configuration. Unknown node IDs are ignored by the renderer, allowing a runtime
snapshot and a newly edited draft to coexist safely.

The initial frontend transport refreshes the snapshot after loading the graph and
after deployment. A failed refresh marks the connection as disconnected and clears
node values, because retaining them would misleadingly present stale telemetry as
current. A future server-sent-event transport may publish the same snapshot shape.
