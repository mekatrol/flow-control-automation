# Flow Control Automation

Flow Control Automation is a home automation flow engine with a graphical editor for creating, deploying, and managing automation logic.

## What the Application Does

The application allows users to build automation flows by connecting visual nodes in a web interface. Each deployed flow runs independently in the backend and can be triggered in two ways:

- By events, such as messages received from an MQTT broker.
- At configured intervals using a timed loop.

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

- ASP.NET Core on .NET 10 for the API server and automation execution engine.
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

The ASP.NET Core API listens on `http://localhost:5008`. Until the frontend proxy
default is changed, set `VITE_API_PROXY=http://localhost:5008` when running Vite
against this backend. The starter endpoint is:

```text
GET http://localhost:5008/weatherforecast
```

### Backend tasks

The repository provides these VS Code tasks:

- **clean: backend** — runs `dotnet clean`.
- **check format: backend** — runs `dotnet format --verify-no-changes` against
  `backend/Server/.editorconfig`.
- **format: backend** — applies the formatting and code-style rules from
  `backend/Server/.editorconfig`.
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
dotnet run --project backend/Server/Server.Api/Server.Api.csproj --launch-profile http
```

When working from the command line, run the format verification before
`dotnet build`; the VS Code **build: backend** task enforces this automatically.
You can also run either **dev: backend** or **dev: frontend** on its own.

Run the complete frontend quality suite from `frontend/flow-control-ui` with:

```sh
npm run format
npm run lint
npm run test:unit -- --run
npm run test:e2e
npm run build
```

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
