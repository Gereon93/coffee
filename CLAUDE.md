# CLAUDE.md

Guidance for AI coding assistants (and humans) working in this repository.

## Project

**Coffee Analytics Hub** — tracks and visualises the coffee consumption of a
Siemens EQ900 espresso machine. Counter readings are pulled from the BSH
Home Connect API by an n8n workflow every 15 minutes, POSTed to the ASP.NET
Core API, stored in SQLite, and visualised in a React dashboard.

See `ARCHITECTURE.md` for the system design and `SPEC.md` for the API contract.

## Stack

- **Backend:** ASP.NET Core (.NET 10), EF Core, SQLite
- **Frontend:** React 19, Vite, Recharts, Tailwind CSS v4
- **Tests:** xUnit (`CoffeeTest/`)

## Conventions

- **Architecture:** Controller → Service → EF Core. DTOs are mapped at the
  controller boundary; entities are never exposed over the API.
- **Quality:** `Nullable` is enabled; keep it warning-free. Async all the way down.
- **Tests first:** business logic in `Services/` and every controller branch is
  covered. Add/extend tests in the same change as the behaviour.

## Common commands

```bash
dotnet build Coffee.sln -c Release      # build everything
dotnet test CoffeeTest/                 # run the test suite
cd CoffeeApi && dotnet run              # API on http://localhost:5000 (Scalar at /scalar/v1)
cd coffee-dashboard && npm run dev      # dashboard on http://localhost:5173
```
