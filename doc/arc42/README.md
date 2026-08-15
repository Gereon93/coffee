# arc42 Architecture Documentation — Coffee Analytics Hub

This directory holds the architecture documentation for the **Coffee Analytics
Hub**, structured along the [arc42](https://arc42.org/) template (v8).

The Coffee Analytics Hub tracks and visualises the coffee consumption of a
Siemens EQ900 espresso machine. Counter readings are pulled from the BSH Home
Connect API by an n8n workflow every 15 minutes, POSTed to an ASP.NET Core API,
stored in SQLite, and visualised in a React dashboard.

## How to read this

| Question | Section |
|----------|---------|
| What is this system for, and what must it do well? | [01](01-introduction.md), [10](10-quality.md) |
| What am I not allowed to change? | [02](02-constraints.md) |
| Who talks to whom? | [03](03-context.md) |
| Why does it look like this? | [04](04-solution.md), [09](09-design.md) |
| Where is the code that does X? | [05](05-building-blocks.md) |
| What happens when a request arrives? | [06](06-runtime.md) |
| How does it get to production? | [07](07-deployment.md) |
| How are time, security, errors, tests handled? | [08](08-concepts.md) |
| What is broken or shaky? | [11](11-risks.md) |
| What does this word mean? | [12](12-glossary.md) |

## Structure

| File | arc42 Section | Content |
|------|---------------|---------|
| [01-introduction.md](01-introduction.md) | Introduction & Goals | Purpose, requirements overview, stakeholders, quality goals |
| [02-constraints.md](02-constraints.md) | Constraints | Technical, organisational, contractual constraints |
| [03-context.md](03-context.md) | System Context | External systems, business and technical interfaces |
| [04-solution.md](04-solution.md) | Solution Strategy | Fundamental design decisions and their rationale |
| [05-building-blocks.md](05-building-blocks.md) | Building Block View | Static decomposition, levels 1–3 |
| [06-runtime.md](06-runtime.md) | Runtime View | Dynamic behaviour, key scenarios |
| [07-deployment.md](07-deployment.md) | Deployment View | Infrastructure, containers, CI/CD |
| [08-concepts.md](08-concepts.md) | Cross-cutting Concepts | Domain model, time, security, errors, testing, observability |
| [09-design.md](09-design.md) | Design Decisions | ADRs with context, decision, consequences |
| [10-quality.md](10-quality.md) | Quality Requirements | Quality tree, measurable scenarios |
| [11-risks.md](11-risks.md) | Risks & Technical Debt | Ranked risk register and debt list |
| [12-glossary.md](12-glossary.md) | Glossary | Domain and technical terms |

## Source of truth

This documentation describes the code in this repository. Where the two
disagree, the code wins and the documentation is a defect. Sections that
depend on implementation detail carry file references (e.g.
`CoffeeApi/Services/SnapshotService.cs:120`) so the claim can be checked.

Related, non-arc42 documents at the repository root:

| Document | Purpose |
|----------|---------|
| `README.md` | Setup, deployment, endpoint overview |
| `SPEC.md` | HTTP API contract |
| `CLAUDE.md` / `AGENTS.md` | Conventions for AI-assisted contributions |

**Verified against:** 81 passing xUnit tests (`dotnet test CoffeeTest/`),
branch `Gereon93/cichlid`.
