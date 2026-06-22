# 11. Risks and Technical Debt

## 11.1 Known Issues

| ID | Issue | Severity | Description |
|----|-------|----------|-------------|
| TD-01 | Stale `.gitlab-ci.yml` reference in `Coffee.sln` | Low | The solution file still lists `.gitlab-ci.yml` under `SolutionItems`, but the file was deleted during the GitHub migration. |
| TD-02 | API key comparison is not constant-time | Medium | `ApiKeyMiddleware` uses `String.Equals()` which is vulnerable to timing attacks. Should use a constant-time comparison. |
| TD-03 | MarkedDaysController bypasses service layer | Medium | Directly accesses `AppDbContext` instead of going through a service, violating the Controller → Service → EF Core convention. |
| TD-04 | StatsController partially bypasses service layer | Low | Baseline snapshot queries in `GetDaily` and `GetRange` access `AppDbContext` directly. |
| TD-05 | Power endpoint has no authentication | Medium | `POST /coffee/power` can turn the machine on/off without authentication. Mitigated by LAN-only deployment and UI time lock. |
| TD-06 | Exception messages leaked to clients | Medium | `IngestController` and `PowerController` return `ex.Message` in 500 responses, potentially exposing internal details. |
| TD-07 | CORS allows any origin | Low | `AllowAnyOrigin()` is configured globally. Acceptable for LAN-only but overly permissive. |
| TD-08 | Unused `KeyMappings` dictionary in SnapshotService | Low | A static dictionary is defined but never used; the actual mapping is done via a switch statement. |
| TD-09 | `formatDisplayDate` duplicated in frontend | Low | Function exists in both `src/lib/dateUtils.ts` and `src/pages/LogPage.tsx`. |
| TD-10 | No pagination validation | Low | `StatsController.GetAll` does not validate `page` or `pageSize` parameters (negative values, zero). |
| TD-11 | `DateTime.UtcNow` as default value in entity constructors | Low | `CreatedAt` defaults are evaluated at class load time in some contexts; should be set explicitly. |
| TD-12 | No rate limiting | Low | No rate limiting configured on any endpoint. |
| TD-13 | `coffee-dashboard/.env` committed to repository | Low | The `.env` file is tracked in git; should be in `.gitignore` if it contains environment-specific values. |
| TD-14 | `HourlyPeaksChart` uses local `getHours()` | Medium | The chart component calls `new Date(timestamp).getHours()` which uses the browser's local timezone, but the backend already converts timestamps based on the `tz` parameter. This could lead to double-conversion. |
| TD-15 | Counter reset not handled | Medium | If the machine's counters reset to 0 (e.g. after maintenance), the idempotency logic treats it as a duplicate (counters not increased) and does not persist the reset. This means the baseline for delta computation remains at the old high value, causing incorrect (negative, clamped to 0) deltas until counters exceed the previous high. |
| TD-16 | Timezone offset not DST-aware | Low | The `tz` parameter is computed once per request from the browser's current timezone. Queries spanning CET/CEST boundaries use the current offset for all dates, causing a 1-hour shift for dates in the other DST period. |

## 11.2 Risks

| Risk | Probability | Impact | Mitigation |
|------|-------------|--------|------------|
| SQLite concurrent write limitation | Low | Low | Only one writer (ingest); reads don't block writes in SQLite |
| n8n single point of failure | Medium | High | n8n handles its own retries; API gracefully handles missing data |
| BSH API changes | Low | High | n8n abstraction layer isolates the API from BSH changes |
| NAS hardware failure | Low | High | SQLite file can be backed up by simple file copy |
| .NET 10 EOL | Low | Medium | .NET 10 is LTS; supported until November 2028 |
