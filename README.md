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
