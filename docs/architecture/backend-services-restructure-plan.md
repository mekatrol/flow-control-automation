# Backend services restructure plan

## Purpose

Restructure `backend/Server/Server.Services` around functional domains instead
of placing every concrete class in one `Implementation` folder and every
shared shape in one `Contracts` folder.

This is a structural refactor. It must preserve behavior, persistence formats,
HTTP contracts, dependency-injection lifetimes, and runtime semantics. It is a
prerequisite for the
[unified flow execution context plan](unified-flow-execution-context-plan.md),
so the new execution-context implementation starts within durable domain
boundaries rather than adding more classes to the existing flat structure.

## Goals

- Organize internal services by cohesive functional areas.
- Keep concrete implementations internal to `Server.Services`.
- Expose cross-project behavior only through interfaces in
  `Server.Common.Contracts`.
- Place cross-project data models in `Server.Common.Models`.
- Place cross-project enums and other closed value vocabularies in
  `Server.Common.Types`.
- Prevent `Server.Common` from depending on `Server.Services`,
  `Server.Data`, or `Server.Compiler`.
- Make dependencies between execution, communication, point, configuration,
  and validation code explicit.
- Keep API transport DTOs in `Server.Api.Contracts`; do not confuse HTTP
  request/response shapes with service contracts.
- Make future files have an obvious functional home.

## Non-goals

- No behavior or HTTP endpoint changes in the folder-move phases.
- No database schema or persisted JSON/YAML changes.
- No replacement of the Flow VM, compiler, communications protocols, or stores.
- No large namespace-only compatibility layer.
- No speculative project-per-folder split. Separate assemblies can follow only
  where dependency evidence justifies them.
- No simultaneous unified-execution implementation during mechanical moves.

## Current problems

`Server.Services/Implementation` currently combines:

- VM runtime, deployment, debugger, simulator, and emulator code;
- FCP controller clients, debug transport, TCP, TLS, serial, and frame transport;
- DNS and HTTP/MQTT connectivity diagnostics;
- point definitions, sources, reads, runtime values, retained values, and
  compatibility rules;
- configuration parsing, persistence, credentials, and startup validation;
- audit infrastructure.

The flat namespace hides coupling and makes unrelated code appear equally
related. Several implementation types are public solely because tests or other
projects construct them directly. Meanwhile, interfaces and public models are
mostly under `Server.Services`, forcing consumers to depend on the
implementation assembly.

The existing `Server.Common` project already has the intended
`Contracts`, `Models`, and `Types` directories. The restructure should
complete that boundary rather than introduce a similarly named
`Service.Common` project.

## Dependency rules

```text
Server.Common
  ↑
Server.Compiler      Server.Data
         ↑             ↑
          Server.Services
                 ↑
              Server.Api
```

Rules:

1. `Server.Common` contains dependency-light cross-project contracts.
2. `Server.Common.Contracts` contains public interfaces, not implementation
   classes or HTTP DTOs.
3. `Server.Common.Models` contains public records/classes passed across
   project boundaries.
4. `Server.Common.Types` contains public enums and small closed value types.
5. `Server.Services` implements contracts and may reference Common, Compiler,
   and Data.
6. `Server.Api.Contracts` owns HTTP-specific requests and responses.
7. Internal helpers, policies, registries, adapters, options used only by
   services stay in their functional `Server.Services` namespace.
8. A Common contract must not expose a Data entity, compiler implementation
   type, ASP.NET type, database context, socket, stream, or concrete service.
9. If moving an interface to Common would introduce a forbidden dependency,
   first extract a Common model or narrow the interface. Do not add a reverse
   project reference.
10. Every concrete implementation in `Server.Services` is `internal`.
    There are no public implementation exceptions.
11. The only supported exposure of service behavior is a public interface in
    `Server.Common.Contracts`, registered by the public
    `AddServerServices` dependency-injection extension.
12. Production consumers and unit tests both call the same DI extension and
    resolve the Common interface. Tests must not construct, reference, expose,
    or obtain internals through `InternalsVisibleTo`.
13. Pure reusable code that genuinely must be public is not an implementation:
    move it behind a Common contract or into an appropriate dependency-free
    Common service/value abstraction.

## Proposed Server.Services layout

```text
Server.Services/
  Audit/
  Communication/
    Controllers/
    Fcp/
    Network/
    Protocols/
      Http/
      Mqtt/
    Serial/
  Configuration/
    ControllerTemplates/
    Credentials/
    Execution/
    Serialization/
  FlowExecution/
    Deployment/
    Debugging/
    Emulation/
    Runtime/
    Simulation/
    VirtualMachine/
  Points/
    Definitions/
    Runtime/
    Sources/
    Validation/
  Validation/
    Startup/
  DependencyInjection/
```

Folders and namespaces must match. For example:

```csharp
namespace Server.Services.FlowExecution.Debugging;
namespace Server.Services.Communication.Network;
namespace Server.Services.Points.Validation;
```

Avoid retaining an `Implementation` segment. Membership in
`Server.Services` already indicates implementation.

## File allocation

### FlowExecution/Runtime

- `FlowRuntimeService.cs`
- `ServerFlowPointAdapter.cs`

### FlowExecution/Deployment

- `FlowDeploymentService.cs`

### FlowExecution/Debugging

- `FlowDebugService.cs`
- `FlowDebugSessionRegistry.cs`
- `LocalFlowDebugSession.cs`
- `DebugSnapshotDecoder.cs`

### FlowExecution/Simulation

- `FlowSimulatorService.cs`
- `FlowSimulatorSessionRegistry.cs`

These are temporary homes. The unified execution-context work will replace the
separate debugging/simulation service and registry boundaries after this
restructure is complete.

### FlowExecution/Emulation

- `FlowEmulatorService.cs`

### FlowExecution/VirtualMachine

- `ManagedFlowVirtualMachine.cs`
- `ManagedFlowVirtualMachineFactory.cs`

### Communication/Controllers

- `FcpControllerDebugTransport.cs`

The transport may move under `FlowExecution/Debugging/Transports` if it
contains execution policy. Keep wire framing and controller communication under
`Communication`; keep debug orchestration under `FlowExecution`.

### Communication/Fcp

- `FcpAuthenticatedClient.cs`
- `UnavailableFcpClient.cs`

### Communication/Network

- `DnsLookup.cs`
- `TcpConnectionFactory.cs`
- `TlsHandshake.cs`

### Communication/Serial

- `SerialRs485FrameTransport.cs`

### Communication/Protocols/Http

- `HttpProtocolCheck.cs`

### Communication/Protocols/Mqtt

- `MqttProtocolCheck.cs`

### Communication connectivity orchestration

- `ConnectivityClock.cs`
- `ConnectivityPolicy.cs`
- `ConnectivityRateLimiter.cs`
- `ConnectivityService.cs`

Place these directly under `Communication/Connectivity` if that cluster gains
additional code. Protocol checks depend inward on network abstractions; network
utilities must not depend on connectivity orchestration.

### Points/Definitions

- `PointDefinitionDatabaseStore.cs`

### Points/Sources

- `PointSourceDatabaseService.cs`

### Points/Runtime

- `PointReadService.cs`
- `VirtualPointRuntimeStore.cs`
- `VirtualPointRetainedDatabaseStore.cs`

### Points/Validation

- `PointCompatibility.cs`
- `PointDefinitionValidator.cs`
- `PointSourceValidator.cs`

This area owns point quality, compatibility, safety policy, and definition
validation. Generic application startup validation does not belong here.

### Configuration/ControllerTemplates

- `ControllerTemplateFileStore.cs`
- `ControllerTemplateValidator.cs`

### Configuration/Credentials

- `CredentialDatabaseService.cs`

### Configuration/Execution

- `ExecutionConfigurationService.cs`

### Configuration/Flows

- `FlowDatabaseService.cs`

Although flows are executed under `FlowExecution`, stored authoring
definitions are configuration and should not depend on runtime services.

### Validation/Startup

- `StartupDataValidator.cs`

### Audit

- `AuditService.cs`

### DependencyInjection

- move `Extensions/ServiceCollectionExtensions.cs` to a domain-neutral
  composition root;
- split registration into internal extension methods such as
  `AddFlowExecutionServices`, `AddCommunicationServices`,
  `AddPointServices`, and `AddConfigurationServices`;
- retain one public `AddServerServices` entry point.

`AddServerServices` is the only public construction/composition boundary for
the services assembly. Domain registration methods and concrete types remain
internal. Registration tests must call this public extension exactly as
production does, then resolve and exercise `Server.Common.Contracts`
interfaces.

## Public contract classification

Every public item currently in `Server.Services` must be classified before
moving files.

### Move to Server.Common.Contracts

Move interfaces consumed by `Server.Api`, `Server.Compiler`,
`Server.Data`, future hosts, or other projects. Expected candidates include:

- `IFlowService`, `IFlowStore`, `IFlowRuntimeService`,
  `IFlowDeploymentService`;
- current debug/simulator/emulator interfaces, until replaced by
  `IFlowExecutionContextService`;
- `IFlowVirtualMachine` and its factory if hosts construct VMs through them;
- controller/FCP/frame transport interfaces used across assemblies;
- point definition, source, read, runtime, and retained-store interfaces;
- controller-template, credential, configuration, audit, and startup contracts
  used by API or composition code.

Do not move an interface merely because it is declared `public`. If it has no
cross-project consumer, make it `internal` and place it beside its functional
implementation.

### Move to Server.Common.Models

Move cross-project data-bearing records/classes, including the appropriate
members currently under `Server.Services/Contracts`:

- runtime snapshots, node snapshots, VM values, frames, commands, and inputs;
- execution/debug inspection, breakpoint, capability, and session data that
  survive the unified-contract design;
- emulator input/output and snapshot models;
- point runtime envelopes, definitions, mappings, limits, state labels,
  validation context/results, source connection details, and retained values;
- connectivity results, stages, HTTP previews, controller diagnostics, and
  connection descriptors;
- configuration documents and YAML validation results when they cross the
  service/API boundary;
- credential input and metadata;
- list options and paginated results where they are service-domain concepts.

Models should be grouped into matching subfolders inside Common, for example
`Models/FlowExecution`, `Models/Points`, `Models/Communication`, and
`Models/Configuration`. They retain the namespace
`Server.Common.Models` unless namespace subdivision provides material
ambiguity reduction.

### Move to Server.Common.Types

Move cross-project enums and small closed vocabularies:

- configuration kind;
- connectivity stage;
- point source kind and point persistence;
- VM error code if it is part of a public result;
- execution lifecycle/mode types introduced by the unified execution plan.

Do not put mutable models, exceptions, or interfaces in `Types`.

### Keep in Server.Api.Contracts

HTTP-only payloads remain under `Server.Api.Contracts`, including create,
advance, reset, fault, import, and error response DTOs. They map to/from Common
models at endpoint boundaries.

### Keep internal to Server.Services

- registries and registry entries;
- retry, timeout, rate-limit, and connectivity policies;
- database-backed concrete stores;
- protocol implementations and socket/stream helpers;
- session implementations;
- implementation-specific options not bound from public configuration;
- mapping and decoding helpers used only by service implementations.

### Exceptions

Exceptions should live beside the domain that throws them under
`Server.Services` when only API exception mapping consumes them indirectly.
Prefer stable error/result contracts in Common over making implementation
exceptions a public integration API. If an exception must cross assemblies,
place it under a deliberate `Server.Common.Errors` namespace rather than
`Models` or `Types`.

## Visibility cleanup

The current implementation folder contains public concrete types such as
`FlowDebugService`, `FlowSimulatorService`, `FlowEmulatorService`,
`FlowDebugSessionRegistry`, `FlowSimulatorSessionRegistry`,
`FcpAuthenticatedClient`, `FcpControllerDebugTransport`,
`SerialRs485FrameTransport`, `ManagedFlowVirtualMachineFactory`,
`PointCompatibility`, `DebugSnapshotDecoder`, and
`VirtualPointRuntimeStore`.

For each:

1. Find all cross-project construction and static calls.
2. Introduce or move the required interface/model to Common.
3. Register and resolve the concrete type through dependency injection.
4. Move reusable pure behavior to a deliberate Common service only when it is
   genuinely shared and dependency-free.
5. Change the concrete implementation to `internal`.
6. Register it behind its Common contract through the DI extensions.
7. Rewrite tests to call `AddServerServices` and resolve the Common contract.
8. Delete direct concrete construction and white-box access.

No API endpoint or unit test may name a concrete `Server.Services`
implementation. Remove `InternalsVisibleTo` for service implementations once
the converted tests no longer require it.

## Legacy compatibility removal inventory

This repository is pre-release and its contributor guidance rejects fallback
decoders, old aliases, dual writes, and compatibility shims unless a released
contract requires them. The restructure therefore removes—not relocates—the
following concrete legacy artifacts.

Before deleting an item, prove with repository-wide references and current
contract documentation that it exists only for an obsolete version. Current
rejection fixtures and security tests are not legacy merely because they
mention an unsupported version.

### Legacy flow fixture

Delete `testdata/contracts/flows/legacy.json`. It describes “Legacy climate,”
omits current flow metadata, contains pre-points node kinds, and is currently
orphaned from the backend and frontend fixture suites. Remove latent copy/output
entries found during implementation. Replace useful graph coverage with a
current-schema fixture named for the behavior it tests; do not modernize or
rename the legacy artifact.

### Missing-field and default compatibility

Remove flow mapper, serializer, validator, and store paths that accept old
documents by silently supplying:

- a missing flow schema version;
- a missing `controllerTemplateId` as `default`;
- obsolete node, connector, execution-mode, or point-source aliases;
- any field required by the current flow schema.

Search C# and TypeScript for default initializers, null coalescing, converters,
and tolerant JSON settings. Replace silent repair with strict current-schema
validation and stable diagnostics.

Update or remove affected assertions in
`Tests.Unit/Flows/FlowServiceTests.cs`,
`Tests.Unit/Flows/FlowCompilerBoundaryTests.cs`,
`Tests.Unit/Api/FlowEndpointTests.cs`, and frontend flow mapper/store contract
tests. Retain tests that reject unsupported schema versions.

### Debug snapshot compatibility projection

When the unified execution response is introduced, delete from
`FlowDebugService.cs`:

- `CompatibilitySnapshot`;
- `ToCompatibilitySnapshot`;
- compatibility-only calls around completed local scans;
- duplicate `DebugRuntimeSnapshot` mapping used only by the old session API.

Replace these with one canonical execution snapshot mapper shared by simulator,
server debugger, emulator debugger, and controller debugger.

Rename `testdata/contracts/debug-snapshot.schema.v1.json` to a current
execution-context schema fixture if it is referenced; delete it if orphaned.
Do not keep parallel debug and execution snapshot fixtures.

Update `FlowDebugServiceTests.cs`, `LocalFlowDebuggerTests.cs`,
`FcpControllerDebugTransportTests.cs`, and frontend execution/canvas snapshot
tests to verify one canonical shape in every mode.

### Separate debug and simulator session APIs

After the unified context cutover, delete:

- `IFlowDebugService.cs` and `IFlowSimulatorService.cs`;
- `StartFlowDebugSession.cs`, `FlowDebugSession.cs`, and
  `FlowSimulatorSession.cs`;
- `FlowDebugSessionNotFoundException.cs`, `FlowSimulatorOptions.cs`, and
  `FlowSimulatorException.cs`;
- `CreateDebugSessionRequest.cs` and `CreateSimulatorSessionRequest.cs`;
- `FlowDebugEndpointRouteBuilderExtensions.cs` and
  `FlowSimulatorEndpointRouteBuilderExtensions.cs`;
- old debug/simulator endpoint registrations;
- `FlowDebugSessionRegistry.cs` and `FlowSimulatorSessionRegistry.cs`;
- `FlowSimulatorService.cs`;
- mode-specific session mapping code in `FlowDebugService.cs`.

Replace them with the Common execution-context interface/models/types, one
internal service and registry, and unified API requests. Do not retain old
endpoints as adapters: no released client contract requires them.

Rewrite the corresponding tests:

- `FlowSimulatorSessionRegistryTests.cs` becomes unified registry
  lease/expiry/cleanup tests;
- `FlowSimulatorServiceTests.cs` becomes simulator-mode cases in shared
  execution-context service contract tests;
- `FlowDebugServiceTests.cs` becomes debugger-mode cases in that same suite;
- `LocalFlowDebuggerTests.cs` retains VM behavior coverage through the unified
  interface;
- endpoint tests and E2E mocks move from `debug-sessions` and
  `simulator-sessions` to `execution-contexts`.

### Obsolete debug-prefixed public models

After every host returns a unified context, replace and delete public models
whose names encode the old API boundary:

- `DebugRuntimeSnapshot`, `DebugNodeSnapshot`,
  `DebugProposedOutput`, and `DebugTypedValue`;
- `FlowDebugBreakpoint`, `FlowDebugCapabilities`, and
  `FlowDebugInspection`;
- controller debug envelope/load/live-output DTOs that duplicate unified
  context fields.

Use mode-neutral `FlowExecution*` names. Change backend, frontend, controller
transport, tests, and fixtures together. Delete old JSON properties rather than
supporting both names.

### YAML and normalized JSON artifacts

Review and regenerate these canonical pairs:

- `testdata/contracts/points/v1.yaml` and `v1.normalized.json`;
- `testdata/contracts/point-sources/v1.yaml` and `v1.normalized.json`;
- `testdata/contracts/controllers/default.v1.yaml` and
  `default.v1.normalized.json`;
- `testdata/contracts/controllers/constrained.v1.yaml` and
  `constrained.v1.normalized.json`;
- `Server.Api/data/controllers.json`.

Remove obsolete fields, aliases, default substitutions, and enum spellings.
Keep source YAML free of backend metadata and normalized JSON limited to current
canonical names. Remove stale mutable records from `controllers.json`, or
replace it with the minimal documented development seed. Regenerate expected
JSON through the current serializer and review the diff.

Do not delete negative fixtures for unsupported schema, YAML aliases/tags,
syntax, unknown fields, or invalid Flow IL. They verify strict current
validation. Update `ConfigurationFixtureTests.cs`,
`ControllerTemplateValidatorTests.cs`, and frontend
`configurationFixtures.spec.ts` so each retained fixture appears exactly once
and deleted artifacts are not copied to test output.

### Parser compatibility branches

Audit `ConfigurationYaml.cs`, controller/point/source validators, database
stores, and JSON options for alternate field names, case-insensitive values,
missing-version defaults, scalar coercion, ignored unknown fields, multiple
spellings, read-old/write-new normalization, and dual serialization properties.

Delete discovered compatibility branches and add a rejection test containing
the exact obsolete input. Preserve security behavior: alias/tag rejection,
depth/size limits, duplicate-key rejection, strict UTF-8, and unknown-field
rejection.

### Database migration history

`Server.Data/Migrations` currently contains one
`20260905134410_InitialCreate` migration, designer, and model snapshot. Keep
this current baseline. If pre-release work creates intermediate migrations,
squash them back to one reviewed `InitialCreate` following
`docs/development/database-migrations.md`. Database tests must create a fresh
current database; do not test upgrades from unsupported pre-release versions.

### Documentation and generated artifacts

- Remove current-document promises of legacy flow, session, field, or endpoint
  compatibility.
- Preserve archived plans only as historical records.
- Update API references to the current schema and unified endpoints.
- Exclude generated `bin`, `obj`, test-results, copied fixtures, and local
  runtime data from change sets.

## Unit-test restructuring requirements

Unit tests are part of every restructure phase, not follow-up work. Mirror the
production domains under `Tests.Unit`:

```text
Tests.Unit/
  Audit/
  Communication/{Connectivity,,Controllers,Network,Protocols,Serial}/
  Configuration/{ControllerTemplates,Credentials,Execution,Flows}/
  FlowExecution/{Deployment,Debugging,Emulation,Runtime,VirtualMachine}/
  Points/{Definitions,Runtime,Sources,Validation}/
  Architecture/
```

For every slice:

1. Move tests into the matching folder and namespace.
2. Configure services through the public `AddServerServices` extension,
   override dependencies only through contract registrations intended for test
   composition, and resolve the system under test through its
   `Server.Common.Contracts` interface.
3. Rename tests referring to old namespaces, sessions, contracts, or behavior.
4. Delete tests whose sole purpose is accepting a prior unsupported format.
5. Add strict rejection tests for every removed alias or format.
6. Preserve behavioral assertions during mechanical moves.
7. Add architecture tests for references, public concrete types,
   folder/namespace parity, and forbidden `Implementation` folders.
8. Ensure fixture-copy configuration includes only current artifacts.
9. Do not use direct concrete construction, reflection, implementation
   namespaces, or `InternalsVisibleTo` to test service implementations.
10. Where a behavior cannot be tested through its contract, improve the
    contract or extract a separately injectable Common abstraction instead of
    exposing the implementation.

Unified execution tests use one parameterized contract suite that runs identical
run/pause/stop/restart/step/breakpoint/run-to assertions against simulator and
debugger contexts. Mode-specific suites contain only capability extensions such
as virtual time, fault injection, and live physical output.

## Migration phases

### Phase 0 — Read-only baseline gate

- Run the complete backend test suite and capture the baseline.
- Generate a namespace/project dependency report.
- Inventory every public symbol in `Server.Services` and its consumers.
- Confirm no pending namespace-sensitive serialization behavior.
- Make no production moves or namespace changes in this gate.

### Phase 1 — Legacy cleanup

This is the first implementation phase and must complete before any folder,
namespace, contract-location, or service restructure begins.

- Execute every applicable item in the legacy compatibility removal inventory.
- Delete the orphaned legacy flow and snapshot artifacts.
- Remove missing-field defaults, aliases, compatibility projections, and other
  unsupported-format acceptance paths proven to be legacy.
- Regenerate current YAML/JSON fixtures and clean development seed data.
- Delete legacy-only tests and replace them with current-contract behavior or
  strict rejection tests.
- Update all affected unit tests before proceeding.
- Run the complete backend and frontend contract suites.
- Record any legacy item blocked by a genuinely released compatibility
  obligation; do not silently carry it into the restructure.

No phase below may start until Phase 1 is complete and green.

### Phase 2 — Common boundary preparation

- Create functional subfolders under `Server.Common.Contracts`,
  `Server.Common.Models`, and `Server.Common.Types`.
- Move dependency-free public interfaces, models, and enums.
- Update namespaces and consumers one functional slice at a time.
- Split HTTP DTOs from service-domain models where they are currently shared.
- Verify Common remains dependency-free.
- Add the public `AddServerServices` composition contract and migrate tests
  to resolve Common interfaces through it.

### Phase 3 — Points and configuration

- Move point implementations into Definitions, Sources, Runtime, and
  Validation.
- Move flow/configuration stores, controller templates, credentials, and
  execution configuration.
- Make every concrete store and validator internal.
- Update DI registrations and focused tests.

### Phase 4 — Communication

- Move DNS/TCP/TLS and RS485 helpers first.
- Move FCP clients/transports next.
- Move HTTP/MQTT protocol checks.
- Move connectivity orchestration last.
- Enforce dependency direction from orchestration to protocols to network
  primitives.
- Preserve timeout, cancellation, SSRF, TLS, and redaction behavior.
- Make every network, protocol, serial, client, and transport implementation
  internal and resolve it only through contracts and DI.

### Phase 5 — Existing flow execution

- Move VM, runtime, deployment, emulator, simulator, and debugger code into the
  proposed folders without redesigning behavior.
- Make every concrete execution service, VM adapter, and registry internal.
- Move shared interfaces/models to Common.
- Preserve current endpoints and tests.

### Phase 6 — Composition and cross-cutting code

- Split service registration by functional area.
- Move startup validation and audit code.
- Remove empty `Implementation`, `Contracts`, and obsolete
  `Extensions` folders.
- Remove stale global usings and namespace aliases.
- Ensure `AddServerServices` is the sole public DI entry point and every
  domain registration extension is internal.

### Phase 7 — Architecture enforcement

- Add tests or analyzers that reject:
  - any public concrete class under `Server.Services`;
  - Common references to Services, Data, Compiler, or Api;
  - API references to concrete service implementations;
  - test references to service implementation namespaces or types;
  - service tests that bypass `AddServerServices`;
  - `InternalsVisibleTo` exposure of service implementations;
  - new files under a generic `Implementation` folder;
  - service-domain models under `Server.Api.Contracts`;
  - cross-domain internal dependencies not on the approved graph.
- Document the placement rules in contributor guidance.

### Phase 8 — Unified execution context

Begin the unified execution-context plan only after phases 1–7 pass. Its new
service, registry, models, types, and interfaces must be created directly in:

- `Server.Services/FlowExecution` for internal implementations;
- `Server.Common.Contracts` for public interfaces;
- `Server.Common.Models/FlowExecution` for public data;
- `Server.Common.Types` for execution mode and lifecycle values;
- `Server.Api.Contracts` for HTTP-only requests and responses.

## Safe move strategy

Each pull request should move one cohesive slice:

1. Move Common contracts/models/types required by the slice.
2. Move implementation files and change namespaces.
3. Update DI and consumers.
4. Make every concrete implementation internal.
5. Update tests to use `AddServerServices` and resolve Common contracts
   without changing behavioral assertions.
6. Run formatting, analyzers, unit tests, and API tests.
7. Commit the slice independently.

Do not combine mechanical moves with logic rewrites. Git history remains useful
when file moves and behavioral changes are separate commits.

## Verification

For every phase:

- `dotnet build` succeeds with warnings treated according to repository policy;
- all `Tests.Unit` tests pass;
- API and browser integration tests covering the moved slice pass;
- DI validation resolves all registrations;
- no service lifetime changes occur accidentally;
- cancellation and disposal tests pass for resource-owning code;
- namespace and project-boundary architecture tests pass;
- persisted configuration fixtures remain byte/semantically compatible;
- public API JSON fixtures remain compatible during structural phases.

Communication moves additionally test DNS failures, TCP cancellation, TLS
validation, serial disposal, MQTT/HTTP timeouts, SSRF policy, and diagnostic
redaction. Flow-execution moves additionally test VM parity, scan-cycle
boundaries, session cleanup, controller transport disposal, and emulator
isolation.

## Completion criteria

- `Server.Services/Implementation` no longer exists.
- Internal service code is organized by the functional layout in this plan.
- No concrete implementation in `Server.Services` is public.
- `AddServerServices` is the sole public service composition entry point.
- Unit tests configure services through that extension and exercise only
  `Server.Common.Contracts`; they do not access implementation types.
- Other projects depend on service behavior through
  `Server.Common.Contracts`.
- Cross-project data is in `Server.Common.Models`.
- Cross-project enums/value vocabularies are in `Server.Common.Types`.
- HTTP-only data remains in `Server.Api.Contracts`.
- `Server.Common` has no reverse dependency on implementation projects.
- DI has one public composition entry point with domain-specific internal
  registration modules.
- Architecture tests enforce the boundaries.
- Existing behavior and wire/persistence compatibility tests pass.
- The unified execution-context plan can proceed without adding code to a flat
  implementation namespace.
