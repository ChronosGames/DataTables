# Planning records

This directory contains time-bounded reviews, optimization plans, and implementation plans. Plans are useful for sequencing work, but durable architectural rules should be promoted to `docs/adr/` once accepted.

## Current records

| Record | Status | Purpose |
| --- | --- | --- |
| [agent-engineering-review-2026-07.md](agent-engineering-review-2026-07.md) | active | Reviews Agent guidance drift and defines the plan implemented in `AGENTS.md`, `CLAUDE.md`, docs routing, and future ADR work. |
| [optimization-plan-2026-07.md](optimization-plan-2026-07.md) | active | Tracks the second-phase DataTables architecture optimization plan across generator, runtime, data source, diagnostics, and docs. |

## Status values

- `proposed`: drafted but not yet accepted for execution;
- `active`: currently guiding implementation;
- `completed`: implemented and retained as historical context;
- `superseded`: replaced by a newer plan or ADR.

## Maintenance rules

- Keep plans scoped to a date or phase.
- Link completed or durable decisions to ADRs when the plan produces long-lived rules.
- Update this index when adding, completing, or superseding planning records.
