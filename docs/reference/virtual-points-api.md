# Virtual-points API

The API separates portable execution contexts, concrete execution instances,
deployments, declarations, and runtime values. All routes except
`GET /api/health` require an API key in `X-Api-Key`.

## Execution contexts

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/execution-contexts` | List contexts |
| `GET` | `/api/execution-contexts/{id}` | Read a context |
| `POST` | `/api/execution-contexts` | Create a context |
| `PUT` | `/api/execution-contexts/{id}` | Replace a context |
| `DELETE` | `/api/execution-contexts/{id}` | Delete a context |

Context reads require `contexts.view`; mutations require `contexts.edit`.

## Execution instances and runtime state

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/execution-instances` | List instances |
| `GET` | `/api/execution-instances/{id}` | Read an instance |
| `POST` | `/api/execution-instances` | Create an instance |
| `PUT` | `/api/execution-instances/{id}` | Replace an instance |
| `DELETE` | `/api/execution-instances/{id}` | Delete an instance |
| `GET` | `/api/execution-instances/{id}/virtual-points` | List allocations |
| `GET` | `/api/execution-instances/{id}/virtual-points/runtime` | List runtime values |
| `GET` | `/api/execution-instances/{id}/virtual-points/{pointKey}/runtime` | Read one runtime value |

Instance and allocation reads require `contexts.view`. Runtime-value reads
require `points.view`. Instance mutations require `contexts.edit`.

A runtime value reports its typed value, quality, timestamp, writer, readers,
version, and retained status. A point with no runtime allocation returns
`not_found` rather than an invented value.

## Point resolution

```text
GET /api/point-resolution/{pointKey}
    ?executionContextId={contextId}
    &executionInstanceId={instanceId}
```

The qualifiers are optional. When supplied, they must identify existing
resources and resolution is restricted to them. The response distinguishes an
unknown point from unavailable validation and contains the resolved type,
implementation, units, capabilities, and revision. This route requires
`contexts.view`.

## Context deployments

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/execution-contexts/{contextId}/deployments` | List deployments |
| `POST` | `/api/execution-contexts/{contextId}/deployments` | Create and prepare a deployment |
| `PUT` | `/api/execution-contexts/{contextId}/deployments/{id}` | Replace and prepare a deployment |
| `DELETE` | `/api/execution-contexts/{contextId}/deployments/{id}` | Remove a deployment |

Reads require `contexts.view`; mutations require `deployments.manage`.
Deployment validation rejects stale revisions, missing physical bindings,
unsupported capabilities, incompatible contracts, disabled instances, resource
limits, and writer conflicts before activation.

## Retained-state operations

| Method | Route | Purpose |
| --- | --- | --- |
| `GET` | `/api/execution-instances/{id}/virtual-points/retained-backup` | Export retained values |
| `PUT` | `/api/execution-instances/{id}/virtual-points/retained-backup` | Restore retained values |
| `DELETE` | `/api/execution-instances/{id}/virtual-points/retained` | Clear retained values |

Export requires `points.view`. Restore and clear require
`points.manage-retained`. A backup has schema version `1`, the exact execution
instance ID, and keyed retained values. Restore rejects a mismatched schema,
instance, type, units, persistence, default, or contract.

## Audit records

`GET /api/audit-records` returns actor-qualified mutation audit records and
requires `system.view`. Records include actor, method, path, result status, and
timestamp.

## Errors

Definition and deployment failures use a stable JSON shape:

```json
{
  "message": "Human-readable explanation",
  "code": "stable_machine_code",
  "details": {}
}
```

Authentication failures use `unauthenticated`; authorization failures use
`forbidden` and report the required permission. Domain errors include details
such as the point key, execution instance, existing writer, configured limit,
or stale revision when applicable.

