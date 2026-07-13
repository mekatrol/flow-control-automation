# Flow Control UI

Vue 3 and TypeScript frontend for creating, editing, saving, and deploying Flow
Control automation graphs. The designer uses accessible HTML controls around an
interactive SVG canvas and obtains persisted and runtime state from the backend API.

## Requirements

- Node.js 22.18 or newer (or 24.12 or newer)
- npm
- A current Chromium-based browser. Desktop Chrome/Chromium and the Chromium
  mobile viewport are the supported and continuously tested browser targets.

## Development and verification

```sh
npm install
npm run dev
```

The development server proxies no API by itself; run the repository's Go backend
or use deterministic Playwright route fixtures. Before merging frontend changes,
run the same checks required by the completed migration:

```sh
npm run format
npm run lint
npm run test:unit -- --run
npm run test:e2e
npm run build
```

Playwright starts its own Vite server and covers desktop and mobile Chromium. Its
route suite includes direct designer URLs and reloads, responsive overflow, the
critical create/edit/save/deploy lifecycle, keyboard interaction, and a 120-node
graph fixture.

## Production base path

The default build uses `/` as its public base. Set `VITE_BASE_PATH` when the UI is
served below a Docker reverse-proxy or Home Assistant ingress prefix. Include both
leading and trailing slashes:

```sh
VITE_BASE_PATH=/flow-control/ npm run build
```

The web server must return `index.html` for unknown frontend routes such as
`/flow-control/flows/example`; Vue Router then resolves the direct URL. API calls
remain rooted at `/api` and should be routed to the Go backend by the deployment.

## Architecture references

- [Flow DTO schema](../../.codex/ui-flow-schema.md)
- [Runtime API contract](../../.codex/ui-runtime-api.md)
- [Legacy retirement record](../../.codex/ui-legacy-retirement.md)
- [Completed migration plan](../../.codex/ui-migration-plan.md)
