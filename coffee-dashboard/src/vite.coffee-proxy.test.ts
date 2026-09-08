import { createServer as createHttpServer, type IncomingMessage, type ServerResponse } from 'node:http'
import { afterAll, beforeAll, describe, expect, it } from 'vitest'
import { createServer, type ViteDevServer } from 'vite'

function readBody(req: IncomingMessage): Promise<string> {
  return new Promise((resolve, reject) => {
    const chunks: Buffer[] = []
    req.on('data', (chunk: Buffer) => chunks.push(chunk))
    req.on('end', () => resolve(Buffer.concat(chunks).toString('utf8')))
    req.on('error', reject)
  })
}

describe('vite /coffee proxy', () => {
  let backend: ReturnType<typeof createHttpServer>
  let backendPort: number
  let vite: ViteDevServer
  let vitePort: number
  const seen: { method: string; url: string; body: string }[] = []

  beforeAll(async () => {
    backend = createHttpServer(async (req: IncomingMessage, res: ServerResponse) => {
      const body = await readBody(req)
      seen.push({ method: req.method ?? '', url: req.url ?? '', body })

      if (req.method === 'GET' && req.url === '/coffee/status') {
        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({ power: 'on' }))
        return
      }

      if (req.method === 'POST' && req.url === '/coffee/power') {
        res.writeHead(200, { 'Content-Type': 'application/json' })
        res.end(JSON.stringify({ ok: true }))
        return
      }

      res.writeHead(404)
      res.end()
    })

    await new Promise<void>((resolve) => {
      backend.listen(0, '127.0.0.1', () => resolve())
    })
    const address = backend.address()
    if (!address || typeof address === 'string') {
      throw new Error('backend failed to bind')
    }
    backendPort = address.port

    vite = await createServer({
      configFile: false,
      root: process.cwd(),
      logLevel: 'error',
      server: {
        host: '127.0.0.1',
        port: 0,
        strictPort: false,
        proxy: {
          '/coffee': {
            target: `http://127.0.0.1:${backendPort}`,
            changeOrigin: true,
          },
        },
      },
    })
    await vite.listen()
    const urls = vite.resolvedUrls?.local
    if (!urls?.[0]) {
      throw new Error('vite failed to expose a local URL')
    }
    vitePort = Number(new URL(urls[0]).port)
  }, 30_000)

  afterAll(async () => {
    await vite?.close()
    await new Promise<void>((resolve, reject) => {
      backend.close((err) => (err ? reject(err) : resolve()))
    })
  })

  it('forwards GET /coffee/status to the stub backend', async () => {
    seen.length = 0
    const response = await fetch(`http://127.0.0.1:${vitePort}/coffee/status`)
    expect(response.status).toBe(200)
    await expect(response.json()).resolves.toEqual({ power: 'on' })
    expect(seen).toContainEqual({ method: 'GET', url: '/coffee/status', body: '' })
  })

  it('forwards POST /coffee/power to the stub backend', async () => {
    seen.length = 0
    const response = await fetch(`http://127.0.0.1:${vitePort}/coffee/power`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ power: 'off' }),
    })
    expect(response.status).toBe(200)
    await expect(response.json()).resolves.toEqual({ ok: true })
    expect(seen).toContainEqual({
      method: 'POST',
      url: '/coffee/power',
      body: JSON.stringify({ power: 'off' }),
    })
  })
})
