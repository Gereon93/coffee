import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { execSync } from 'child_process'

let commit = process.env.BUILD_COMMIT ?? 'dev'
try {
  commit = execSync('git rev-parse --short HEAD').toString().trim()
} catch {
  // git not available (e.g. Docker build) — use BUILD_COMMIT env or fallback
}

const buildTime = new Date().toLocaleString('de-DE', {
  timeZone: 'Europe/Berlin',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
})

export default defineConfig({
  plugins: [react(), tailwindcss()],
  define: {
    __BUILD_COMMIT__: JSON.stringify(commit),
    __BUILD_TIME__: JSON.stringify(buildTime),
  },
  server: {
    proxy: {
      '/api': {
        // Backend-Adresse fuer den Dev-Proxy; ueber VITE_API_PROXY_TARGET ueberschreibbar.
        target: process.env.VITE_API_PROXY_TARGET ?? 'http://localhost:8089',
        changeOrigin: true,
      },
    },
  },
})
