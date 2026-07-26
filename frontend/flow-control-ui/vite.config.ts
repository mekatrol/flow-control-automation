import { fileURLToPath, URL } from 'node:url';

import { defineConfig, type Plugin } from 'vite';
import vue from '@vitejs/plugin-vue';
import vueDevTools from 'vite-plugin-vue-devtools';

const defaultChunkSizeLimitKb = 500;
const chunkSizeBudgets = [
  { name: 'AppYamlEditor', pattern: /^assets\/AppYamlEditor-.*\.js$/, limitKb: 1_300 },
  { name: 'Monaco editor API', pattern: /^assets\/editor\.api-.*\.js$/, limitKb: 2_700 },
  { name: 'Monaco YAML worker', pattern: /^assets\/YamlWorker-.*\.js$/, limitKb: 1_200 }
];

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
export default defineConfig({
  // Home Assistant add-ons commonly serve their UI below an ingress prefix.
  // Vite rewrites built asset URLs against this value while Vue Router continues
  // to receive the browser-visible route.
  base: process.env.VITE_BASE_PATH || '/',
  server: {
    // During local development the browser talks to Vite. Forward API calls to
    // the separately running Go process so the UI uses the durable store rather
    // than requiring cross-origin URLs or development-only CORS permissions.
    proxy: {
      '/api': process.env.VITE_API_PROXY || 'http://localhost:8080'
    }
  },
  plugins: [vue(), ...(process.env.FLOW_UI_E2E ? [] : [vueDevTools()]), chunkSizeBudgetPlugin()],
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
  }
});
