# 10. Quality Requirements

## 10.1 Quality Tree

```
Quality
├── Correctness
│   ├── Counter deltas are computed correctly across day boundaries
│   ├── Timezone handling produces correct local day boundaries
│   ├── Idempotency prevents duplicate records
│   └── Heatmap excludes mass-import days but includes event days
├── Reliability
│   ├── System operates unattended 24/7
│   ├── n8n handles retries and token refresh
│   ├── HomeConnectService handles timeouts gracefully
│   └── Migration baseliner prevents data loss on schema updates
├── Performance
│   ├── Read API responds in < 100ms
│   ├── Heatmap aggregation for 52 weeks completes in < 1s
│   └── Dashboard loads within 2s on first paint
├── Security
│   ├── Ingest endpoint requires API key
│   ├── No secrets in source code
│   └── No PII in error tracking
├── Maintainability
│   ├── Controller → Service → EF Core separation
│   ├── DTOs at controller boundary
│   ├── 77+ tests covering services, controllers, and integration
│   └── Type-safe API contracts (OpenAPI + TypeScript types)
└── Usability
    ├── Dark mode via system preference
    ├── Mobile-first responsive design
    └── German UI labels
```

## 10.2 Quality Scenarios

| Scenario | Expected Outcome |
|----------|-----------------|
| n8n sends the same payload twice | First: 201 Created. Second: 200 OK, no duplicate record |
| Machine counter resets to 0 | System treats reset as duplicate (counters not increased); reset is not persisted. This is a known limitation documented in issue #8. |
| BSH API is unreachable | `/coffee/status` returns `reachable: false` with "Offline" label; no exception thrown |
| User queries daily stats for a day with no snapshots | Returns empty snapshot list and zeroed summary |
| Container restarts with existing database | MigrationBaseliner detects pre-migration DB; `Migrate()` applies only pending migrations |
| Dashboard sends `tz=60` (CET) | Day boundaries are computed as 23:00 UTC (previous day) to 23:00 UTC (current day) |
| Mass-import day is marked | Excluded from heatmap aggregation and anomaly detection; greyed out in charts |
