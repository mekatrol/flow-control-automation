# UI flow persistence schema

The frontend API boundary is defined by `src/features/flows/api/flowDto.ts`. All
untrusted payloads must pass `parseFlowDto` before entering Pinia, and API writes
must use `flowDomainToDto` rather than serialising transient designer state.

## Differences from the legacy HtmlSvg persistence model

| Legacy field | Current schema decision |
| --- | --- |
| Numeric flow-element IDs | String IDs are used for stable URL/API identifiers and readable fixtures. |
| `flow.nodes` and `flow.connections` only | A flow also persists its ID, display metadata, deployment status, and update timestamp. |
| Node `type` enum | Renamed to the descriptive `kind` string union. |
| Node `cssClass` | Removed. Colours and other theme choices belong to the node-kind registry and are not persisted. |
| Node `zOrder` | Preserved as a number. |
| Connector `type` | Renamed to `dataType`; supported values are validated at the boundary. |
| Connector `direction` | Preserved as validated `input`/`output` values. |
| No connector label or side | Added so connector meaning and placement are persisted explicitly and are accessible. |
| No node label or configuration | Added as user-authored node data; configuration values are restricted to JSON scalar values initially. |
| Numeric connection endpoint IDs | Replaced by string `nodeId`/`connectorId` references and validated against the graph. |

The DTO intentionally does not contain colours, icons, selection, pointer/drag
state, SVG paths, DOM references, component instances, runtime status, or other
view state. A node's persisted `kind` resolves its current visual metadata through
the frontend node-kind registry.

## Frontend HTTP operations

The Phase 6 frontend expects these JSON operations. Browser tests provide
deterministic route fixtures until the Go server implements the matching API.

| Method and path | Request | Successful response |
| --- | --- | --- |
| `GET /api/flows` | None | Array of validated flow DTOs |
| `POST /api/flows` | `{ "name": string }` | Created flow DTO |
| `GET /api/flows/:flowId` | None | Flow DTO |
| `PUT /api/flows/:flowId` | Flow DTO | Server-confirmed flow DTO |
| `DELETE /api/flows/:flowId` | None | Empty successful response |
