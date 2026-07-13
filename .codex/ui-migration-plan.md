# Vue UI migration plan

This is the restartable plan for migrating the remaining designer behaviour from
`../HtmlSvg` into `frontend/flow-control-ui`.

The old project is a behavioural and visual reference. It is not an architecture
template. New code must use the current UI project's Vue, Vue Router, Pinia,
TypeScript, lint, format, accessibility, and testing conventions.

## How to resume the migration

1. Read `.codex/instructions.md` and this entire file.
2. Check `git status` and preserve unrelated or incomplete user changes.
3. Inspect the current implementation and the relevant files in `../HtmlSvg`.
4. Start with the first unchecked slice whose prerequisites are complete.
5. Implement only that slice, including its tests and documentation updates.
6. Run every required verification command.
7. Mark a slice complete only when its exit criteria and verification pass.
8. Update the handoff log with what changed, what was verified, and the exact next
   unchecked slice.

If a slice is too large to verify independently, split it into smaller checklist
items before writing code. Do not migrate the entire legacy designer at once.

## Mandatory quality rules

These requirements apply to every migration slice, not only the final phase.

- Run the UI formatter and commit only formatter-compliant source.
- Run all configured linters and fix errors rather than disabling rules without a
  documented reason.
- Add or update unit tests for domain logic, stores, composables, utilities, and
  component behaviour introduced or changed by the slice.
- Add or update Playwright e2e tests for every user-visible route or interaction
  introduced or changed by the slice.
- Update existing tests when routes, labels, first-screen content, or behaviour
  change. Tests must describe current behaviour rather than preserve stale output.
- Keep graph data serialisable. Do not place DOM nodes, Vue component instances,
  event objects, or class-heavy view state in Pinia.
- Prefer typed props and emits, pure geometry functions, focused composables, and
  feature-local components over a global event emitter or singleton controller.
- Include keyboard and accessible-name behaviour when adding pointer interactions.
- Do not mark a checklist item complete while required tests are skipped or failing.

Run these commands from `frontend/flow-control-ui` before completing a slice:

```sh
npm run format
npm run lint
npm run test:unit -- --run
npm run test:e2e
npm run build
```

`npm run build` includes the configured TypeScript type-check. If an e2e test needs
a backend endpoint that does not exist yet, use a deterministic Playwright route
fixture and record replacement with the real API in the relevant later slice.

## Architecture decisions

- Organise code by feature instead of copying the old project-wide `model`, `view`,
  `controller`, and `impl` folders.
- Use lazy route components and named routes. Views receive route parameters as
  typed props so they can be rendered and tested without reading the router directly.
- Use Pinia setup stores containing plain serialisable state and explicit actions.
- Keep persisted graph data separate from SVG geometry, transient selection,
  pointer state, and runtime status.
- Put designer-wide interaction orchestration in focused composables rather than a
  module-level singleton or `mitt` event bus.
- Keep SVG components mostly presentational. Geometry and hit-testing belong in
  pure TypeScript functions that can be unit tested without mounting Vue.
- Represent node kinds, connector capabilities, icons, and editors with typed
  registries so adding a node kind does not require scattered switch statements.
- Treat API payloads as untrusted at the frontend boundary and validate them before
  adding them to application state.

## Legacy behaviour inventory

Use this as a coverage map when checking whether the migration is complete.

| Legacy area | Behaviour to preserve or deliberately replace | Target approach |
| --- | --- | --- |
| `src/App.vue` | Starts the designer with sample data | Route-driven views and API-loaded flows |
| `src/stores/flows.ts` | Holds nodes and connections | Setup-style Pinia store with serialisable graph state |
| `flow/model` | Graph, node, connector, and persistence types | Feature-local domain types and validated API schema |
| `flow/impl` | Mutable graph classes and hit testing | Plain data plus pure geometry and graph operations |
| `flow/controller/flowPersistence.ts` | JSON conversion | Explicit API mappers and validation with round-trip tests |
| `flow/controller/flowDesignController.ts` | Selection, drag, connect, keyboard, grid | Focused designer interaction composables |
| `flow/controller/zOrder.ts` | Node stacking commands | Pure immutable graph operations exposed through store actions |
| `flow/controller/flowEventEmitter.ts` | Cross-component mouse events | Typed props/emits and composable-owned interaction state |
| `components/flow/FlowDesigner.vue` | SVG canvas and event surface | `FlowDesignerCanvas` plus viewport/interaction composables |
| `components/flow/FlowDesignerToolbar.vue` | Z-order controls | Accessible command toolbar driven by selection capabilities |
| `flow/view/FlowNode*.vue` | Node, icon, label, marker, status | Small feature components backed by a node-kind registry |
| `flow/view/FlowConnector.vue` | Connector layout and events | Typed connector component and pure layout functions |
| `flow/view/FlowConnection.vue` | Spline rendering and selection | Pure path geometry plus a presentational SVG component |
| `composables/screenSize.ts` | Canvas sizing | `ResizeObserver`-based viewport composable where needed |
| `utils/cubic-spline.ts` | Connection path calculations | Tested geometry utilities with no Vue dependency |
| `utils/svg-generator.ts` | SVG generation helpers | Retain only behaviour needed by the new renderer |
| `public/icons` | Node-kind imagery | Audited, accessible assets referenced by the typed registry |
| SCSS assets | Grid, node, connector, status styling | Current CSS strategy and design tokens; no Sass dependency by default |

## Migration phases and slices

### Phase 0 — Modern application foundation

- [x] Add the application shell and global UI styling.
- [x] Add named, lazy flow-library and flow-designer routes.
- [x] Add a setup-style Pinia flow store and typed serialisable sample graph.
- [x] Add a read-only SVG canvas with sample nodes and connections.
- [x] Update starter unit tests for the new application shell and store.
- [x] Record the architecture decisions and restart procedure in `.codex`.

### Phase 1 — Test harness and graph contract

Complete this phase before adding mutable designer behaviour.

- [ ] Add Playwright configuration, deterministic web-server startup, and e2e test
  directories to the current UI project.
  - Unit coverage: configuration helpers if any logic is introduced.
  - E2e coverage: `/flows`, `/flows/climate-control`, route navigation, and unknown
    flow handling at desktop and mobile viewport sizes.
  - Exit criteria: `npm run test:e2e` runs locally without manual server startup.
- [ ] Define the persisted flow, node, connector, and connection DTOs shared at the
  frontend API boundary.
  - Compare every persisted field with `../HtmlSvg/src/flow/model/*PersistModel.ts`
    and document intentional schema differences.
  - Unit coverage: valid payload, missing node reference, duplicate IDs, invalid
    connector direction/type, and safe rejection of malformed JSON.
  - E2e coverage: a valid mocked payload renders and an invalid payload produces a
    useful error state.
- [ ] Add explicit mapping between API DTOs and editable frontend graph data.
  - Unit coverage: DTO-to-domain mapping and lossless domain-to-DTO round trip.
  - E2e coverage: opening then saving an unchanged mocked flow preserves its graph.

### Phase 2 — Read-only visual parity

- [ ] Add the typed node-kind registry for labels, icons, colours, default size,
  connector definitions, and configuration editor metadata.
  - Migrate only audited assets from `../HtmlSvg/public/icons`.
  - Unit coverage: every supported node kind has complete registry metadata.
  - E2e coverage: representative logic, timing, maths, and routing nodes render.
- [ ] Replace hard-coded node dimensions and letters with reusable node, icon,
  label, marker, and status components.
  - Unit coverage: optional markers/status and accessible node names.
  - E2e coverage: deployed/runtime states are visually distinguishable by text or
    accessible name, not colour alone.
- [ ] Define connector types, capabilities, and pure connector layout functions.
  - Unit coverage: input/output placement on all supported sides and multiple
    connectors on one side.
  - E2e coverage: connector labels and compatible type information are available.
- [ ] Extract connection spline/path geometry into pure TypeScript utilities and a
  presentational connection component.
  - Unit coverage: forward, reverse, vertical, and missing-endpoint paths.
  - E2e coverage: all persisted connections render after route navigation/reload.
- [ ] Add a `ResizeObserver`-based designer viewport with usable desktop and mobile
  overflow/zoom behaviour.
  - Unit coverage: viewport calculations and coordinate conversion.
  - E2e coverage: graph remains reachable at desktop and mobile viewport sizes.

### Phase 3 — Selection and node manipulation

- [ ] Add canvas and node selection state through a focused composable.
  - Support pointer selection, keyboard focus, Escape to clear, and clicking blank
    canvas space to clear.
  - Unit coverage: selection transitions and deletion eligibility.
  - E2e coverage: select and clear a node with mouse and keyboard.
- [ ] Add SVG/client coordinate conversion and node dragging.
  - Keep pointer events transient and update only serialisable positions in Pinia.
  - Unit coverage: coordinate conversion, drag delta, clamping, and cancellation.
  - E2e coverage: drag a node and verify its new persisted position.
- [ ] Add configurable grid snapping.
  - Unit coverage: positive, negative, disabled, and boundary snap cases.
  - E2e coverage: dragged nodes land on the visible grid when snapping is enabled.
- [ ] Add accessible z-order commands: front, forward, backward, and back.
  - Unit coverage: immutable ordering operations and boundary no-ops.
  - E2e coverage: toolbar enablement and each command's resulting render order.
- [ ] Add keyboard move and delete commands with safeguards for editable controls.
  - Unit coverage: command interpretation and graph mutation.
  - E2e coverage: keyboard move/delete plus focus restoration.

### Phase 4 — Connection editing

- [ ] Add compatible connector highlighting and connection-start state.
  - Unit coverage: direction/type compatibility matrix.
  - E2e coverage: starting a connection highlights only valid destinations.
- [ ] Add preview connection drawing during pointer and keyboard interaction.
  - Unit coverage: preview path and cancellation state.
  - E2e coverage: preview follows the pointer and Escape cancels it.
- [ ] Add connection completion with duplicate, self-link, and invalid-link guards.
  - Unit coverage: graph validation for all accepted and rejected cases.
  - E2e coverage: create a valid connection and reject invalid connections visibly.
- [ ] Add connection selection and deletion.
  - Unit coverage: selected connection state and graph removal.
  - E2e coverage: select/delete with pointer and keyboard.

### Phase 5 — Flow authoring tools

- [ ] Add an accessible node palette driven by the node-kind registry.
  - Unit coverage: filtering/grouping and default-node creation.
  - E2e coverage: search and add representative node kinds.
- [ ] Add node creation by command and optional drag/drop onto the canvas.
  - Unit coverage: ID generation, default connector creation, and placement.
  - E2e coverage: add, position, and select a new node.
- [ ] Add the node configuration panel with typed editors and validation messages.
  - Unit coverage: editor validation and domain update actions.
  - E2e coverage: edit a node label/configuration and retain it after save/reload.
- [ ] Add graph-level dirty state and navigation protection.
  - Unit coverage: dirty-state transitions and reset after successful save.
  - E2e coverage: warn on navigation with unsaved changes and allow explicit discard.
- [ ] Decide whether undo/redo is required; document the decision before adding it.
  - If required, unit-test command history boundaries and e2e-test user undo/redo.

### Phase 6 — Flow API and persistence

- [ ] Define a typed frontend API client with abort handling and consistent errors.
  - Unit coverage: success, validation failure, network failure, and cancellation.
  - E2e coverage: loading, empty, error, and retry states through mocked routes.
- [ ] Replace flow-library sample data with API list/create/rename/delete operations.
  - Unit coverage: store actions and optimistic/confirmed state decisions.
  - E2e coverage: create, open, rename, and delete a flow.
- [ ] Load individual graph definitions by route ID with stale-request protection.
  - Unit coverage: route race and stale response handling.
  - E2e coverage: direct URL, reload, unknown ID, and rapid route navigation.
- [ ] Add save behaviour with validation, pending state, errors, and retry.
  - Unit coverage: payload mapping and state transitions.
  - E2e coverage: edit/save/reload and failed-save recovery.
- [ ] Remove frontend sample data after API-backed flows and tests are stable.
  - Exit criteria: no production import references `sampleFlows.ts`.

### Phase 7 — Deploy and runtime status

- [ ] Agree the deploy and runtime-status API contract with the Go backend.
- [ ] Add deploy confirmation, pending, success, and failure states.
  - Unit coverage: deployment store/action transitions.
  - E2e coverage: successful and failed deployment using deterministic API fixtures.
- [ ] Display per-flow runtime state without mixing it into persisted graph data.
  - Unit coverage: runtime update reconciliation and disconnect behaviour.
  - E2e coverage: running/stopped/error state updates are announced accessibly.
- [ ] Display node-level runtime status and values where supported.
  - Unit coverage: status mapping and stale value handling.
  - E2e coverage: representative node status updates without editing the graph.

### Phase 8 — Hardening and legacy retirement

- [ ] Complete keyboard-only and screen-reader-oriented accessibility review.
- [ ] Add stable e2e coverage for critical flow create/edit/save/deploy journeys.
- [ ] Add visual regression snapshots only for stable designer states where they
  provide value beyond behavioural assertions.
- [ ] Test large graphs and remove avoidable reactive/rendering bottlenecks.
- [ ] Verify supported browsers, responsive layouts, Docker base paths, and direct
  route reloads.
- [ ] Compare the finished UI against every row in the legacy behaviour inventory.
- [ ] Document deliberately dropped legacy behaviour and why it is no longer needed.
- [ ] Remove obsolete migration fixtures, compatibility code, and unused assets.
- [ ] Update project documentation and mark this migration plan complete.

## Per-slice definition of done

A slice is complete only when all applicable items below are true:

- [ ] Behaviour and acceptance criteria are implemented.
- [ ] New and changed TypeScript/Vue code is formatter-compliant.
- [ ] Lint passes without new suppressions unless the reason is documented.
- [ ] Relevant unit tests were created or updated and all unit tests pass.
- [ ] Relevant Playwright e2e tests were created or updated and all e2e tests pass.
- [ ] Type-check and production build pass.
- [ ] Keyboard and accessibility behaviour was considered and tested where relevant.
- [ ] Existing tests and docs reflect the current UI.
- [ ] The phase checklist and handoff log below were updated.

Do not permanently check this reusable definition-of-done list. Use it as the review
template for each slice and record the result in the handoff log.

## Handoff log

Add the newest entry first. Keep entries concise and include exact commands/results.

### 2026-07-13 — Foundation slice

- Completed Phase 0: application shell, named lazy routes, setup-style flow store,
  typed sample graph, read-only canvas, and updated unit tests.
- Verified at completion: `npm run format`, `npm run lint`,
  `npm run test:unit -- --run` (3 tests), and `npm run build`.
- Browser-rendered `/flows/climate-control` was inspected at desktop size.
- Known gap: Playwright infrastructure and committed e2e tests do not exist yet, so
  `npm run test:e2e` was not part of the foundation verification.
- Resume with: **Phase 1 — add Playwright configuration and route smoke tests**.
