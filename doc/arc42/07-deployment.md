# 7. Deployment View

## 7.1 Infrastructure Overview

```
┌─────────────────────────────────────────────────────────────────────┐
│                        Synology NAS (LAN)                            │
│                                                                      │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                     Portainer                                 │   │
│  │                                                               │   │
│  │  ┌──────────────────────┐  ┌──────────────────────────────┐  │   │
│  │  │  coffee-api          │  │  coffee-dashboard            │  │   │
│  │  │  Container           │  │  Container                    │  │   │
│  │  │                      │  │                               │  │   │
│  │  │  .NET 10 + SQLite    │  │  nginx + React SPA            │  │   │
│  │  │  Port 8089 → 8080    │  │  Port 8090 → 80               │  │   │
│  │  │                      │  │                               │  │   │
│  │  │  Volume: /app/data   │  │                               │  │   │
│  │  │  (coffee.db)         │  │                               │  │   │
│  │  └──────────────────────┘  └──────────────────────────────┘  │   │
│  │                                                               │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                                                      │
└─────────────────────────────────────────────────────────────────────┘
         ▲                                    ▲
         │                                    │
         │ LAN only                           │ LAN only
         │                                    │
         │                                    │
┌────────┴────────────────────────────────────┴──────────────────────┐
│                        n8n (self-hosted)                            │
│                                                                      │
│  Cron: */15 7-2 * * *                                               │
│  1. GET BSH Home Connect API (OAuth2)                                │
│  2. POST /api/ingest → coffee-api:8089                               │
│  3. Webhook relay: coffee-power → BSH API                            │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
         │
         │ Internet (OAuth2)
         ▼
┌──────────────────────┐
│  BSH Home Connect    │
│  Cloud API           │
└──────────────────────┘
```

## 7.2 Container Details

### coffee-api

| Property | Value |
|----------|-------|
| Base image | `mcr.microsoft.com/dotnet/aspnet:10.0` |
| Port | 8080 (mapped to 8089 on host) |
| Volume | `/app/data` → persistent SQLite database |
| Environment | `ASPNETCORE_ENVIRONMENT=Production`, `ConnectionStrings__Default`, `ApiKey`, `N8n:*`, `SENTRY_*` |
| Health | `GET /api/health` |

### coffee-dashboard

| Property | Value |
|----------|-------|
| Base image | `nginx:alpine` |
| Port | 80 (mapped to 8090 on host) |
| Routing | SPA: all routes → `index.html` |
| Proxy | `/api/*`, `/scalar/*`, `/openapi/*` → `coffee-api:8080` |
| Caching | `/assets/*` → 1 year, immutable |

## 7.3 CI/CD Pipeline

```
GitHub (push to main)
         │
         ▼
┌─────────────────────────────────────────┐
│  GitHub Actions                          │
│                                          │
│  1. ci.yml: Build + Test (.NET 10)      │
│  2. docker-publish.yml:                  │
│     - Build API image                    │
│     - Build Dashboard image              │
│     - Push to GHCR                       │
│     - Tags: :latest, :sha-<short>        │
└─────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────┐
│  GHCR (ghcr.io/gereon93/)               │
│                                          │
│  coffee-api:latest                       │
│  coffee-api:sha-abc1234                  │
│  coffee-dashboard:latest                 │
│  coffee-dashboard:sha-abc1234            │
└─────────────────────────────────────────┘
         │
         ▼  (manual pull via Portainer)
┌─────────────────────────────────────────┐
│  Synology NAS (Portainer)               │
│                                          │
│  docker-compose pull + up                │
└─────────────────────────────────────────┘
```

## 7.4 Mapping of Building Blocks to Infrastructure

| Building Block | Deployment Artifact | Infrastructure |
|----------------|---------------------|----------------|
| CoffeeApi | `ghcr.io/gereon93/coffee-api` | Docker container on NAS |
| coffee-dashboard | `ghcr.io/gereon93/coffee-dashboard` | Docker container on NAS |
| SQLite DB | `/app/data/coffee.db` | Docker volume on NAS |
| n8n Workflow | External n8n instance | Self-hosted, internet-connected |
| CI/CD | GitHub Actions | GitHub cloud |
