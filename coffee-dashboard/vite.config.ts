import { defineConfig } from 'vitest/config'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import { readFileSync, statSync } from 'node:fs'
import { dirname, join, resolve } from 'node:path'
import { fileURLToPath } from 'node:url'

const CONFIG_DIR = dirname(fileURLToPath(import.meta.url))

function resolveGitDir(startDir: string): string | null {
  let dir = startDir
  for (;;) {
    const candidate = join(dir, '.git')
    const stats = statSync(candidate, { throwIfNoEntry: false })
    if (stats?.isDirectory()) return candidate
    if (stats?.isFile()) {
      return resolve(dir, readFileSync(candidate, 'utf8').replace('gitdir: ', '').trim())
    }
    const parent = resolve(dir, '..')
    if (parent === dir) return null
    dir = parent
  }
}

function readText(...pathSegments: string[]): string | null {
  try {
    return readFileSync(join(...pathSegments), 'utf8').trim()
  } catch {
    return null
  }
}

function readCommitFromGitDir(): string | null {
  const gitDir = resolveGitDir(CONFIG_DIR)
  if (!gitDir) return null

  const head = readText(gitDir, 'HEAD')
  if (!head) return null
  if (!head.startsWith('ref: ')) return head.slice(0, 7)

  const ref = head.slice(5)
  const commonDir = readText(gitDir, 'commondir')
  const sha =
    readText(gitDir, ref) ??
    (commonDir ? readText(resolve(gitDir, commonDir), ref) : null)

  return sha?.slice(0, 7) ?? null
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
