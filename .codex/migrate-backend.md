# Go to .NET Backend Migration Plan

## Goal

Replace the Go server in `backend/go` with the ASP.NET Core server in
`backend/Server/Server.Api` without changing the API contracts, validation
rules, or security behavior. Replace the Go JSON-file stores with the same
EF Core/SQLite persistence architecture used by
`/home/dad/repos/homecontrol/Mekatrol.Automatum/Mekatrol.Automatum.Data`.
Migrate the Go tests into `backend/Server/Tests.Unit`, using the existing
fixtures under `testdata/contracts` as the compatibility boundary.

The migration is complete only when the .NET server is the supported backend,
the frontend can use it without contract changes, and the Go server can be
removed.

## Persistence reference

Use
`/home/dad/repos/homecontrol/Mekatrol.Automatum/Mekatrol.Automatum.Data`
as the implementation reference for the persistence layer. In particular,
follow these reference files:

- `Mekatrol.Automatum.Data.csproj` for the EF Core and SQLite package pattern;
- `Context/IAutomatumDbContext.cs` for an injectable context interface;
- `Context/AutomatumDbContext.cs` for model configuration, migration startup,
  and SQLite row-version triggers;
- `Entities/BaseEntity.cs` for the common `Id`, `Key`, `Json`, `Created`,
  `Updated`, and `RowVersion` storage shape;
- `Entities/FlowEntity.cs` and `Entities/PointEntity.cs` for typed tables over
  the common entity shape;
- `Migrations/` for checked-in EF Core migrations.

Reproduce this architecture inside this solution; do not add a project
reference or runtime dependency on the project in the other repository. Adapt
names and entities to this server's flow, point-source, and credential domains,
and use the package versions compatible with this solution's `net10.0` target.

## Rules for every phase

- Keep every phase small enough to review independently.
- At the end of every phase, from `backend/Server`, run:

  ```sh
  dotnet build Server.slnx
  dotnet test Server.slnx
  dotnet format Server.slnx --verify-no-changes
  ```

- A phase is not complete until all three commands pass. Run `dotnet format
  Server.slnx` to fix formatting before using `--verify-no-changes`.
- Preserve the current HTTP routes, methods, status codes, content types,
  headers, error response shape, request-size limits, JSON/YAML property names,
  and omission behavior.
- Use nullable reference types and the repository `.editorconfig`; do not
  suppress analyzer or formatter findings without documenting why.
- Put service interfaces directly in `Server.Services`, organized into feature
  folders when useful.
- Put concrete service implementations in
  `Server.Services/Implementation`. Concrete implementations must not be
  public; use `internal sealed` by default. Use `protected` or
  `private protected` only where C# permits it for nested or inherited
  implementation details.
- Put public DI registration methods in `Server.Services/Extensions`. Expose
  capabilities through interfaces and public `IServiceCollection` extension
  methods so production and tests do not need access to concrete
  implementations.
- Register the EF Core context and database-backed services as scoped. Register
  process-local runtime state with the lifetime required to preserve current
  behavior (normally singleton). Do not retain a scoped context in a singleton.
- Pass the request `CancellationToken` from endpoints to every injected
  service call. Service interfaces must accept and propagate it through file,
  DNS, socket, TLS, HTTP, and credential operations where the .NET API supports
  cancellation.
- Use comments to explain non-obvious behavior and why it is required,
  especially compatibility, transactional persistence, concurrency, security,
  and cancellation decisions. Do not add comments that merely restate code.
- Do not use `InternalsVisibleTo` as the normal test seam. Tests should arrange
  the system through public extension methods and replace public interfaces in
  the service collection when a fake is needed.
- Port tests with the behavior they protect. Do not weaken assertions merely
  to make the .NET implementation pass.
- Do not add new product behavior during migration. Record desirable changes
  separately and preserve the Go behavior until cutover.

## Target structure

Use the existing projects and grow them along these boundaries:

```text
backend/Server/
  Server.Data/
    Context/                   context interface and EF Core DbContext
    Entities/                  base and domain persistence entities
    Extensions/                public data-layer registration
    Migrations/                checked-in EF Core migrations
  Server.Services/
    Contracts/                 shared domain and service DTOs
    Implementation/            non-public concrete implementations
    Extensions/                public service-layer registration
    IFlowService.cs            public service interfaces, grouped as needed
  Server.Api/
    Contracts/                 HTTP-only request/response contracts
    Endpoints/                 route-mapping extension classes
    Extensions/                API registration and endpoint extensions
    Program.cs                 composition and middleware only
  Tests.Unit/
    Contracts/
    Flows/
    PointSources/
    Credentials/
    Connectivity/
    Api/
```

`Server.Data` should mirror the reference project's separation of persistence
from API and service code. `Server.Services` owns reusable domain contracts,
validation, interfaces, and business behavior. `Server.Api` owns HTTP-specific
contracts and endpoint mapping. Database entities remain persistence-specific
and store the compatible domain JSON payload plus indexed metadata.

Keep project references one-directional:

```text
Server.Api -> Server.Services -> Server.Data
Tests.Unit -> Server.Api, Server.Services, Server.Data
```

Do not reference `Server.Api` from either class library and do not expose EF
entities or the EF context through service contracts.

Endpoint mapping may be organized by feature, but endpoint delegates must stay
thin: parse HTTP input, invoke an injected service with the request
`CancellationToken`, and map the result to the established HTTP contract.

## Phase 0 — Establish the migration harness

### Implementation

- Add the missing project references to enforce the documented dependency
  direction: `Server.Api` to `Server.Services`, `Server.Services` to
  `Server.Data`, and `Tests.Unit` to all three production projects.
- Remove `Tests.Unit/UnitTest1.cs`.
- Remove the template weather endpoint, DTO, and unused HTTPS redirect from
  `Program.cs`; retain OpenAPI only if it does not affect the production API.
- Add a public application registration extension, for example
  `AddFlowControlServer(this IServiceCollection, IConfiguration)`, and keep
  `Program.cs` limited to host construction, registration, middleware, route
  mapping, and `Run`.
- Add options for `SERVER_ADDRESS`, the SQLite connection string/database path,
  and credential encryption-key configuration.
- Add a public endpoint-mapping extension and implement
  `GET /api/health` returning JSON `{"status":"ok"}`.
- Establish a test host/factory that starts the real endpoint pipeline while
  allowing tests to override registered interfaces. Ensure test data is always
  written beneath a unique temporary directory.
- Add a solution-level smoke test for the health endpoint and DI registration.

### Exit criteria

- The application starts without the template endpoint.
- The health route matches the Go response and content type.
- Tests prove the public registration extension can be called and overridden
  without accessing concrete implementation types.
- Build, test, and format gates pass.

## Phase 1 — Complete the existing EF Core/SQLite data project

### Implementation

- Build on the existing `Server.Data` class library already included in
  `Server.slnx` and targeting `net10.0`; do not create another data project.
- Reference `Server.Data` from `Server.Services` and `Tests.Unit`. Keep
  `Server.Api` decoupled from EF Core and data entities.
- Add EF Core, EF Core Design, and SQLite packages using versions compatible
  with the solution target. Keep design-time dependencies private.
- Following the reference project, add:
  - a public context interface exposing the required `DbSet` properties,
    `Set<TEntity>()`, `SaveChangesAsync(CancellationToken)`, and
    `InitializeDatabase(CancellationToken)`;
  - a non-public or otherwise implementation-scoped EF Core `DbContext`;
  - a base entity containing `Id`, `Key`, `Json`, `Created`, `Updated`, and
    `RowVersion`;
  - flow, point-source, and credential entity types derived from the base;
  - unique indexes on `Key`, required keys and JSON payloads, and concurrency
    configuration for `RowVersion`.
- Add a public `IServiceCollection` extension in `Server.Data/Extensions` that
  registers the context as scoped with SQLite. Tests must be able to call the
  same extension with a temporary database connection string.
- Implement `InitializeDatabase` to run checked-in migrations with the supplied
  cancellation token and install idempotent SQLite update triggers that
  increment `RowVersion`, following `AutomatumDbContext.InitializeDatabase`.
- Create and check in the initial migration and model snapshot. Document the
  exact `dotnet ef migrations add` command, including data and startup project
  arguments.
- Initialize the database during application startup in an async DI scope
  before accepting requests. Propagate host shutdown cancellation.
- Add tests for schema creation, unique keys, required columns, migration
  startup, trigger idempotency, and row-version increments. Use a unique
  temporary SQLite file per test; do not use the EF in-memory provider.

### Exit criteria

- A clean database is created entirely from checked-in migrations.
- Calling initialization repeatedly neither fails nor duplicates triggers.
- Updating each entity type increments its row version and stale updates raise
  an EF concurrency exception.
- The data project is usable only through its public context interface and
  registration extension by normal consumers.
- Build, test, and format gates pass.

## Phase 2 — Port service contracts and fixture compatibility

### Implementation

- Add reusable DTOs for flows, nodes, connectors, connections, endpoints,
  runtime snapshots, point sources, credentials, pagination, and connectivity
  stages under `Server.Services/Contracts`.
- Keep the HTTP `{"message":"..."}` error envelope and any request/response
  types that exist only for transport under `Server.Api/Contracts`.
- Configure `System.Text.Json` explicitly so camel-case names, empty
  collections, numbers, timestamps, and omitted fields match Go output.
- Add strict YAML parsing/rendering support for the shared configuration
  contracts. It must reject duplicate keys, aliases, anchors, custom tags,
  unsupported schema versions, unknown fields, excessive nesting, multiple
  documents, and oversized input as the Go implementation does.
- Recreate the Go contract-fixture tests against:
  - valid and invalid point fixtures
  - valid and invalid point-source fixtures
  - valid and invalid controller fixtures
- Resolve fixture paths from a stable repository or test-output location, not
  from an assumed developer working directory.

### Exit criteria

- All valid YAML fixtures normalize to their corresponding JSON fixtures after
  backend metadata is removed.
- All invalid fixtures are rejected for the same reason category as Go.
- Build, test, and format gates pass.

## Phase 3 — Migrate flow persistence and validation

### Implementation

- Define public flow service/store interfaces in `Server.Services` with
  asynchronous methods that accept `CancellationToken`.
- Implement a non-public scoped flow database service over the injected context,
  following the reference project's generic JSON-backed database service
  pattern where useful. Serialize the complete validated flow into `Json` and
  keep queryable identity, key, timestamps, and row version in entity columns.
- Preserve:
  - readable, unique slug generation and numeric suffixes;
  - case-insensitive name filtering and deterministic name/ID sorting;
  - `draft` and `deployed` status filtering;
  - page sizes 10, 20, and 50 and current page-clamping behavior;
  - RFC 3339 UTC timestamps;
  - the complete browser node-kind catalogue;
  - finite coordinates and scalar-only configuration values;
  - unique node, connector, and connection IDs;
  - endpoint direction and data-type compatibility validation;
  - path/body ID matching and validation-before-replacement;
  - transaction rollback when validation or persistence fails;
  - optimistic concurrency using the entity row version.
- Register the flow services through a public
  `IServiceCollection` extension in `Server.Services/Extensions`.
- Port the store-level parts of the Go flow tests, replacing file-restart
  assertions with SQLite context/server restart assertions.

### Exit criteria

- Concurrent updates are transactional and stale writes cannot silently replace
  newer data.
- All flow validation and persistence tests pass.
- Build, test, and format gates pass.

## Phase 4 — Migrate flow HTTP and runtime behavior

### Implementation

- Map and test the existing routes:
  - `GET` and `POST /api/flows`
  - `GET`, `PUT`, and `DELETE /api/flows/{flowId}`
  - `POST /api/flows/{flowId}/deploy`
  - `POST /api/flows/{flowId}/disable`
  - `POST /api/flows/{flowId}/enable`
  - `GET /api/flows/{flowId}/runtime`
- Preserve strict JSON decoding, the 10 MiB request limit, exactly one JSON
  value per body, validation messages, 404 behavior, and redacted persistence
  failures.
- Implement runtime snapshots as process-local state that is never persisted
  into a flow. Preserve stopped/running node snapshots, disabled-flow behavior,
  deploy/disable/enable transitions, and runtime cleanup after deletion.
- Port all tests from `internal/flows/http_test.go` as endpoint tests using the
  real ASP.NET pipeline and temporary storage.

### Exit criteria

- Flow CRUD, query validation, pagination, deployment, runtime, disable/enable,
  and missing-flow behavior match Go.
- HTTP tests assert status, content type, headers where relevant, response
  shape, and empty 204 bodies.
- Build, test, and format gates pass.

## Phase 5 — Migrate point-source configuration and persistence

### Implementation

- Define public point-source interfaces in `Server.Services` whose methods
  accept `CancellationToken`; add non-public implementations under
  `Server.Services/Implementation` and a public DI registration extension
  under `Server.Services/Extensions`.
- Port source validation for `home_assistant`, `http_json`, and `mqtt`,
  including IDs, names, credential references, schemes, URLs, timeouts, MQTT
  QoS/topics, allowed HTTP methods, response limits, and private-network
  opt-in.
- Add a scoped point-source database service using the shared JSON-backed
  entity pattern. Preserve stable ordering, unique IDs/names, revision
  increments, stale-revision conflicts, and transactional rollback.
- Map and test:
  - `GET` and `POST /api/point-sources`
  - `GET`, `PUT`, and `DELETE /api/point-sources/{sourceId}`
- Preserve YAML request/response bodies, `application/yaml`, the 256 KiB limit,
  `ETag`, `If-Match`, delete `revision`, pagination/query behavior, 404/409
  mapping, and redacted storage errors.
- Port the source fixture, validation, store revision, rollback, restart, and
  HTTP behavior tests.

### Exit criteria

- YAML round-trips without exposing backend metadata and revisions behave
  exactly as the current API expects.
- Build, test, and format gates pass.

## Phase 6 — Migrate credential storage and API

### Implementation

- Define public credential-store and credential-resolver interfaces in
  `Server.Services` with cancellation-aware methods; register non-public
  implementations through a public extension.
- Preserve AES-GCM encrypted-at-rest secret payloads using a securely managed
  32-byte key. Store only ciphertext in the credential entity JSON, while keeping
  queryable metadata in the entity columns as needed. Preserve stable ordering,
  revisions, EF transactions, and rollback on failure.
- Preserve `mqtt` and `token` validation and resolution semantics. Secret
  values must never appear in list, get, create, update, logs, exceptions
  returned to clients, or test diagnostics.
- Block credential deletion while a point source references
  `secret://{credentialId}`.
- Map and test:
  - `GET` and `POST /api/credentials`
  - `GET`, `PUT`, and `DELETE /api/credentials/{credentialId}`
- Preserve the 64 KiB strict JSON body, one-value rule, status codes, conflict
  messages, and delete `revision` behavior.
- Port all tests from `internal/points/credentials_test.go` and add endpoint
  coverage for every credential route.

### Exit criteria

- Tests inspect persisted database rows to prove plaintext secrets are absent
  and API payloads contain metadata only.
- Referenced credentials and stale revisions produce the established conflict
  behavior.
- Build, test, and format gates pass.

## Phase 7 — Migrate connectivity testing

### Implementation

- Define injectable interfaces in `Server.Services` for credential resolution,
  DNS lookup, TCP connection creation, time, and outbound HTTP/MQTT protocol
  checks. Keep their concrete implementations non-public. This keeps tests
  deterministic without exposing implementation types.
- Pass the endpoint cancellation token through DNS, connect timeout, TLS
  handshake, credential resolution, HTTP requests, response reads, and MQTT
  operations. Return the existing cancellation diagnostic rather than leaking
  framework exceptions.
- Preserve:
  - ten tests per client key per minute;
  - SSRF protection for loopback, private, link-local, multicast, unspecified,
    and other forbidden destinations unless explicitly allowed;
  - DNS pinning and redirect destination revalidation;
  - maximum three redirects;
  - TLS 1.2 minimum and certificate verification;
  - bearer authentication for HTTP/Home Assistant;
  - response-size and request-timeout limits;
  - MQTT CONNECT/CONNACK, structured MQTT credentials, exact-topic SUBSCRIBE/
    SUBACK, and DISCONNECT behavior;
  - transient, redacted stage results that are never persisted.
- Map and test:
  - `POST /api/point-sources/test`
  - `POST /api/point-sources/{sourceId}/test`
- Port the private-address, explicit opt-in, rate-limit, MQTT authentication,
  wildcard-topic, and exact-subscription tests. Add cancellation and redirect
  policy tests where the Go behavior currently lacks direct coverage.

### Exit criteria

- Connectivity tests use fakes or loopback test infrastructure and require no
  external network.
- Security-policy, cancellation, credential-redaction, rate-limit, HTTP, and
  MQTT tests pass.
- Build, test, and format gates pass.

## Phase 8 — Full compatibility verification and cutover

### Implementation

- Run the Go and .NET contract suites against copies of the same non-secret
  fixture data and compare normalized responses for every route.
- Start the .NET server with the existing environment-variable configuration
  and run the frontend integration/end-to-end suite against it.
- Verify startup failure behavior for malformed stores, invalid key length,
  undecryptable credentials, duplicate IDs, and unwritable data locations.
- Verify graceful shutdown cancels outstanding requests and connectivity tests.
- Update repository scripts, documentation, development tasks, CI, containers,
  and frontend backend URL defaults to launch `Server.Api` instead of Go.
- Take a backup of real deployment data and document rollback to the last Go
  release before switching production startup.
- Perform a canary/smoke run covering health, flow list/save/deploy, source
  list/test, and credential metadata access. Do not use or log real secret
  values in verification output.

### Exit criteria

- The frontend and all automated suites pass against .NET.
- Deployment and rollback instructions are verified.
- Build, test, and format gates pass in CI from a clean checkout.

## Phase 9 — Remove the Go implementation

Only begin this phase after the .NET cutover has passed the Phase 8 exit
criteria and the agreed rollback window has elapsed.

### Implementation

- Remove `backend/go` and Go-only build, CI, and dependency configuration.
- Retain `testdata/contracts` as implementation-independent compatibility
  fixtures owned by the .NET tests.
- Remove temporary dual-run/comparison tooling while keeping permanent API,
  persistence, encryption-compatibility, and security regression tests.
- Search the repository for stale Go server commands, port assumptions, and
  documentation references, then update them.

### Exit criteria

- No supported workflow or documentation references the removed server.
- A clean checkout builds, tests, formats, and runs using only the .NET
  backend.
- The final `dotnet build`, `dotnet test`, and
  `dotnet format --verify-no-changes` gates pass.

## Migration completion checklist

- [ ] Every Go test has an equivalent .NET test or a documented reason it is
      covered by a stronger replacement.
- [ ] Every public route has success, validation, not-found, conflict, request
      limit, and cancellation coverage where applicable.
- [ ] JSON, YAML, headers, timestamps, errors, and 204 response bodies remain
      wire-compatible.
- [ ] SQLite schema changes are represented by checked-in EF Core migrations.
- [ ] Database startup initialization, row-version triggers, scoped context
      lifetime, and optimistic concurrency follow the referenced Automatum data
      project pattern.
- [ ] No secret or host filesystem detail can be returned or logged.
- [ ] All service calls propagate `CancellationToken`.
- [ ] All concrete implementations are non-public and registered through
      public `IServiceCollection` extensions.
- [ ] Project references remain one-directional from API to services to data;
      service contracts expose no API or EF Core types.
- [ ] `Program.cs` remains a composition root rather than containing business
      logic.
- [ ] CI enforces build, tests, and formatting.
- [ ] The frontend passes against .NET.
- [ ] Cutover and rollback have been exercised before the Go server is removed.
