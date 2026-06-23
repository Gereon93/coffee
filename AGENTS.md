# AGENTS.md

> Drop this at the repository root. Codex applies the closest `AGENTS.md` to each
> changed file, so you can place a more specific one deeper in the tree if a
> package needs extra scrutiny. Edit the Project context / Tech stack per repo.

## Project context

Coffee Analytics Hub — tracks and visualises the coffee consumption of a
Siemens EQ900 espresso machine. Counter readings are pulled from the BSH
Home Connect API by an n8n workflow every 15 minutes, POSTed to the ASP.NET
Core API, stored in SQLite, and visualised in a React dashboard.

## Tech stack

- **Backend:** ASP.NET Core (.NET 10), EF Core, SQLite
- **Frontend:** React 19, Vite, TypeScript, Recharts, Tailwind CSS v4
- **State:** TanStack React Query
- **Tests:** xUnit (`CoffeeTest/`)
- **CI/CD:** GitHub Actions → GHCR → Docker/Portainer on NAS
- **Error tracking:** GlitchTip (Sentry-compatible)

## Review guidelines

Codex surfaces only **P0** and **P1** findings on GitHub pull requests. Anything
lower will not appear, so everything worth a human's attention belongs in one of
these two buckets.

**Guiding principle: behavior first.** A change that compiles and reads cleanly
but breaks or silently alters an intended process or UI/UX flow is worse than a
style issue. Verify the code does what it is supposed to do before commenting on
how it is written. Function follows design.

### P0 — block the merge

- **Behavioral regressions:** the change breaks or silently alters an existing
  process, workflow, or UI/UX behavior, or the implemented behavior does not
  match the apparent intent of the PR.
- **Security:** hardcoded secrets / keys / connection strings, missing auth or
  authorization checks, injection (SQL, command, path), unsafe deserialization,
  logging of PII or credentials.
- **Timestamp / timezone correctness:** before judging any timestamp logic,
  determine which timezone the source provides. User-facing timezone is
  Europe/Berlin (CET/CEST). Flag naive `DateTime` where `DateTimeOffset` / UTC is
  required, `DateTime.Now` used for persisted or compared values, and any
  conversion that assumes a timezone without checking the source. When the source
  timezone is genuinely unclear, flag it as an open question rather than guessing.
- **Data correctness:** money / decimal rounding errors, off-by-one, null or
  empty edge cases that change results.
- **Concurrency:** race conditions, deadlocks, shared mutable state without
  synchronization, sync-over-async (`.Result` / `.Wait()`) on request or hot paths.
- **Resource leaks:** undisposed `IDisposable`, unclosed streams / connections,
  mishandled `CancellationToken`.
- **Untested behavior:** new or changed behavior shipped without test coverage
  (see Testing expectations).

### P1 — should fix

- **Architecture:** domain logic leaking into controllers, controllers bypassing
  the service layer to access `AppDbContext` directly, aggregate boundary
  violations. The convention is Controller → Service → EF Core.
- **Clean Code:** single-responsibility violations, methods or classes doing too
  much, unclear naming, duplicated logic that should be extracted, deep nesting,
  primitive obsession.
- **Error handling:** swallowed exceptions, catch-all without context, control
  flow driven by exceptions, exception messages leaked to API clients.
- **Test gaps:** missing integration test for changes that cross a boundary
  (DB, HTTP, messaging); unit tests that assert nothing meaningful or only cover
  the happy path.
- **Performance:** N+1 queries, materializing before filtering (`ToList` too
  early), avoidable allocations in hot paths, missing `async` on IO-bound work.
- **Contract changes:** public API, events, or DTOs changed without updated
  docs / changelog or a migration note.
- **Validation:** missing input validation at trust boundaries.
- **Explanatory comments are a smell:** flag any comment that describes *what*
  the code does or *how* it works. The fix is not a better comment but clearer
  code: extract a well-named method, rename variables, simplify control flow so
  the intent is obvious without prose. Recommend the refactor, never a reworded
  comment.
- **Magic numbers and unexplained literals:** flag them. They must become named
  constants whose name carries the meaning, so neither a comment nor any further
  explanation is needed. This is a pure refactor, unrelated to ADRs.

### Testing expectations

- New behavior needs unit tests. Behavior that crosses a boundary (DB, HTTP,
  messaging) needs an integration test as well.
- Tests must cover failure and edge cases, not only the happy path.
- Flag assertions that do not actually verify the intended outcome.

### What NOT to flag

- Formatting, whitespace, import ordering, and casing already enforced by the
  analyzers / formatter and the build (e.g. SonarQube). Do not duplicate tooling.
- Subjective style preferences with no maintainability impact.
- Pre-existing issues unrelated to the diff, unless the change makes them
  materially worse.
- A comment that *only* references an ADR (e.g. `// rationale: ADR-0012`). It
  explains nothing inline; it points to where the decision lives. That is the
  intended way to keep the "why" without explanatory comments.
- XML doc comments (`///`) on public / published API surface, where they drive
  IntelliSense or generated docs.

Keep comments specific and actionable: state the risk, point to the line,
suggest the fix. If an assumption in the code is not guaranteed to hold, say so
rather than letting it pass.
