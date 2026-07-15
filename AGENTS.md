# AGENTS.md

This file is the canonical agent-facing engineering guide for DataTables. `CLAUDE.md` intentionally delegates here to avoid duplicated instructions drifting over time.

## Project fact baseline

DataTables converts Excel `.xlsx` configuration tables into paired, schema-compatible C# classes and `.bytes` data files for .NET server runtimes and Unity clients. Generated code and generated data are two outputs of the same schema and must be regenerated, reviewed, and shipped together.

Stable runtime concepts:

- Preferred runtime API: `await DataTableManager.LoadAsync<T>(name, cancellationToken)`, `DataTableManager.GetCached<T>(name)`, and `DataTableManager.IsLoaded<T>(name)`.
- `DataTableContext` provides isolated data source, cache, loading, cancellation, statistics, and hook state for multi-tenant or test scenarios.
- `EnableEstimatedMemoryBudget` configures an estimated parsed-table LRU budget; it is not a hard process-memory limit.
- `IDataSource` is the canonical asynchronous payload source abstraction. Data source decorators such as cache, compression, encryption, fallback, and versioning operate on payload bytes before table parsing.
- v3 `.bytes` payloads include structured header metadata and schema validation. Do not add silent compatibility for older payload formats unless an explicit migration decision says so.

Avoid unverified performance claims in docs or comments. If a percentage or latency claim is important, link it to a benchmark, test, or `docs/performance/` note; otherwise describe it as a goal or hypothesis.

## Repository map

- `src/DataTables`: core runtime source and the preferred edit location for runtime behavior.
- `src/DataTables.GeneratorCore`: Excel parsing, schema services, code/data generation, T4 templates, and binary writing.
- `src/DataTables.Generator`: CLI wrapper.
- `src/DataTables.MSBuild.Tasks`: MSBuild integration.
- `src/DataTables.Unity`: Unity package mirror. Do not directly edit mirrored runtime files unless the sync/package mechanism itself is being changed.
- `tests/DataTables.Tests`: xUnit + FluentAssertions tests for runtime, generator, data sources, concurrency, and public API metadata.
- `docs`: user guides, designs, references, migration notes, ADRs, and planning records.
- `templates`: Excel/template guidance for table authors.
- `sandbox`: examples and benchmark projects.

## Task routing

### Runtime loading, caching, data sources, hooks, or concurrency

Read first:

- `README.md` runtime API sections.
- `docs/guides/data-source-pipeline.md`.
- Relevant tests in `tests/DataTables.Tests`, especially context, concurrency, LRU cache, data source pipeline, parsing, and public API tests.

Rules:

- Prefer `LoadAsync` / `GetCached` / `IsLoaded` in new code and examples.
- Preserve single-flight loading semantics: concurrent requests for the same table must share the same underlying load.
- Caller cancellation must cancel only that caller's wait unless the context/source is cleared or disposed.
- Keep parsed-table cache budgeting separate from payload-byte caching.
- When changing public API shape, update metadata tests and docs together.

Minimum validation:

```bash
dotnet test tests/DataTables.Tests/DataTables.Tests.csproj --filter "DataTableContextTests|ConcurrencyTest|LRUDataTableCacheTest|DataSourcePipelineTests|PublicApiMetadataTests"
```

### Generator, schema, validation, binary format, or new table types

Read first:

- `docs/guides/table-types.md`.
- `docs/reference/binary-format-v3.md`.
- `docs/planning/optimization-plan-2026-07.md`.
- Relevant generator/schema tests.

Rules:

- Keep `DataTableProcessor` as orchestration; do not add new table-type behavior by expanding the processor main flow.
- New table capabilities should be routed through parser, validator, writer/serialization, template, diagnostics, docs, and tests.
- Diagnostics should identify file, sheet, field, row/column/cell, or table wherever possible.
- Generated C# and `.bytes` are paired outputs; do not update only one side of generated fixtures or examples.
- v3 schema hash/header behavior must remain explicit and fail-fast on mismatches.

Minimum validation:

```bash
dotnet test tests/DataTables.Tests/DataTables.Tests.csproj --filter "Generator|Schema|Binary|DataTableProcessor|DataType"
dotnet build DataTables.sln
```

### T4 templates and generated C# outputs

Read first:

- The relevant `.tt` source file.
- The generated `.cs` file referenced by the template `LastGenOutput` metadata.
- `/workspace/DataTables/.agents/skills/maintain-t4-templates/SKILL.md` when available in the environment.

Rules:

- Edit the `.tt` template first; never treat generated `.cs` as the source of truth.
- Regenerate the `.cs` output after template changes and review both files for expected drift only.
- Keep generated code deterministic.

Minimum validation:

```bash
dotnet build DataTables.sln
```

### Documentation, planning, ADRs, or examples

Read first:

- `README.md` for current user-facing facts.
- `docs/README.md` for document placement rules.
- Related guides, references, migration docs, ADRs, or planning records.

Rules:

- Keep `AGENTS.md` focused on stable facts, task routing, constraints, and validation; avoid duplicating long-form design plans here.
- Put durable decisions in `docs/adr/` and time-bounded plans in `docs/planning/`.
- Mark planning records as active, completed, or superseded when practical.
- Prefer current recommended APIs in examples; list obsolete or compatibility APIs only in compatibility sections.

Minimum validation:

```bash
dotnet build DataTables.sln
```

### Documentation, planning, ADRs, or examples

Read first:

- `README.md` for current user-facing facts.
- `docs/README.md` for document placement rules.
- Related guides, references, migration docs, ADRs, or planning records.

Rules:

- Keep `AGENTS.md` focused on stable facts, task routing, constraints, and validation; avoid duplicating long-form design plans here.
- Put durable decisions in `docs/adr/` and time-bounded plans in `docs/planning/`.
- Mark planning records as active, completed, or superseded when practical.
- Prefer current recommended APIs in examples; list obsolete or compatibility APIs only in compatibility sections.

Minimum validation:

```bash
dotnet build DataTables.sln
```

If the environment cannot run .NET commands, record the limitation explicitly in the final message and PR body.

### Unity package work

Read first:

- README Unity section.
- Runtime source under `src/DataTables`.
- Unity mirror under `src/DataTables.Unity` only to verify packaging/sync effects.

Rules:

- Runtime behavior changes should be made in `src/DataTables` first.
- Do not directly edit Unity mirrored runtime files unless changing package-only glue, tests, metadata, or the sync process.
- Be careful with APIs that assume background threads; Unity WebGL may require calling-context parsing behavior.

Minimum validation:

```bash
dotnet build DataTables.sln
```

## Common commands

```bash
# Build solution
dotnet build DataTables.sln

# Run all repository tests
dotnet test tests/DataTables.Tests/DataTables.Tests.csproj

# Run a focused test filter
dotnet test tests/DataTables.Tests/DataTables.Tests.csproj --filter "FullyQualifiedName~TestMethodName"

# Run benchmark project when performance claims or regressions are involved
dotnet run --project sandbox/Benchmark/Benchmark.csproj -c Release

# Run sample app when changing generated output or runtime loading examples
dotnet run --project sandbox/ConsoleApp/ConsoleApp.csproj
```

## Hard constraints

- Do not put try/catch blocks around imports.
- Do not use `ls -R` or `grep -R`; use `rg` / `find` instead.
- Do not directly edit generated T4 `.cs` outputs without changing and regenerating their `.tt` source.
- Do not directly edit Unity mirrored runtime files as the primary source for runtime changes.
- Do not introduce unverified performance percentages or absolute latency claims.
- Do not silently accept stale generated code/data pairs or schema hash mismatches.
- Do not end work with committed changes but no PR metadata when PR creation is required by the environment.

## Planning and decision records

- Active and historical implementation plans live in `docs/planning/`.
- Durable architecture decisions live in `docs/adr/`.
- The current Agent engineering review is `docs/planning/agent-engineering-review-2026-07.md`.
