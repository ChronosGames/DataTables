# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

DataTables is a data table solution for .NET Core servers and Unity clients. It provides tools to convert Excel data tables into C# code and binary data files, offering fast binary serialization and type-safe data access with indexing and querying capabilities.

## Key Architecture

### Core Components

- **DataTableManager**: Static central manager for all data tables, handles loading, caching, and lifecycle
- **DataTableBase**: Base class for all generated data table classes
- **IDataTableHelper**: Interface for custom data loading strategies (file system, network, etc.)
- **DataTableGenerator**: Code generator that converts Excel files to C# classes and binary data
- **DTXXX Classes**: Generated data table classes with static methods for direct data access

### Generator Architecture

- **DataTables.GeneratorCore**: Core generation logic, Excel parsing, and code templates
- **DataTables.Generator**: CLI tool wrapper using ConsoleAppFramework
- **DataTables.MSBuild.Tasks**: MSBuild integration for automatic code generation

### Data Flow

1. Excel files → DataTableGenerator → C# classes + binary data files
2. Runtime: DataTableManager + IDataTableHelper → Load binary data → Type-safe queries

## Common Development Commands

### Building the Solution
```bash
dotnet build DataTables.sln
```

### Running Tests
```bash
dotnet test tests/DataTables.Tests/DataTables.Tests.csproj
```

### Code Generation (CLI Tool)
```bash
# Install global tool
dotnet tool install --global DataTables.Generator

# Generate from Excel files
dotnet dtgen -i "input/directory" -co "code/output" -do "data/output" -n "YourNamespace" -p "DR"
```

### MSBuild Integration
Add to your .csproj:
```xml
<PackageReference Include="DataTables.MSBuild.Tasks" Version="x.x.x" PrivateAssets="All" />

<Target Name="DataTablesGen" BeforeTargets="BeforeBuild">
    <DataTablesGenerator UsingNamespace="$(ProjectName)" 
                        InputDirectory="$(ProjectDir)" 
                        CodeOutputDirectory="$(ProjectDir)Tables" 
                        DataOutputDirectory="$(ProjectDir)Datas" 
                        PrefixClassName="DR" />
</Target>
```

### Unity Development
- Runtime package: `src/DataTables.Unity/Assets/Scripts/DataTables/`
- Use the standalone CLI generator for code generation
- Generated files go into Unity project's Scripts folder

## Development Patterns

### Data Table Format
Excel sheets must follow specific format:
- Row 1: Header (DTGen=Table/Matrix, Title, Class, Index, Group settings)
- Row 2: Column descriptions (support #comments, @tags)
- Row 3: Field names (C# identifiers)
- Row 4: Field types (int, string, Array<T>, Enum<T>, Map<K,V>, JSON, Custom)

### Generated Code Usage (Static API)
```csharp
// Set up data helper (static)
DataTableManager.SetDataTableHelper(new DefaultDataTableHelper("dataDirectory"));

// Load all tables (static)
DataTableManagerExtension.Preload(() => Console.WriteLine("All loaded"));

// Direct data access through static methods - PURE STATIC API
var scene = DTScene.GetDataRowById(2000);  // Old naming convention
var scenes = DTScene.GetDataRowsGroupByType(SceneType.Battle);

// 注意：静态方法使用小驼峰形式的参数名
// 例如：GetDataRowById(int id) 而不是 GetDataRowById(int Id)

// Alternative: Get table instance if needed for complex operations
var dtScene = DataTableManager.GetDataTable<DTScene>();
if (dtScene != null)
{
    Console.WriteLine($"Scene table has {dtScene.Count} rows");
}

// Manual table loading (static)
DataTableManager.CreateDataTable<DTScene>(() => Console.WriteLine("DTScene loaded"));
```

### Custom Data Helpers
Implement IDataTableHelper for custom loading (networking, encryption, etc.):
```csharp
public class CustomDataTableHelper : IDataTableHelper
{
    public void Read(string fileName, Action<byte[]> onLoaded) { /* custom logic */ }
}
```

## Project Structure Notes

- **src/DataTables**: Core runtime library (NuGet: DataTables.API)
- **src/DataTables.GeneratorCore**: Generation logic with NPOI Excel parsing
- **src/DataTables.Generator**: CLI tool (NuGet: DataTables.Generator)
- **src/DataTables.Unity**: Unity package (copied from core library)
- **tests/DataTables.Tests**: Unit tests using xUnit and FluentAssertions
- **sandbox/**: Example projects (ConsoleApp, Benchmark)

## Code Generation Details

- Supports both Table (row-based) and Matrix (2D grid) formats
- Template-based code generation using T4 templates
- Parallel Excel processing for performance
- Binary serialization using BinaryReader/Writer
- Automatic Index/Group generation for fast queries

## Testing and Quality

- Run tests before commits: `dotnet test`
- Benchmark project available in sandbox/Benchmark
- Unity tests in src/DataTables.Unity/Assets/Tests/

## Package Management

- Core library: `DataTables.API` on NuGet
- CLI tool: `DataTables.Generator` on NuGet  
- Unity: UPM via git URL or manual .unitypackage

## Hook机制

### Hook系统
DataTableManager 支持在数据表加载完成后执行自定义逻辑：

```csharp
// 注册特定类型的数据表Hook
DataTableManager.HookDataTableLoaded<DTScene>(table =>
{
    Console.WriteLine($"DTScene已加载，包含 {table.Count} 行数据");
    // 自定义后处理逻辑
});

// 注册全局Hook
DataTableManager.HookGlobalDataTableLoaded(table =>
{
    Console.WriteLine($"数据表 {table.GetType().Name} 已加载");
});

// 清除所有Hook
DataTableManager.HookClear();
```

## Important Notes

- All generated classes inherit from DataTableBase
- Binary data format is custom and optimized for fast loading
- Supports async/callback-based loading patterns
- Thread-safe DataTableManager with ConcurrentDictionary storage
- Excel files must be .xlsx format (Excel 2007+)
- 生成的代码仅包含静态方法，无实例方法
- 使用旧命名规范：GetDataRowById, GetDataRowsGroupByName