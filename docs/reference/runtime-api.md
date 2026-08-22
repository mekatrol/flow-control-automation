# Deploy and runtime-status API contract

The snapshot endpoints let the browser deploy a saved flow and refresh runtime
state without adding execution data to the persisted graph DTO. Deployment to
the built-in target compiles, prepares, and transactionally starts the server
VM. A successful status response must not
be synthesized merely from the saved graph.

## Endpoints

- `POST /api/flows/{flowId}/deploy` resolves and compiles the latest saved
  definition, prepares the selected target runtime, and atomically replaces the
  prior deployment. After runtime acceptance, the saved definition is copied to
  the flow's immutable deployed-version snapshot. A successful response is `200`
  with a runtime snapshot.
  Compile, validation, or startup failures use a non-2xx status and leave a
  previous runtime unchanged.
- `GET /api/flows/{flowId}/runtime` returns the current runtime snapshot. A missing
  flow uses `404`; a temporarily unavailable runtime service uses `503`.
- `GET /api/flows/{flowId}/deployed` returns the last successfully deployed graph.
  A flow that has never been deployed uses `409`.
- `POST /api/flows/{flowId}/revert-to-deployed` replaces the editable draft with
  the deployed graph and persists the result as a new revision.

The normal `GET` and `PUT /api/flows/{flowId}` routes operate on the editable
draft. Saving content that differs from the deployed snapshot sets its status to
`draft`; saving matching content or successfully deploying sets it to `deployed`.
`deployedRevision` remains populated while both versions exist.
Activating a context deployment records its selected flow revisions as deployed
through the same snapshot mechanism.

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
current. Streaming transports must preserve the same snapshot semantics.
