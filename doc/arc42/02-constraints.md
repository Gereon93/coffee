# 2. Constraints

## 2.1 Technical Constraints

| Constraint | Description |
|------------|-------------|
| Single-machine scope | Only one EQ900 is tracked; the `MachineId` field exists but is always `EQ900-DEFAULT` |
| LAN-only deployment | API and dashboard run exclusively in the local network; only n8n has internet access |
| SQLite | No concurrent writers; single-file storage; no server process required |
| n8n as cloud gateway | All BSH/Home Connect OAuth2 credentials live exclusively in n8n; the API never talks to the cloud directly |
| .NET 10 LTS | Backend targets .NET 10 (Long Term Support) |
| Docker hosting | Both API and dashboard are containerised and deployed via Portainer on a Synology NAS |

## 2.2 Organisational Constraints

| Constraint | Description |
|------------|-------------|
| Self-hosted | No cloud SaaS for core services; everything runs on-premise |
| Solo developer | One person maintains the entire stack |
| n8n dependency | Scheduling, OAuth2 token refresh, and error notifications are delegated to n8n |

## 2.3 Contractual Constraints

| Constraint | Description |
|------------|-------------|
| BSH Home Connect API | Rate limits and acceptable-use policies apply; the 7-second cache on `/coffee/status` mitigates quota usage |
| No PII | System does not collect personal data; only machine counters and status |
