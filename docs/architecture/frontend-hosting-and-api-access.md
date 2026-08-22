# Frontend hosting and API access

The Flow Control frontend is a static Vue application that calls the ASP.NET
Core API. API requests require an `X-Api-Key` header, so the browser must receive
the API identity selected by the process serving `index.html`.

## Runtime configuration contract

The source `index.html` contains page-level configuration metadata:

```html
<meta name="flow-control-api-key" content="__FLOW_CONTROL_API_KEY__">
```

The frontend reads this metadata at runtime. It treats a missing element, an
empty value, or the unchanged `__FLOW_CONTROL_API_KEY__` placeholder as an
unconfigured key. The shared API request wrapper adds a configured value to
requests as the `X-Api-Key` header.

The key belongs in a `meta` element because it configures the entire document;
it is not a user-editable form value. Keeping the value out of compiled
JavaScript also allows one frontend build to be deployed with different server
configuration.

## Local development

When Vite serves the application, its HTML transform reads the first API
identity from the gitignored
`backend/Server/Server.Api/appsettings.Local.json` file and replaces the
placeholder in the served HTML. `VITE_FLOW_CONTROL_API_KEY` overrides the value
read from that file. The production build does not perform this replacement.

This keeps the Vite development server and local API on the same identity and
avoids asking a developer to enter the key in the browser.

## Server-hosted frontend

The production bundle retains the placeholder. When ASP.NET serves
`index.html`, a single HTML-response middleware injects the key for the root
document, an explicit `/index.html` request, and SPA fallback routes. It:

1. Selects the identity named by `ApiAccess:FrontendIdentity`, or the first
   configured identity when no frontend identity is named.
2. HTML-attribute-encodes its key.
3. Replaces the exact `__FLOW_CONTROL_API_KEY__` placeholder in the response.
4. Prevents a response containing one deployment's key from being cached and
   served for another deployment or identity.

Replacement occurs in the HTML response, before response compression. Hashed
JavaScript and CSS assets remain static and cacheable because they contain no
deployment key.

The server includes files found in `frontend/flow-control-ui/dist` under its
`wwwroot` output. Build the frontend before building or publishing the server.
ASP.NET default-file, static-file, and SPA fallback handling all run behind the
injection middleware. At startup, the server resolves a built bundle from its
content-root `wwwroot`, compiled-output `wwwroot`, or the repository frontend
`dist` directory. If none contains `index.html`, frontend hosting is disabled
and the API can still start normally.

## Security boundary

An API key delivered to a browser is observable by anyone who can load the page.
It identifies the hosted frontend as an API client; it does not authenticate an
individual user and must not be used as the only authorization boundary between
users of that frontend.

Until user authentication is implemented, deployments must restrict access to
the UI itself, for example through a trusted ingress or reverse proxy. The
injected identity should receive only the permissions the UI needs. A wildcard
administrative identity is appropriate only for an explicitly trusted,
restricted deployment.

The session-storage key and manual prompt are a compatibility fallback for a
bundle served without injected metadata. They are not the intended hosted-user
authentication flow.
