# Architecture Decision Records

This directory stores durable architecture decisions for DataTables. Use ADRs for decisions that should remain discoverable after implementation plans or pull requests age out of context.

## When to add an ADR

Add an ADR when a change establishes or reverses a long-lived rule, such as:

- binary format compatibility and migration policy;
- schema hash or generated artifact consistency requirements;
- runtime registration, reflection, loading, cancellation, or caching semantics;
- data source payload/header/manifest semantics;
- Unity packaging and source-of-truth policy;
- generator extension boundaries that future table types must follow.

## Suggested status values

- `proposed`: under discussion and not yet binding;
- `accepted`: current decision;
- `superseded`: replaced by a newer ADR;
- `deprecated`: intentionally retained for history but no longer recommended.

## Suggested template

```markdown
# ADR-0000: Short decision title

- Status: proposed | accepted | superseded | deprecated
- Date: YYYY-MM-DD
- Supersedes: ADR-xxxx, if any
- Superseded by: ADR-xxxx, if any

## Context

What problem, constraint, or trade-off forced a decision?

## Decision

What rule should future contributors and agents follow?

## Consequences

What improves, what gets harder, and what must be validated?
```
