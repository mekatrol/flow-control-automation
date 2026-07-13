# UI legacy retirement record

This record closes the migration from `../HtmlSvg`. The old project remains a
historical reference but is no longer required to build, test, or run the current UI.

## Inventory disposition

| Legacy area | Final disposition |
| --- | --- |
| `src/App.vue` | Replaced by named library/designer routes and API-loaded flows. No sample graph starts in production. |
| `src/stores/flows.ts` | Replaced by the serialisable setup-style Pinia flow store and a separate runtime store. |
| `flow/model` | Replaced by feature-local domain types and validated DTOs documented in `ui-flow-schema.md`. |
| `flow/impl` | Replaced by plain graph data and tested pure connection, z-order, keyboard, drag, and geometry functions. |
| `flow/controller/flowPersistence.ts` | Replaced by explicit DTO validation and mapper functions with round-trip tests. |
| `flow/controller/flowDesignController.ts` | Replaced by focused selection, connection, viewport, modal-focus, and dragging composables. |
| `flow/controller/zOrder.ts` | Replaced by immutable z-order operations exposed through Pinia actions. |
| `flow/controller/flowEventEmitter.ts` | Dropped. Typed component events and composable-owned transient state remove the global event bus. |
| `components/flow/FlowDesigner.vue` | Replaced by `FlowDesignerCanvas` and viewport/interaction composables. |
| `components/flow/FlowDesignerToolbar.vue` | Replaced by an accessible selection-aware toolbar. |
| `flow/view/FlowNode*.vue` | Replaced by small registry-backed node, icon, label, marker, and runtime-status components. |
| `flow/view/FlowConnector.vue` | Replaced by a typed keyboard-accessible connector and pure layout functions. |
| `flow/view/FlowConnection.vue` | Replaced by tested path geometry and a presentational selectable SVG component. |
| `composables/screenSize.ts` | Replaced by a `ResizeObserver` viewport with tested coordinate conversion, zoom, and overflow. |
| `utils/cubic-spline.ts` | Replaced by the tested `connectionPath` geometry utility. |
| `utils/svg-generator.ts` | Dropped. Vue renders the small required SVG shapes directly; a generator abstraction adds no behavior. |
| `public/icons` | Dropped after audit. The supported node registry uses accessible text symbols, so none of the legacy image files are required. |
| SCSS assets | Dropped. Component-scoped and global CSS provide the current grid, state, focus, node, and connector styling without Sass. |

## Deliberately dropped behavior

- Automatic startup with mutable sample data was replaced by explicit API loading,
  loading/error/empty states, and test-only fixtures.
- The singleton controller, mutable graph classes, and global event emitter were
  architectural coupling rather than user behavior and were not migrated.
- Legacy play/pause bitmap controls were not retained. Deployment and runtime state
  now use labelled buttons, confirmation, API state, and accessible status messages.
- Undo/redo was deferred because persistence baselines, explicit discard, and route
  protection cover the current authoring scope. The rationale is in
  `ui-undo-decision.md`.
- Visual regression snapshots were deliberately not added. Current designer states
  contain dynamic graph labels, positions, runtime values, and responsive overflow;
  behavioural and accessibility assertions provide a more stable failure signal.

## Cleanup and performance audit

Production code contains no imports from the test-only `sampleFlows.ts` fixture and
no compatibility layer or copied legacy asset. The fixture remains because it is
active deterministic unit/e2e test data, not a migration runtime dependency. The
generated Playwright result file was removed and its directory is ignored.

The hardening suite renders 120 nodes and 119 connections at desktop and mobile
sizes. Connection endpoint layout is resolved once per connection per graph update,
avoiding duplicate node and connector lookups from template bindings.
