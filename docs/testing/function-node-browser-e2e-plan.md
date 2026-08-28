# Function-node browser E2E implementation plan

Status: In progress
Last updated: 28 August 2026

## Implementation progress

| Deliverable | Status | Notes |
| --- | --- | --- |
| Dedicated real-backend runner | Complete | `npm run test:e2e:function-nodes` builds `Server.Api` into an isolated temporary output and runs the suite in desktop Chromium, avoiding locks from Visual Studio or extension-managed servers. `--no-build` is available for local iteration. |
| Playwright extension launch | Complete | Direct and VS Code extension runs use the standard Playwright configuration, which starts the managed .NET backend, configures the Vite proxy, and seeds browser API access. The npm mocked-suite wrapper continues to own its lightweight server lifecycle. |
| Browser action helpers | Complete for numeric stateless nodes | Flow creation, palette addition, virtual-point creation, keyboard connector wiring, save, simulation start, keyboard input entry, Apply, and rendered analog output assertions are implemented. |
| First alphabetical function: Add | Complete | The browser constructs Analog Input + Analog Input -> Add -> Analog Output and verifies positive, fractional/negative, and zero vectors against the real backend. |
| Remaining functions and adapter smoke coverage | Not started | Continue with And, then the remaining registry in section 6. |

## 1. Goal

Add Playwright end-to-end coverage for every executable function node by driving the browser against the real ASP.NET Core backend and the server-owned simulator.

Each function test must exercise the same user-visible workflow:

1. Create a new flow and construct the graph in the designer.
2. Add and configure the virtual analog or digital points used by the graph.
3. Save the flow and start its simulation.
4. Change every virtual input point in the **Simulation points** panel and click **Apply**.
5. Assert the displayed virtual output value after each applied test vector.

The tests verify the complete authoring-to-execution path: designer interaction, persisted flow DTO, backend compilation, portable VM execution, simulator API, and browser rendering. Browser route mocking is forbidden in this suite.

## 2. Scope and boundaries

### In scope

- Chromium Playwright tests using the managed real-.NET backend path.
- Graph construction through user-facing designer controls.
- Virtual point declaration and point-node configuration through the UI.
- Simulation start, typed input changes, **Apply**, and visible output assertions.
- Boolean truth tables, numeric boundary/equivalence partitions, configuration variants, state transitions, quality behaviour, and virtual-time behaviour where applicable.
- A repeatable structure for adding one function block at a time in alphabetical order.

### Out of scope for this plan

- Mocked simulator or flow API responses.
- Direct API insertion of the flow under test as a shortcut around the designer.
- Physical controller I/O, deployed runtimes, or live output commissioning.
- Exhaustive backend/VM conformance already covered by unit and integration tests.
- Visual regression and cross-browser coverage during the initial rollout.

Analog Input, Analog Output, Digital Input, and Digital Output are the test harness adapters around a function under test. Add one real-backend smoke scenario for each adapter, but do not count those scenarios as semantic function-block tests.

## 3. Canonical test topology

Use the smallest graph that exposes the function entirely through virtual points:

```text
virtual input point node(s) -> function node -> virtual output point node
```

- Numeric connectors use Analog Input and Analog Output nodes with `analog` virtual-point declarations.
- Boolean connectors use Digital Input and Digital Output nodes with `digital` virtual-point declarations.
- Mixed nodes use the matching adapter for each connector.
- Source-only functions connect directly to an output adapter.
- Stateful functions retain one simulator session while a sequence of input vectors is applied.
- A fresh flow, unique flow name, and unique point keys are used per test so fully parallel workers cannot share state.

Every semantic assertion must read the output shown in the browser's **Simulation points** panel. API responses may be observed for synchronization and diagnostics, but they are not the test oracle.

## 4. Proposed test layout

```text
frontend/flow-control-ui/e2e/
  functionNodes/
    fixtures/
      functionNodeTest.ts
    helpers/
      designer.ts
      simulation.ts
      testIds.ts
    adapterSmoke.spec.ts
    add.spec.ts
    and.spec.ts
    ...one spec per function kind...
```

The helper boundary should express user actions, not manufacture persisted graphs. Expected initial helper capabilities are:

- create an isolated flow through the flow library;
- add a node from the searchable palette;
- configure a selected node, including creating/selecting a virtual point;
- connect named output and input connectors using the designer interaction;
- save and wait for the real backend response;
- open the simulator and start a session;
- set all virtual inputs for one vector, then click **Apply** exactly once;
- wait for the real apply-and-step response and read a named virtual output;
- advance virtual time through the browser when a timing scenario requires it;
- stop the simulator and delete the test flow when safe cleanup support is added.

Prefer accessible roles and labels already exposed by the UI. Add stable `data-testid` attributes only where connector geometry, duplicate labels, or transient simulator state cannot be selected reliably through an accessible contract.

## 5. Phased delivery

### Phase 0 - Baseline and execution contract

- Add a dedicated real-backend command for the function-node suite rather than expanding the current small `test:e2e:dotnet` compatibility allow-list without a clear boundary.
- Run only `desktop-chromium` initially to keep feedback time controlled.
- Ensure the backend build occurs before the runner starts `Server.Api.dll`.
- Give every test a worker-safe identifier derived from Playwright test metadata plus a random suffix.
- Capture Playwright trace on first retry and preserve backend stdout/stderr on failure.
- Document local and CI commands and required browser/.NET prerequisites.

Exit criteria: an empty or tagged function-node suite can start an isolated real backend and UI reliably in local and CI environments.

### Phase 1 - Real-backend vertical slice and adapter smoke tests

- Build one flow entirely through the browser: Digital Input -> Not -> Digital Output.
- Create the readable virtual input and commandable virtual output declarations through the node configuration UI.
- Save, start simulation, apply `Off`, assert `On`, apply `On`, and assert `Off`.
- Add focused smoke scenarios for analog/digital input and output adapters.
- Confirm from Playwright request inspection that no relevant flow, point, compile, or simulator request was fulfilled by a browser route handler.
- Record any missing accessible labels, simulator synchronization hooks, or UI operations as product testability changes and implement them before generalizing helpers.

Exit criteria: the vertical slice fails if designer persistence, backend compilation, VM semantics, simulator transport, or rendered output is broken.

### Phase 2 - Extract the reusable browser DSL

- Extract the proven Phase 1 interactions into typed helpers.
- Keep node-kind details and expected vectors in each spec; do not create a generic test generator that hides the user workflow.
- Add typed utilities for digital and analog input editing and output parsing.
- Synchronize on specific network responses and visible lifecycle state rather than fixed sleeps.
- Produce actionable failure messages containing function kind, vector, point key, expected value, and displayed value.
- Make cleanup best-effort and scoped to the exact flow created by the test. Never use collection-wide deletion.

Exit criteria: Phase 1 reads as a short scenario composed of clear user actions, and a deliberately wrong expected output produces a useful trace and assertion error.

### Phase 3 - Stateless logic functions, alphabetically

Add one spec at a time, following the rollout order in section 6. For Boolean functions, cover the complete truth table when practical. For numeric functions, cover representative normal, negative, zero, fractional, boundary, and configuration cases without duplicating VM unit-test exhaustiveness.

For every new spec:

- construct the graph in the designer;
- configure all required virtual points;
- start one simulation session;
- change every virtual input for every vector;
- click **Apply** once per vector;
- assert the visible virtual output;
- run the individual spec repeatedly before adding it to the main suite.

Exit criteria: all applicable stateless functions pass against a clean real backend with no request interception.

### Phase 4 - Stateful, edge, and timing functions

- Add ordered vector sequences for Memory, Pulse, and Rising Edge without restarting between steps.
- Add Delay, On Delay, and Timer scenarios using deterministic virtual-time controls; wall-clock sleeps are forbidden.
- Assert initial state, transition state, elapsed/not-elapsed state, reset behaviour, and a second activation to detect stale state.
- Verify that one **Apply** represents one atomic input update and scan; use the browser's time-advance operation separately when elapsed time is part of the contract.

Exit criteria: stateful scenarios are deterministic under retry and do not depend on machine speed.

### Phase 5 - Source, routing, quality, and special-profile functions

- Cover source-only blocks (Calendar, Digital Constant, Numeric Constant, Schedule) by configuring them in the designer and observing a virtual output.
- Cover mixed-type and routing nodes with input vectors that select every branch.
- Define a browser-reachable way to vary input quality before implementing Quality Good. If the current UI cannot inject quality, add that simulator control as a separately reviewed product/testability change rather than using a direct API shortcut.
- Confirm the intended executable semantics for canonical single-value profiles such as Calculator, Override, and Split in their specs.

Exit criteria: every registered executable function kind is either covered or has a documented, accepted blocker with an owning follow-up issue.

### Phase 6 - CI hardening and coverage guard

- Run the suite serially or with a conservative worker count until isolation is proven; then increase parallelism deliberately.
- Repeat the suite enough times to identify lifecycle and rendering flakes.
- Add a registry-to-spec guard that compares executable function kinds with the declared E2E coverage manifest. Adapter kinds are tracked separately.
- Keep retries as diagnostics, not as the definition of success; remove causes of repeated first-attempt failures.
- Split fast stateless and slower stateful/timing jobs if CI duration requires it while retaining one command for the full suite.
- Update `docs/testing/virtual-points.md` with the final commands and coverage boundary.

Exit criteria: CI detects a newly registered executable function without a corresponding E2E coverage entry, and the full suite is repeatable against an isolated backend.

## 6. Alphabetical rollout order

The function-under-test sequence is based on the current executable registry and excludes the four point adapter kinds:

1. Add
2. And
3. Average
4. Calculator
5. Calendar
6. Clamp
7. Comparator
8. Delay
9. Digital Constant
10. If
11. Level Shifter
12. Line
13. Max
14. Memory
15. Min
16. Nand
17. Nor
18. Not
19. Numeric Constant
20. On Delay
21. Or
22. Override
23. Pulse
24. Quality Good
25. Rising Edge
26. Schedule
27. Selector
28. Sequence
29. Split
30. Timer
31. Xnor
32. Xor

Do not skip ahead when a block exposes a harness limitation. Fix or explicitly document the shared limitation first so later tests do not encode a workaround that bypasses the intended browser workflow.

## 7. Per-block definition of done

A function block is complete only when:

- its graph is created through the designer UI against a real backend;
- every required analog/digital virtual input and output is configured through the UI;
- the saved graph reloads or successfully starts through the authoritative backend compile path;
- every test vector changes all virtual input controls and clicks **Apply**;
- the expected virtual output is asserted from the rendered simulator panel;
- configuration choices and state/time preconditions are explicit in the spec;
- the spec passes alone and as part of the accumulated alphabetical suite;
- no fixed delay, mocked endpoint, shared identifier, or direct graph-seeding shortcut is present; and
- the coverage manifest and relevant testing documentation are updated.

## 8. Risks and mitigations

| Risk | Mitigation |
| --- | --- |
| Fully parallel tests collide in the SQLite-backed API or one-session-per-flow limit | Generate unique flow IDs/names and point keys; never reuse a flow across tests. |
| SVG connector interactions are sensitive to layout | Prefer accessible connector buttons and established pointer-event helpers; add narrowly scoped stable selectors when necessary. |
| Output assertions race the apply-and-step request | Wait for the matching simulator response and then assert the named rendered output with Playwright auto-retry. |
| Timing blocks become flaky | Advance server virtual time through the UI; never wait for real elapsed time. |
| Test helpers hide the actual workflow | Keep helpers at user-action granularity and keep graph topology/vectors visible in each spec. |
| Suite duration grows with every block and browser project | Start with Chromium, reuse one session per spec, and split CI jobs only after measuring. |
| Quality Good cannot be exercised from the current panel | Treat quality injection as a required simulator UI capability and implement it before that alphabetical entry. |
| Constants and scheduled profiles do not have editable virtual inputs | Still configure the block in the designer, start simulation, click **Apply** when an input adapter exists; for source-only graphs, use the simulator's scan control and document the justified exception. |

## 9. Initial implementation checkpoint

The initial checkpoint is complete using **Add**, the first function in the required alphabetical series, as the real-backend vertical slice. The next reviewable change should add **And** using the proven helpers, extending them only for digital inputs and outputs as required.
