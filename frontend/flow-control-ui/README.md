# Flow Control UI

Vue 3 and TypeScript frontend for creating, editing, saving, and deploying Flow
Control automation graphs. The designer uses accessible HTML controls around an
interactive SVG canvas and obtains persisted and runtime state from the backend API.

## Requirements

- Node.js 22.18 or newer (or 24.12 or newer)
- npm
- Current Chromium, Firefox, and Microsoft Edge browsers. Desktop Chromium,
  Firefox, and Edge, plus a Chromium mobile viewport, are the supported and
  continuously tested browser targets.

## Development and verification

```sh
npm install
npm run test:e2e:install
npm run dev
```

The default install command downloads Playwright's bundled Chromium and Firefox
browsers. Microsoft Edge is a branded, system-wide browser and must also be
installed before running the complete test matrix.

### Installing Microsoft Edge on Linux

On Playwright-supported Ubuntu and Debian releases, install Edge with:

```sh
npm run test:e2e:install:edge
```

Playwright's branded Edge installer supports Ubuntu and Debian but rejects
Ubuntu-derived distributions even when their packages are compatible. This
applies to the current development environment, Linux Mint 22.3.

For Linux Mint 22.3, download the Linux `.deb` from
[Microsoft's Edge download page](https://www.microsoft.com/edge/download).
Then install it from the download directory:

```sh
cd ~/Downloads
sudo apt install ./microsoft-edge-stable_*_amd64.deb
```

APT may report that the local download was read unsandboxed because the `_apt`
user could not access it. That warning is harmless when the package finishes
with `Setting up microsoft-edge-stable`.

Verify the standard executable and run the Edge tests:

```sh
/opt/microsoft/msedge/msedge --version
cd ~/repos/flow-control-automation/frontend/flow-control-ui
npm run test:e2e -- --project=desktop-edge
```

CI images must likewise install Edge at `/opt/microsoft/msedge/msedge` before
running the complete Playwright suite.

The development server proxies `/api` to the ASP.NET Core backend at
`http://localhost:5008` by default. Set `VITE_API_PROXY` to override that
address. Before merging frontend changes, run the same checks required by the
completed migration:

```sh
npm run format
npm run lint
npm run test:unit -- --run
npm run test:e2e
npm run test:e2e:dotnet
npm run build
```

Playwright starts its own Vite server and covers desktop Chromium, Firefox, and
Microsoft Edge, plus mobile Chromium. Its route suite includes direct designer
URLs and reloads, responsive overflow, the critical create/edit/save/deploy
lifecycle, keyboard interaction, and a 120-node graph fixture.

## Production base path

The default build uses `/` as its public base. Set `VITE_BASE_PATH` when the UI is
served below a Docker reverse-proxy or Home Assistant ingress prefix. Include both
leading and trailing slashes:

```sh
VITE_BASE_PATH=/flow-control/ npm run build
```

The web server must return `index.html` for unknown frontend routes such as
`/flow-control/flows/example`; Vue Router then resolves the direct URL. API calls
remain rooted at `/api` and should be routed to `Server.Api` by the deployment.

## Architecture references

- [Portable flow runtime architecture](../../docs/portable-flow-runtime-architecture.md)
- [Flow DTO schema](../../.codex/ui-flow-schema.md)
- [Runtime API contract](../../.codex/ui-runtime-api.md)
