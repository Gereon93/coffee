# 12. Glossary

| Term | Definition |
|------|-----------|
| **BSH** | BSH Hausgeraete GmbH, manufacturer of Bosch/Siemens/Neff/Gaggenau home appliances |
| **Home Connect** | BSH's cloud platform and API for smart home appliance integration |
| **EQ900** | Siemens fully-automatic coffee machine model; the subject of this project |
| **Snapshot** | A point-in-time reading of the machine's counters and status fields |
| **Ingest** | The process of receiving and storing a new snapshot from n8n |
| **Idempotency** | Property that processing the same payload multiple times produces the same result as processing it once |
| **Counter** | A monotonically increasing integer representing the lifetime count of a beverage type |
| **Delta** | The difference between two counter values; represents actual consumption |
| **Cross-Day Delta** | Delta computation that spans midnight; requires the previous day's last snapshot as baseline |
| **Mass-Import** | A MarkedDay kind indicating backfilled data that should be excluded from statistics |
| **Event** | A MarkedDay kind indicating valid data with an annotation (birthday, visitors, etc.) |
| **Heatmap** | A matrix visualisation of consumption by day-of-week (Y) and hour (X) |
| **Anomaly** | A day with consumption significantly above the statistical mean (Z-score based) |
| **n8n** | Self-hosted workflow automation platform; acts as cloud gateway and scheduler |
| **Scalar** | Modern API documentation UI; replaces Swagger/Swashbuckle |
| **MigrationBaseliner** | Utility that seeds EF Core migration history for pre-migration databases |
| **GHCR** | GitHub Container Registry; hosts Docker images for deployment |
| **Portainer** | Web-based Docker management UI running on the Synology NAS |
| **GlitchTip** | Self-hosted error tracking service compatible with the Sentry API |
| **TZ** | UTC offset in minutes; sent by the frontend as a query parameter |
