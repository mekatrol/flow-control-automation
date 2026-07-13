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

- Go for the API server and automation execution engine.
- Vue.js and SVG for the graphical flow designer.

## Project Structure

```text
flow-control-automation/
├── backend/             Go module and API server
│   └── cmd/server/      Server entry point
├── frontend/
│   └── flow-control-ui/ Vue application
└── .vscode/             Development tasks and extension recommendations
```

## Local Development

Install Go 1.24 or later and a current Node.js/npm release. The Vue application
dependencies are installed from its project directory:

```sh
cd frontend/flow-control-ui
npm install
```

After the Vue setup has installed its dependencies, open this repository root in
one VS Code window. Run **Tasks: Run Task** from the Command Palette and choose
**dev**. VS Code starts these tasks in separate integrated terminal panes:

- `go run ./cmd/server` from `backend/`
- `npm run dev` from `frontend/flow-control-ui/`

The Go API listens on `http://localhost:8080`. Vite proxies browser requests under
`/api` to that address during local development. Its health endpoint is:

```text
GET http://localhost:8080/api/health
```

Flow create, edit, save, and delete operations are persisted as JSON in
`backend/data/flows.json`. Set `FLOW_DATA_FILE` to use another file, which is
particularly useful for mounting a durable Docker or Home Assistant data volume.
Set `VITE_API_PROXY` if the development backend listens somewhere other than
`http://localhost:8080`.

Run the backend tests from `backend/` with:

```sh
go test ./...
```

You can also run either the **dev: backend** or **dev: frontend** task on its own.

Run the complete frontend quality suite from `frontend/flow-control-ui` with:

```sh
npm run format
npm run lint
npm run test:unit -- --run
npm run test:e2e
npm run build
```

To use breakpoints in both applications, open VS Code's **Run and Debug** view
and select **Debug full stack**. This starts the Go debugger, starts Vite without
opening an extra browser window, and launches the Vue app in the VS Code Chrome
debugger. The individual **Debug Go server** and **Debug Vue app** configurations
are also available.
