# Coffee Dashboard

React-Frontend fuer den Coffee Analytics Hub. Zeigt Verbrauchsdaten der Philips EQ900 als interaktive Charts und KPIs.

## Entwicklung

```bash
npm install
npm run dev      # http://localhost:5173
npm run build    # Production Build
npm run lint     # ESLint
```

Der Vite Dev-Server proxied `/api/*` Requests an `http://192.168.2.143:8089` (konfiguriert in `vite.config.ts`).

## Docker

```bash
# Aus dem Projekt-Root:
./build.sh dashboard
```

Baut ein nginx-Image das die statischen Dateien serviert und API-Requests an den `coffee-api` Container weiterleitet (siehe `nginx.conf`).

## Tech Stack

- React 19 + TypeScript
- Vite (Build Tool)
- Recharts (Charts)
- TanStack React Query (Data Fetching)
- Tailwind CSS v4 (Styling)
