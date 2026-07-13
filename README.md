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

The starter Go API listens on `http://localhost:8080`. Its health endpoint is:

```text
GET http://localhost:8080/api/health
```

You can also run either the **dev: backend** or **dev: frontend** task on its own.

To use breakpoints in both applications, open VS Code's **Run and Debug** view
and select **Debug full stack**. This starts the Go debugger, starts Vite without
opening an extra browser window, and launches the Vue app in the VS Code Chrome
debugger. The individual **Debug Go server** and **Debug Vue app** configurations
are also available.
