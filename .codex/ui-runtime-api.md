# Deploy and runtime-status API contract

The snapshot endpoints let the browser deploy a saved flow and refresh runtime
state without adding execution data to the persisted graph DTO. During the
portable-IL migration, deployment to the built-in target means compile, prepare,
and transactionally start the server VM. A successful status response must not
be synthesized merely from the saved graph.

## Endpoints

- `POST /api/flows/{flowId}/deploy` resolves and compiles the latest saved
  definition, prepares the selected target runtime, and atomically replaces the
  prior deployment. A successful response is `200` with a runtime snapshot.
  Compile, validation, or startup failures use a non-2xx status and leave a
  previous runtime unchanged.
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
