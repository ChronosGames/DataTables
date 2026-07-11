---
name: maintain-t4-templates
description: Maintain T4-backed C# generator templates in the DataTables repository. Use when changing .tt files or their generated .cs outputs, fixing generated C# compilation, adding a new TextTemplatingFilePreprocessor template, resolving drift between a T4 template and LastGenOutput, or regenerating DataTables generator code. Always edit the .tt source first and regenerate the .cs with T4; never treat the generated .cs as the source of truth.
---

# Maintain T4 Templates

Treat every `LastGenOutput` C# file as generated output. Never patch it directly to implement a fix.

## Workflow

1. Inspect `git status --short` and preserve unrelated work.
2. Read the complete `.tt`, its generated `.cs`, the project metadata, and any non-generated partial support such as `Template.cs`.
3. Locate the mapping in the project file:

   ```xml
   <None Update="Example.tt">
     <Generator>TextTemplatingFilePreprocessor</Generator>
     <LastGenOutput>Example.cs</LastGenOutput>
   </None>
   ```

4. Implement the behavior in `.tt` first. Use the existing `.cs` only as a behavior oracle when recovering drift.
5. Ensure regeneration is self-contained. Put constructors and helpers in the T4 template or a non-generated partial file; do not depend on private code that will disappear when the generated `.cs` is overwritten.
6. Preprocess to a temporary directory before overwriting tracked output:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .agents/skills/maintain-t4-templates/scripts/invoke_t4_templates.ps1 -RepositoryRoot .
   ```

7. Compare representative generated output with the current behavior. Cover empty, single-key, and compound-key contexts when indexes or groups are involved.
8. Regenerate tracked `.cs` only after the temporary output is correct:

   ```powershell
   powershell -NoProfile -ExecutionPolicy Bypass -File .agents/skills/maintain-t4-templates/scripts/invoke_t4_templates.ps1 -RepositoryRoot . -WriteGeneratedFiles
   ```

9. Review the `.tt`, project metadata, and generated `.cs` diff together. The `.cs` may change only as T4 output.
10. Run targeted generated-code compilation tests, the full test suite, and the solution build. The bundled script removes trailing horizontal whitespace from preprocessed C# while preserving its BOM and line endings.

## Guardrails

- Stop if a generated `.cs` is dirty while its `.tt` is clean. Port the change into `.tt` before continuing.
- Do not use `-AllowGeneratedOnlyChanges` unless explicitly recovering known template drift.
- Stop when Visual Studio T4 tooling is unavailable. Report the missing prerequisite; do not fall back to editing generated C#.
- Use project-level MSBuild T4 preprocessing. Standalone `TextTransform.exe -PP` can falsely fail when template members come from project partial classes.
- When adding a template, add both `Compile Update`/`DependentUpon` metadata for the generated `.cs` and `None Update`/`Generator`/`LastGenOutput` metadata for the `.tt`.
- Preserve public generated APIs, nullability, namespaces, schema hashes, line-ending behavior, and target C# compatibility.

## Current DataTables Templates

- `DataTableTemplate.tt` → `DataTableTemplate.cs`
- `DataTableManagerExtensionTemplate.tt` → `DataTableManagerExtensionTemplate.cs`
- `DataMatrixTemplate.tt` → `DataMatrixTemplate.cs`
- `KvTableTemplate.tt` → `KvTableTemplate.cs`
- `GraphTableTemplate.tt` → `GraphTableTemplate.cs`
- `TreeTableTemplate.tt` → `TreeTableTemplate.cs`

## Required Verification

Run:

```powershell
dotnet test tests/DataTables.Tests/DataTables.Tests.csproj --no-restore
dotnet build DataTables.sln --no-restore
git -c core.whitespace=cr-at-eol diff --check -- src/DataTables.GeneratorCore .agents
```

Report T4 preprocessing, generated C# compilation, output-equivalence coverage, tests, build results, and any pre-existing warnings separately.
