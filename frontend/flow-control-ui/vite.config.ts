import { fileURLToPath, URL } from 'node:url';

import { defineConfig } from 'vite';
import vue from '@vitejs/plugin-vue';
import vueDevTools from 'vite-plugin-vue-devtools';

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
  plugins: [vue(), vueDevTools()],
  resolve: {
    alias: {
      '@': fileURLToPath(new URL('./src', import.meta.url))
    }
  }
});
