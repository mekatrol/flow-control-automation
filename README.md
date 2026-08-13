# Flow Control Automation

Flow Control Automation is a home automation flow engine with a graphical editor for creating, deploying, and managing automation logic.

Every running flow follows a strict
[`PLC Scan Cycle`](docs/plc-scan-cycle.md): **Read Inputs** into a frozen
snapshot, **Execute Logic** against that snapshot and committed state, then
**Write Outputs** by atomically committing next state, proposed commands, and a
completed-scan snapshot. Intentional feedback uses explicit memory/delay state,
so cyclic behavior is predictable without recursive graph evaluation.

The production execution architecture is defined by the
[`portable Flow IL architecture`](docs/portable-flow-il-architecture.md).
Flows compile on the backend into deterministic bytecode and execute through the
same portable VM on the server or a hardware controller. Earlier design records
remain under [`docs/decisions/`](docs/decisions/), but pre-release runtime paths
support only the current format.
The normative production bytecode and native-host boundaries are defined by the
[`Flow IL v2 contract`](docs/flow-il-v2-contract.md),
[`VM host ABI`](docs/flow-vm-host-abi-v1.md), and
[`debugger contract`](docs/flow-il-v2-debug-contract.md).
The backend compiler is authoritative and emits only the current scheduled Flow
IL version. Non-current versions are rejected rather than migrated or executed.
The backend decompiler performs the reverse tooling operation: users
can import supported compiled IL as a valid editable designer flow. Artifacts
recover a deterministic semantically equivalent graph with stable executable
IDs and configuration. Current symbol metadata restores labels, groups, and
canvas layout losslessly; stripped metadata produces explicit recovery warnings.

## What the Application Does

The application allows users to build automation flows by connecting visual
nodes in a web interface. Each server deployment owns an isolated portable-VM
instance. It scans on its configured interval and can also perform one explicit,
non-overlapping scan through `POST /api/flows/{flowId}/runtime/scan`. Runtime
snapshots report the scan number, node values, proposed outputs, diagnostics,
and separate Read Inputs, Execute Logic, and Write Outputs timings.

Flows can exchange data and commands with Home Assistant or other home automation controllers through their supported protocols.

The backend provides the API used by the flow designer, stores flow definitions, and manages the lifecycle of each running flow. Individual flows can be started, updated, or stopped without interrupting other automations.

## Deployment

The application is intended to run in Docker as either:

- A Home Assistant add-on.
- A standalone service connected to the Home Assistant API or an external MQTT broker.

The frontend build supports reverse-proxy and Home Assistant ingress prefixes via
`VITE_BASE_PATH`. Deployment details and the required single-page application
fallback are documented in [`frontend/flow-control-ui/README.md`](frontend/flow-control-ui/README.md).

## Technology

- ASP.NET Core on .NET 10 for the API server, authoritative flow compiler, and
  host for the portable flow VM.
- Vue.js and SVG for the graphical flow designer.

## Project Structure

```text
flow-control-automation/
├── backend/
│   └── Server/
│       ├── Server.slnx  .NET solution
│       └── Server.Api/   ASP.NET Core API project
├── frontend/
│   └── flow-control-ui/ Vue application
├── controllers/
│   └── kincony/kc868-a16/ ESP-IDF firmware for the KC868-A16v3
└── .vscode/             Development tasks and extension recommendations
```

## Local Development

Install the .NET 10 SDK and a current Node.js/npm release. The Vue application
dependencies are installed from its project directory:

```sh
cd frontend/flow-control-ui
npm install
```

After the Vue setup has installed its dependencies, open this repository root in
one VS Code window. Run **Tasks: Run Task** from the Command Palette and choose
**dev**. VS Code starts these tasks in separate integrated terminal panes:

- `dotnet run` for `backend/Server/Server.Api/`
- `npm run dev` from `frontend/flow-control-ui/`

The ASP.NET Core API listens on `http://localhost:5008` under the development
launch profile, and Vite proxies `/api` to that address by default. The health
endpoint is:

```text
GET http://localhost:5008/api/health
```

Credential encryption requires a Base64-encoded 32-byte key. Generate one with
any of the following methods.

C#:

```csharp
using System;
using System.Security.Cryptography;

byte[] key = RandomNumberGenerator.GetBytes(32);
string base64Key = Convert.ToBase64String(key);

Console.WriteLine(base64Key);
```

OpenSSL:

```sh
openssl rand -base64 32
```

PowerShell:

```powershell
$key = New-Object byte[] 32
[System.Security.Cryptography.RandomNumberGenerator]::Fill($key)
[Convert]::ToBase64String($key)
```

For local development, save a generated key in the ignored local settings file
before first startup. For example, with OpenSSL:

```sh
key="$(openssl rand -base64 32)"
printf '{\n  "CredentialEncryptionKey": "%s"\n}\n' "$key" \
  > backend/Server/Server.Api/appsettings.Local.json
unset key
```

Deployment environments should provide the same setting through the
`CREDENTIAL_ENCRYPTION_KEY` environment variable and retain it in their secret
manager. Losing or rotating the key without re-encrypting stored credentials
makes those credentials unreadable.

### Backend tasks

The repository provides these VS Code tasks:

- **clean: backend** — runs `dotnet clean`.
- **check format: backend** — runs `dotnet format --verify-no-changes` against
  the formatting, code-style, and analyzer rules configured for the solution.
- **format: backend** — applies fixable formatting, code-style, and analyzer
  diagnostics configured by `backend/Server/.editorconfig`.
- **build: backend** — verifies formatting first, then runs `dotnet build`. It is
  the default VS Code build task.
- **dev: backend** — builds the backend, including the format check, then runs it
  with the `http` launch profile.

Run the equivalent commands from the repository root with:

```sh
dotnet clean backend/Server/Server.slnx
dotnet format backend/Server/Server.slnx --verify-no-changes
dotnet format backend/Server/Server.slnx
dotnet build backend/Server/Server.slnx
dotnet test backend/Server/Server.slnx
dotnet run --project backend/Server/Server.Api/Server.Api.csproj --launch-profile http
```

From `backend/Server`, run the backend unit tests with:

```sh
dotnet test Server.slnx
```

Run a specific test class by filtering its fully qualified name:

```sh
dotnet test Server.slnx --filter "FullyQualifiedName~DatabaseTests"
```

Backend tests create a unique temporary SQLite database for each test and
remove it when the test completes.

The backend installs StyleCop Analyzers for every solution project through
`backend/Server/Directory.Build.props`. The `.editorconfig` treats these rules
as errors:

- `SA1402`: each source file may contain only one top-level type.
- `SA1649`: the source filename must match its type name.

EF Core migration files are exempt from `SA1649` because their sortable
timestamp prefixes are intentional. Other StyleCop rules are disabled unless
they are explicitly enabled in `.editorconfig`.

`dotnet format` runs installed third-party analyzers, including StyleCop, as
part of its analyzer phase. To run or verify only analyzer fixes from
`backend/Server`, use:

```sh
dotnet format Server.slnx analyzers
dotnet format Server.slnx analyzers --verify-no-changes
```

Some analyzer diagnostics do not provide an automatic code fix. Therefore,
`dotnet build Server.slnx` remains the authoritative enforcement gate for
`SA1402` and `SA1649`; keep both the build and
`dotnet format Server.slnx --verify-no-changes` in CI.

When working from the command line, run format verification before the build;
the VS Code **build: backend** task enforces this automatically. You can also
run either **dev: backend** or **dev: frontend** on its own.

Run the complete frontend quality suite from `frontend/flow-control-ui` with:

```sh
npm run format
npm run lint
npm run test:unit -- --run
npm run test:e2e
npm run test:e2e:dotnet
npm run build
```

`test:e2e:dotnet` starts an isolated ASP.NET Core server and Vite proxy, then
smoke-tests health, flow save/deploy, credential metadata, and point-source
persistence against the real backend. It uses only temporary test data and a
test-only encryption key.

Production cutover, backup, canary, and rollback instructions are in
[`docs/backend-cutover.md`](docs/backend-cutover.md).

Playwright runs the end-to-end tests headlessly by default, so
`npm run test:e2e` does not open a browser window. To watch the tests run in a
browser, use:

```sh
npm run test:e2e -- --headed
```

To open Playwright's interactive test runner, use:

```sh
npm run test:e2e -- --ui
```

To run the tests with a visible browser and Playwright's debugger, use:

```sh
npm run test:e2e -- --debug
```

To use breakpoints in both applications, open VS Code's **Run and Debug** view
and select **dev**. This builds and launches the ASP.NET Core API under the .NET
debugger, starts Vite without opening an extra browser window, and launches the
Vue app in the VS Code Chrome debugger. The individual **debug: backend** and
**debug: frontend** configurations are also available.
