/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  // `npm run dev` serves the SPA; `/api/*` is proxied to the Host's plain-HTTP
  // `launchSettings` URL. The auth cookie is `Secure`, but browsers treat
  // `localhost` as a secure context over plain HTTP, so it still round-trips
  // through the Vite origin. `docker compose` needs no proxy (Host serves the
  // built SPA same-origin).
  server: {
    proxy: {
      '/api': { target: 'http://localhost:2052', changeOrigin: true },
    },
  },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./vitest.setup.ts'],
  },
})
