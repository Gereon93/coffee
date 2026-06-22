# arc42 Documentation: Coffee Analytics Hub

## Overview

This directory contains the architecture documentation for the **Coffee Analytics Hub**
following the [arc42](https://arc42.org/) template.

The Coffee Analytics Hub tracks and visualises the coffee consumption of a
Siemens EQ900 espresso machine. Counter readings are pulled from the BSH
Home Connect API by an n8n workflow every 15 minutes, POSTed to the ASP.NET
Core API, stored in SQLite, and visualised in a React dashboard.

## Structure

| File | arc42 Section | Content |
|------|---------------|---------|
| [01-introduction.md](01-introduction.md) | Introduction & Goals | Purpose, stakeholders, quality goals |
| [02-constraints.md](02-constraints.md) | Constraints | Technical, organisational, contractual constraints |
| [03-context.md](03-context.md) | System Context | External systems, data flows |
| [04-solution.md](04-solution.md) | Solution Strategy | Fundamental design decisions |
| [05-building-blocks.md](05-building-blocks.md) | Building Block View | Static decomposition of the system |
| [06-runtime.md](06-runtime.md) | Runtime View | Dynamic behaviour, key scenarios |
| [07-deployment.md](07-deployment.md) | Deployment View | Infrastructure, mapping to infrastructure |
| [08-concepts.md](08-concepts.md) | Cross-cutting Concepts | Domain model, security, error handling |
| [09-design.md](09-design.md) | Design Decisions | Key ADRs and rationale |
| [10-quality.md](10-quality.md) | Quality Requirements | Quality tree, quality scenarios |
| [11-risks.md](11-risks.md) | Risks & Technical Debt | Known issues, technical debt |
| [12-glossary.md](12-glossary.md) | Glossary | Key terms and definitions |
