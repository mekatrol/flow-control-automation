import { readFileSync } from 'node:fs';
import { fileURLToPath, URL } from 'node:url';

import { defineConfig, type Plugin } from 'vitest/config';
import vue from '@vitejs/plugin-vue';
import vueDevTools from 'vite-plugin-vue-devtools';

const defaultChunkSizeLimitKb = 500;
const apiKeyPlaceholder = '__FLOW_CONTROL_API_KEY__';
const chunkSizeBudgets = [
  { name: 'AppYamlEditor', pattern: /^assets\/AppYamlEditor-.*\.js$/, limitKb: 1_300 },
  { name: 'Monaco editor API', pattern: /^assets\/editor\.api-.*\.js$/, limitKb: 2_700 },
  { name: 'Monaco YAML worker', pattern: /^assets\/YamlWorker-.*\.js$/, limitKb: 1_200 }
];

const localApiSettingsUrl = new URL(
  '../../backend/Server/Server.Api/appsettings.Local.json',
  import.meta.url
);

const getLocalDevelopmentApiKey = (): string => {
  if (process.env.VITE_FLOW_CONTROL_API_KEY) return process.env.VITE_FLOW_CONTROL_API_KEY;

  const settings = JSON.parse(readFileSync(localApiSettingsUrl, 'utf8')) as {
    ApiAccess?: { Identities?: Record<string, { Key?: string }> };
  };
  const apiKey = Object.values(settings.ApiAccess?.Identities ?? {}).find(
    (identity) => identity.Key
  )?.Key;
  if (!apiKey) {
    throw new Error(
      `Configure an API identity in ${fileURLToPath(localApiSettingsUrl)} before starting Vite.`
    );
  }

  return apiKey;
};

const escapeHtmlAttribute = (value: string): string =>
  value
    .replaceAll('&', '&amp;')
    .replaceAll('"', '&quot;')
    .replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;');

const localApiKeyPlugin = (apiKey: string | undefined): Plugin => ({
  name: 'local-api-key',
  transformIndexHtml: apiKey
    ? (html) => html.replace(apiKeyPlaceholder, escapeHtmlAttribute(apiKey))
    : undefined
});

const chunkSizeBudgetPlugin = (): Plugin => {
  return {
    name: 'chunk-size-budgets',
    generateBundle(_options, bundle) {
      for (const output of Object.values(bundle)) {
        if (output.type !== 'chunk') continue;

        const budget = chunkSizeBudgets.find(({ pattern }) => pattern.test(output.fileName));
        const limitKb = budget?.limitKb ?? defaultChunkSizeLimitKb;
        const sizeKb = new TextEncoder().encode(output.code).byteLength / 1_000;

        if (sizeKb <= limitKb) continue;

        this.warn(
          `${output.fileName} is ${sizeKb.toFixed(2)} kB, exceeding the ` +
            `${budget?.name ?? 'default'} chunk budget of ${limitKb} kB.`
        );
      }
    }
  };
};

// https://vite.dev/config/
export default defineConfig(({ command }) => ({
  // Home Assistant add-ons commonly serve their UI below an ingress prefix.
  // Vite rewrites built asset URLs against this value while Vue Router continues
  // to receive the browser-visible route.
  base: process.env.VITE_BASE_PATH || '/',
  server: {
    // During local development the browser talks to Vite. Forward API calls to
    // the separately running ASP.NET Core process so the UI uses the durable store rather
    // than requiring cross-origin URLs or development-only CORS permissions.
    proxy: {
      '/api': process.env.VITE_API_PROXY || 'http://localhost:8080'
    },
    // Runtime stores use atomic temporary files. Watching them on Windows can
    // surface transient EBUSY errors and terminate the development server.
    watch: { ignored: ['**/data/**', '**/test-results/**'] }
  },
  plugins: [
    vue(),
    ...(process.env.FLOW_UI_E2E ? [] : [vueDevTools()]),
    localApiKeyPlugin(command === 'serve' ? getLocalDevelopmentApiKey() : undefined),
    chunkSizeBudgetPlugin()
  ],
  optimizeDeps: {
    // The E2E suite opens several lazy routes in parallel. If Vite discovers a
    // new dependency after a test has started interacting with a page, its
    // optimizer forces a full-page reload and destroys transient UI state such
    // as the selected designer node. Serve undiscovered dependencies directly
    // during E2E runs so browser interactions are never interrupted by a
    // development-only optimizer reload.
    noDiscovery: Boolean(process.env.FLOW_UI_E2E),
    include: process.env.FLOW_UI_E2E
      ? [
          'vue',
          'pinia',
          'vue-router',
          'yaml',
          'monaco-yaml',
          'monaco-yaml/yaml.worker.js',
          'monaco-editor/esm/vs/editor/editor.worker',
          'monaco-editor/esm/vs/editor/editor.main',
          'monaco-editor/esm/vs/basic-languages/yaml/yaml.contribution'
        ]
      : undefined
  },
  build: {
    // Per-chunk budgets are enforced by chunkSizeBudgetPlugin. Keep Vite's
    // aggregate limit out of the way because it cannot exempt known large chunks.
    chunkSizeWarningLimit: Number.MAX_SAFE_INTEGER
  },
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url)),
      '@contracts': fileURLToPath(new URL('../../testdata/contracts', import.meta.url))
    }
  },
  test: {
    // Keep Playwright's `*.spec.ts` files owned by the Playwright VS Code adapter.
    // Without a scoped include, Vitest also discovers everything under `e2e`.
    include: ['src/**/*.spec.ts', 'eslint-rules/**/*.spec.ts']
  }
}));
