# Migration to DataTables v3

DataTables v3 is a forced migration. Do not mix old `.bytes` files with newly generated C# code, and do not mix newly exported `.bytes` files with old runtime packages.

## Required steps

1. Upgrade the DataTables runtime and generator package together.
2. Run the generator once for every source workbook/CSV and export both generated C# code and `.bytes` data.
3. Commit the generated code and `.bytes` data in the same change.
4. In CI, run the generator and fail the build if generated outputs differ from the committed files.

## Command examples

CLI projects should run the DataTables generator with the same input and output directories used by the application build.

MSBuild projects should invoke the DataTables MSBuild task during build and verify there are no uncommitted generated changes.

Unity projects should export the package/runtime and regenerate local `DataTables` bytes through the Unity generation workflow before entering play mode or creating a player build.

## Common runtime errors

- `Unsupported data table version`: the `.bytes` file is not v3; regenerate code and bytes together.
- `Data table header mismatch`: the bytes belong to a different generated table type.
- `Data table schema mismatch`: generated code and bytes were not produced from the same schema.
- `Unsupported data table flags`: compression/encryption was not decoded by the data source before runtime parsing.
