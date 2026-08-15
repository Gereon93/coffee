import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { readFileSync, statSync } from 'node:fs'
import { join } from 'node:path'

function readCommitFromGitDir(): string | null {
  try {
    let gitDir = '.git'
    if (statSync(gitDir).isFile()) {
      gitDir = readFileSync(gitDir, 'utf8').replace('gitdir: ', '').trim()
    }
    const head = readFileSync(join(gitDir, 'HEAD'), 'utf8').trim()
    const sha = head.startsWith('ref: ')
      ? readFileSync(join(gitDir, head.slice(5)), 'utf8').trim()
      : head
    return sha.slice(0, 7)
  } catch {
    return null
  }
}

const commit = process.env.BUILD_COMMIT ?? readCommitFromGitDir() ?? 'dev'

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
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./src/test/setup.ts'],
    css: false,
    coverage: {
      provider: 'v8',
      reporter: ['text-summary', 'lcov'],
      reportsDirectory: 'coverage',
      include: ['src/**/*.{ts,tsx}'],
      exclude: ['src/main.tsx', 'src/lib/sentry.ts', 'src/**/*.d.ts', 'src/test/**'],
    },
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
