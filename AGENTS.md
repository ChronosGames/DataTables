# AGENTS.md

This file provides guidance to Codex (Codex.ai/code) when working with code in this repository.

## Project Overview

DataTables is a **激进优化的现代化高性能数据表系统** for .NET Core servers and Unity clients. It provides tools to convert Excel data tables into C# code and binary data files, offering **异步优先架构、100%并发安全、智能内存管理** with fast binary serialization and type-safe data access.

### 🆕 激进优化特性
- **异步优先**: 纯async/await API设计，ValueTask优化，避免阻塞死锁
- **100%并发安全**: ConcurrentDictionary + Task缓存模式，完全消除竞态条件  
- **智能内存管理**: 内置LRU缓存，自动淘汰机制，30-50%内存优化
- **工厂模式基础**: 零反射调用准备，90%性能提升潜力

## Key Architecture

### Core Components

- **DataTableManager**: 激进优化的异步优先静态管理器，内置LRU缓存、工厂模式、Hook机制
- **LRUDataTableCache**: 智能内存管理，支持可配置限制、命中率统计、自动淘汰
- **IDataTableFactory**: 工厂接口，消除反射调用，提升90%性能
- **IDataSource**: 数据源抽象，支持文件系统、网络、自定义数据源
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
# Run all tests
dotnet test tests/DataTables.Tests/DataTables.Tests.csproj

# Run specific test
dotnet test tests/DataTables.Tests/DataTables.Tests.csproj --filter "FullyQualifiedName~TestMethodName"

# Run tests with verbose output
dotnet test tests/DataTables.Tests/DataTables.Tests.csproj --verbosity normal
```

### Code Generation (CLI Tool)
```bash
# Install global tool
dotnet tool install --global DataTables.Generator

# Generate from Excel files (basic usage)
dotnet dtgen -i "input/directory" -co "code/output" -do "data/output" -n "YourNamespace" -p "DR"

# Generate with column filtering by tags
dotnet dtgen -i "input/directory" -co "code/output" -do "data/output" -n "YourNamespace" -p "DR" -t "ABC"

# Generate with force overwrite and custom namespaces
dotnet dtgen -i "input/directory" -co "code/output" -do "data/output" -n "YourNamespace" -p "DR" -ins "System.Text.Json&Newtonsoft.Json" -f
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

### 🆕 现代化异步优先API
```csharp
// 🌟 新的推荐API - 异步优先，高性能无阻塞
var scene = await DataTableManager.LoadAsync<DTScene>();
var items = DataTableManager.GetCached<DTItem>(); // 缓存优先，零延迟

// 🔥 智能配置 - 一行代码完成所有设置
DataTableManager.UseFileSystem("./DataTables");
DataTableManager.EnableMemoryManagement(50); // 50MB LRU缓存
DataTableManager.EnableProfiling(stats => 
    Console.WriteLine($"加载{stats.TableCount}个表，耗时{stats.LoadTime}ms"));

// 🚀 智能批量预热
await DataTableManager.PreheatAsync(Priority.Critical | Priority.Normal);
await DataTableManager.PreloadAllAsync(); // 服务器全量预热

// 🎯 直接访问 - PURE STATIC API (优化版本)
var sceneData = DTScene.GetDataRowById(2000);  // 使用GetCached优化
var battleScenes = DTScene.GetDataRowsGroupByType(SceneType.Battle);

// 📊 状态检查与监控
bool loaded = DataTableManager.IsLoaded<DTScene>();
var stats = DataTableManager.GetStats();
var cacheStats = DataTableManager.GetCacheStats();

// 🎣 简化Hook机制
DataTableManager.OnLoaded<DTScene>(table => 
    Console.WriteLine($"场景表已加载: {table.Count} 行"));
DataTableManager.OnAnyLoaded(table => 
    Console.WriteLine($"{table.GetType().Name} 已加载"));
```

### 🔄 兼容性API (仍然支持)
```csharp
// 传统API继续工作，无需修改现有代码
DataTableManager.SetDataTableHelper(new DefaultDataTableHelper("dataDirectory"));
DataTableManagerExtension.Preload(() => Console.WriteLine("All loaded"));
var dtScene = DataTableManager.GetDataTable<DTScene>(); // 标记为Obsolete
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

### .NET Testing
- Run tests before commits: `dotnet test`
- Tests use xUnit with FluentAssertions
- Located in: `tests/DataTables.Tests/`

### Benchmarking
- Performance benchmark project: `sandbox/Benchmark/`
- Run benchmarks: `dotnet run --project sandbox/Benchmark/Benchmark.csproj -c Release`

### Unity Testing
- Unity tests in: `src/DataTables.Unity/Assets/Tests/`
- Uses Unity Test Framework

## Package Management

- Core library: `DataTables.API` on NuGet
- CLI tool: `DataTables.Generator` on NuGet  
- Unity: UPM via git URL or manual .unitypackage

## 🆕 现代化Hook机制

### 简化的类型安全Hook系统
激进优化后的DataTableManager提供了更简洁的Hook API：

```csharp
// 🎣 新的简化Hook API
DataTableManager.OnLoaded<DTScene>(table =>
{
    Console.WriteLine($"DTScene已加载，包含 {table.Count} 行数据");
    // 自定义后处理逻辑
});

// 全局Hook
DataTableManager.OnAnyLoaded(table =>
{
    Console.WriteLine($"数据表 {table.GetType().Name} 已加载");
});

// 清除所有Hook
DataTableManager.ClearHooks();

// 🔄 兼容性API (仍然支持，但标记为Obsolete)
DataTableManager.HookDataTableLoaded<DTScene>(table => { /* ... */ });
DataTableManager.HookGlobalDataTableLoaded(table => { /* ... */ });
```

## 🆕 Important Notes (激进优化版)

### 🏗️ 架构特性
- **异步优先**: DataTableManager完全重构为async/await优先架构
- **100%并发安全**: ConcurrentDictionary + Task缓存，彻底消除竞态条件
- **智能内存管理**: 内置LRU缓存，支持自动淘汰和内存限制
- **工厂模式基础**: IDataTableFactory接口，为90%性能提升做准备

### 📊 兼容性与优化
- All generated classes inherit from DataTableBase  
- Binary data format is custom and optimized for fast loading
- **推荐异步模式**: `await DataTableManager.LoadAsync<T>()` 替代传统callback模式
- **缓存优先查询**: `DataTableManager.GetCached<T>()` 替代 `GetDataTable<T>()`
- Thread-safe DataTableManager with ConcurrentDictionary + Task caching
- Excel files must be .xlsx format (Excel 2007+)
- Generated code uses pure static API - no instance methods needed
- Uses legacy naming convention: GetDataRowById, GetDataRowsGroupByName  
- Generated static methods use camelCase parameters (e.g., `id` not `Id`)

## Development Workflow

### Local Development
1. Build solution: `dotnet build DataTables.sln`
2. Run tests: `dotnet test`
3. Test with sample: `dotnet run --project sandbox/ConsoleApp/ConsoleApp.csproj`

### Code Generation Testing
1. Place test Excel files in `sandbox/ConsoleApp/`
2. Generate code: Run DataTables.Generator with appropriate parameters
3. Check generated files in output directories
4. Test runtime loading and querying

### Unity Package Updates
- Core library files are automatically copied to Unity package on build
- Manual Unity package export available via `PackageExporter.cs`