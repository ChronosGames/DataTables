# DataTables binary format v3

DataTables v3 bytes files use a structured header followed by the table payload. The runtime intentionally rejects any non-v3 payload so generated C# code and `.bytes` data cannot drift silently.

## Header fields

1. `Signature` (`string`): must be `DTABLE`.
2. `FormatVersion` (`int32`): must be `3`.
3. `SchemaHash` (`uint64`): hash of generated table schema used to detect stale code or stale data.
4. `GeneratorVersion` (`string`): informational version of the generator that exported the file.
5. `TableFullName` (`string`): generated table type full name expected by runtime loading.
6. `RowCount` (`uint16`): number of serialized rows.
7. `Flags` (`int32`): reserved extension flags. Phase 2 uses the transport-layer model, so decoded DataTables payloads must currently set this to `0`.

`RowCount` is a hard protocol boundary: exports above `65,535` rows fail instead of truncating the count. Runtime and GeneratorCore compile the same internal header codec source; runtime consumers can inspect or produce headers through `DataTableBinaryProtocol`.

Generator exports are transactional. Code, data, manager registration and an optional diagnostics report are written to staging paths and committed together only when the whole batch succeeds. A failed workbook leaves the previous output set untouched.

## Forced migration policy

There is no v2 compatibility reader in the v3 runtime. After upgrading the runtime or generator, regenerate generated code and all `.bytes` files together.

If the runtime sees an unsupported version, table-name mismatch, schema-hash mismatch, or non-zero flags, treat it as a stale artifact problem first and run a full export.
