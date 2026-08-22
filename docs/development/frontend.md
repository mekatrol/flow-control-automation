# Frontend development

The Vue 3 and TypeScript frontend creates, edits, saves, and deploys Flow
Control automation graphs. The designer uses accessible HTML controls around an
interactive SVG canvas and obtains persisted and runtime state from the backend API.

## Requirements

- Node.js 22.18 or newer (or 24.12 or newer)
- npm
- Current Chromium and Microsoft Edge browsers. Desktop Chromium and Edge plus
  a Chromium mobile viewport form the default supported test matrix. Firefox is
  optional while its elevated-Windows Playwright startup defect remains.

## Development and verification

```sh
npm install
npm run test:e2e:install
npm run dev
```

The install command downloads Playwright's bundled Chromium and Firefox
browsers. Microsoft Edge is a branded, system-wide browser and must also be
installed before running the default test matrix.

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
`http://localhost:8080` by default. When the development server starts, Vite
reads the first API identity key from the gitignored
`backend/Server/Server.Api/appsettings.Local.json` file and supplies it to the
frontend through the `flow-control-api-key` metadata in `index.html`. This avoids
presenting the API-key prompt during local debugging and ensures that the
browser uses the same key as the local backend.

Set `VITE_FLOW_CONTROL_API_KEY` before starting Vite to override the local
settings value. Vite replaces the metadata value only when running the
development server. Production builds retain the `__FLOW_CONTROL_API_KEY__`
placeholder; the hosting server must replace it with an HTML-attribute-encoded
API key when serving `index.html`. Set `VITE_API_PROXY` to override the backend
address. Before merging frontend changes, run the same checks required by the
completed migration:

```sh
npm run format
npm run lint
npm run test:unit -- --run
npm run test:e2e
npm run test:e2e -- --last-failed
npm run test:e2e -- e2e/simulatorIo.spec.ts:97 --project=desktop-chromium --reporter=html
npm run test:e2e:dotnet
npm run build
```

You can snapshot page HTML at any time using:

```ts
test('applies numeric interface inputs and presents committed shadow output metadata', async ({
  page
}, testInfo) => {

  // Do stuff...

  // Take snapshop of page HTML
  await testInfo.attach('page.html', {
    body: await page.content(),
    contentType: 'text/html'
  });

  // Do more stuff...
});
```

Playwright starts its own Vite server and covers desktop Chromium, Microsoft
Edge, and mobile Chromium by default. Firefox can be enabled explicitly where
the local Playwright environment supports it. The route suite includes direct designer
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

## Related documentation

- [Frontend hosting and API access](../architecture/frontend-hosting-and-api-access.md)
- [Portable flow runtime architecture](../architecture/portable-flow-runtime.md)
- [Virtual points and execution contexts](../guides/virtual-points.md)
- [Virtual-points API](../reference/virtual-points-api.md)
- [UI flow schema](../reference/ui-flow-schema.md)
- [Runtime API](../reference/runtime-api.md)
