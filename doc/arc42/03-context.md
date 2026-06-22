# 3. System Context

## 3.1 Context Diagram

```
┌─────────────────┐     OAuth2      ┌─────────────────────┐
│  BSH Home       │◄───────────────►│        n8n          │
│  Connect Cloud  │                 │   (Data Fetcher)    │
└─────────────────┘                 └──────────┬──────────┘
                                               │
                                    HTTP POST /api/ingest
                                    HTTP PUT  /webhook/coffee-power
                                               │
                                               ▼
                                    ┌──────────────────────┐
                                    │   Coffee Analytics   │
                                    │    Hub (API)         │
                                    │   ASP.NET Core 10    │
                                    │                      │
                                    │  ┌────────────────┐  │
                                    │  │  SQLite DB     │  │
                                    │  └────────────────┘  │
                                    └──────────┬──────────┘
                                               │
                                       REST API (GET)
                                               │
                                               ▼
                                    ┌──────────────────────┐
                                    │   React Dashboard    │
                                    │   (nginx + SPA)      │
                                    └──────────────────────┘
```

## 3.2 External Systems

### BSH Home Connect Cloud

- **Type:** Cloud API (OAuth2)
- **Role:** Provides machine status and counter readings; accepts power on/off commands
- **Interaction:** Only accessed by n8n, never by the Coffee API directly

### n8n Workflow

- **Type:** External automation platform (self-hosted)
- **Role:** Acts as cloud gateway, scheduler, and relay
- **Schedule:** Cron `*/15 7-2 * * *` (every 15 min, 07:00-02:59)
- **Responsibilities:**
  - OAuth2 token management for BSH API
  - Polling counter readings from Home Connect
  - POSTing payloads to `/api/ingest`
  - Relaying power on/off commands via webhook
  - Error notifications

### React Dashboard (User)

- **Type:** Single-page application
- **Role:** Visualises data, allows annotations, triggers power control
- **Communication:** REST API calls to the Coffee API (proxied via nginx)

## 3.3 Data Flows

### Ingest Flow (n8n → API → SQLite)

1. n8n polls BSH Home Connect API for current machine status
2. n8n POSTs the raw JSON payload to `http://<NAS-IP>:8089/api/ingest` with `X-API-Key` header (or `http://coffee-api:8080/api/ingest` if n8n runs in the same Docker network)
3. API validates the payload, maps Home Connect keys to domain properties
4. API performs idempotency check (counter comparison with last snapshot)
5. If counters increased: new snapshot is persisted (201 Created)
6. If no change: request is acknowledged but not persisted (200 OK)

### Read Flow (Dashboard → API → SQLite)

1. Dashboard requests statistics via GET endpoints (`/api/stats/*`)
2. API queries SQLite, computes deltas, aggregates by day/hour/weekday
3. Dashboard renders charts (bar, area, pie, heatmap)

### Power Control Flow (Dashboard → API → n8n → BSH)

1. Dashboard POSTs to `/coffee/power` with `{ "state": "on" | "off" }`
2. API forwards to n8n webhook (`N8n:PowerWebhookUrl`) via HTTP PUT
3. n8n relays the command to BSH Home Connect API
