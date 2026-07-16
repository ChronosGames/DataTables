<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
## Table of Contents

- [DataTables](#datatables)
  - [文档](#%E6%96%87%E6%A1%A3)
  - [快速开始](#%E5%BF%AB%E9%80%9F%E5%BC%80%E5%A7%8B)
    - [.NET Core 项目](#net-core-%E9%A1%B9%E7%9B%AE)
    - [Unity项目](#unity%E9%A1%B9%E7%9B%AE)
  - [推荐运行时 API](#%E6%8E%A8%E8%8D%90%E8%BF%90%E8%A1%8C%E6%97%B6-api)
    - [独立上下文](#%E7%8B%AC%E7%AB%8B%E4%B8%8A%E4%B8%8B%E6%96%87)
    - [I/O 与解析线程](#io-%E4%B8%8E%E8%A7%A3%E6%9E%90%E7%BA%BF%E7%A8%8B)
  - [代码生成器](#%E4%BB%A3%E7%A0%81%E7%94%9F%E6%88%90%E5%99%A8)
  - [表格式与生成物](#%E8%A1%A8%E6%A0%BC%E5%BC%8F%E4%B8%8E%E7%94%9F%E6%88%90%E7%89%A9)
  - [数据源与缓存边界](#%E6%95%B0%E6%8D%AE%E6%BA%90%E4%B8%8E%E7%BC%93%E5%AD%98%E8%BE%B9%E7%95%8C)
  - [升级与兼容](#%E5%8D%87%E7%BA%A7%E4%B8%8E%E5%85%BC%E5%AE%B9)
  - [开发与验证](#%E5%BC%80%E5%8F%91%E4%B8%8E%E9%AA%8C%E8%AF%81)
  - [License](#license)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

# DataTables

[![GitHub Actions](https://github.com/ChronosGames/DataTables/workflows/Build-Debug/badge.svg)](https://github.com/ChronosGames/DataTables/actions)
[![Releases](https://img.shields.io/github/release/ChronosGames/DataTables.svg)](https://github.com/ChronosGames/DataTables/releases)

DataTables 把 Excel `.xlsx`/`.xlsm` 或 `.csv` 配置生成强类型 C# 代码和配套 `.bytes` 数据，运行时面向 .NET 服务端与 Unity 客户端。项目提供异步数据源读取、并发单飞加载、独立 `DataTableContext`、可选的 LRU 表缓存，以及可组合的数据源装饰器。

生成代码和 `.bytes` 是同一份 schema 的两个组成部分，必须一起生成、一起发布。

## 文档

- [文档中心](docs/README.md)
- [表类型与 Excel 布局](docs/guides/table-types.md)
- [Excel 模板与 Dicts](templates/README.md)
- [数据源管线](docs/guides/data-source-pipeline.md)
- [二进制格式 v3](docs/reference/binary-format-v3.md)
- [v3 迁移指南](docs/migration/migration-to-v3.md)

## 快速开始

### .NET Core 项目

1. 安装运行时和生成器：

```bash
dotnet add package DataTables.API
dotnet tool install --global DataTables.Generator
```

2. 创建 `Tables/Scene.xlsx`（也可使用 `.xlsm` 或单工作表 `.csv`）。以下示例假定工作表的 `Class=Scene`，因此会生成表类 `DTScene` 和默认行前缀对应的行类：

| 行 | A 列 | B 列 |
| --- | --- | --- |
| 1 | `DTGen=Table, Title=Scene, Class=Scene, Index=Id` |  |
| 2 | 场景 ID | 场景名称 |
| 3 | `Id` | `Name` |
| 4 | `int` | `string` |
| 5 | `1` | `Town` |

3. 生成 C# 与数据文件：

```bash
dotnet dtgen -i ./Tables -patterns "*.xlsx" -patterns "*.xlsm" -patterns "*.csv" -co ./Generated -do ./Data -p DR
```

SDK 风格项目会自动编译 `Generated/**/*.cs`。若运行目录不是项目目录，把数据复制到输出目录：

```xml
<ItemGroup>
  <None Update="Data/*.bytes" CopyToOutputDirectory="PreserveNewest" />
  <None Update="Data/manifest.json" CopyToOutputDirectory="PreserveNewest" />
</ItemGroup>
```

4. 配置数据目录并加载：

```csharp
using System;
using System.IO;
using System.Threading.Tasks;
using DataTables;

public static class Program
{
    public static async Task Main()
    {
        using var context = new DataTableContext(
            new FileSystemDataSource(Path.Combine(AppContext.BaseDirectory, "Data")));
        context.EnableEstimatedMemoryBudget(50);

        var scenes = await context.LoadAsync<DTScene>();
        if (scenes is null)
        {
            throw new InvalidOperationException("DTScene was not loaded.");
        }

        Console.WriteLine($"Loaded {scenes.Count} scenes.");
        Console.WriteLine($"Loaded state: {context.IsLoaded<DTScene>()}");

        var cachedScenes = context.GetCached<DTScene>();
        var town = DTScene.GetById(context, 1);
        Console.WriteLine($"Same cached instance: {ReferenceEquals(scenes, cachedScenes)}");
        Console.WriteLine($"Scene: {town?.Name}");
    }
}
```

`EnableEstimatedMemoryBudget(50)` 表示“缓存项估算值合计不超过 50 MiB”，不是进程实际托管内存的硬上限。需要更准确的项目级估算时，传入 `Func<DataTableBase, long>` 大小估算器。
若单张表的估算值已超过整个预算，当前 `LoadAsync` 调用仍会得到该表，但它不会进入缓存；随后 `GetCached` 返回 `null`、`IsLoaded` 返回 `false`，再次加载会重新读取。生产配置应给单表峰值留出空间。

### Unity项目

从 [Releases](https://github.com/ChronosGames/DataTables/releases) 获取 Unity 包，并使用独立 CLI 生成 C#、`.bytes` 与 `manifest.json`。放入 `StreamingAssets` 时三者必须一起部署；Android 与 WebGL 由正式的 `StreamingAssetsDataSource` 使用 `UnityWebRequest` 异步读取，其他平台使用异步文件流。

Unity 生命周期入口可以使用 `async void`，但必须在入口内捕获异常：

```csharp
using System;
using System.Threading;
using DataTables;
using UnityEngine;

public sealed class DataBootstrap : MonoBehaviour
{
    private readonly CancellationTokenSource _lifetime = new();

    private async void Start()
    {
        try
        {
            using var context = new DataTableContext(
                new StreamingAssetsDataSource(Application.streamingAssetsPath));
            context.EnableEstimatedMemoryBudget(30);

            var scenes = await context.LoadAsync<DTScene>(cancellationToken: _lifetime.Token);
            var town = DTScene.GetById(context, 1);
            Debug.Log($"Loaded {scenes?.Count ?? 0} scenes; town={town?.Name}.");
        }
        catch (OperationCanceledException) when (_lifetime.IsCancellationRequested) { }
        catch (Exception exception)
        {
            Debug.LogException(exception);
        }
    }

    private void OnDestroy() => _lifetime.Cancel();
}
```

Unity 构建中也可使用 `new StreamingAssetsDataSource()`，它等价于传入 `Application.streamingAssetsPath`。

`src/DataTables` 是运行时源码的修改源；Unity 镜像位于 `src/DataTables.Unity/Assets/Scripts/DataTables/Runtime`，由构建同步，不应直接编辑。

## 推荐运行时 API

新代码围绕三组查询 API 编写：

| 目的 | API | 行为 |
| --- | --- | --- |
| 加载 | `await LoadAsync<T>(name, cancellationToken)` | 未加载时读取并解析；同一表的并发请求共享一次加载。 |
| 读取缓存 | `GetCached<T>(name)` | 只读取已加载实例，不触发 I/O。 |
| 检查状态 | `IsLoaded<T>(name)` | 检查指定类型与名称的表是否已发布。 |

它们同时存在于默认静态门面 `DataTableManager` 和独立的 `DataTableContext`。`name` 默认为空字符串；拆分表或同类型多实例必须在三处使用相同名称：

```csharp
var shard = await DataTableManager.LoadAsync<DTScene>("x001", cancellationToken);
var cachedShard = DataTableManager.GetCached<DTScene>("x001");
var shardIsLoaded = DataTableManager.IsLoaded<DTScene>("x001");
```

生成的强类型静态查询全部是 context-first，不会隐式访问默认全局 Manager：

```csharp
using var context = new DataTableContext(new FileSystemDataSource("./Data"));
var scene = DTScene.GetById(context, 1001);
var shardScene = DTScene.GetById(context, "x001", 1001);
```

`table` / `matrix` / `kv` / `graph` / `tree` 都遵循同一规则；实例查询 API 仍保留。升级生成器后必须重新生成 C#，旧的无 context 查询、静态查询属性和 `*Static` 别名不再生成。

旧的回调加载、`CreateDataTable*`、`GetDataTable*`、`HasDataTable*`、`EnableMemoryManagement` 等兼容别名已在破坏性清理中移除；代码应直接使用 `LoadAsync` / `GetCached` / `IsLoaded` / `EnableEstimatedMemoryBudget`。

### 独立上下文

多租户、测试隔离或同进程加载多套配置时使用独立上下文：

```csharp
using var context = new DataTableContext(new FileSystemDataSource("./TenantA/Data"));
context.EnableEstimatedMemoryBudget(50);

var scenes = await context.LoadAsync<DTScene>();
var cachedScenes = context.GetCached<DTScene>();
var isLoaded = context.IsLoaded<DTScene>();
```

每个上下文独立持有数据源、加载任务、缓存、取消生命周期、统计和 Hook。单次 `LoadAsync` 的调用方 token 只取消该调用方的等待，不会中断其他调用方共享的加载；切换数据源、`ClearCache` 或释放上下文才会取消该上下文仍在进行的底层读取。

生成的注册清单可直接做有界预热。默认并发度为 4；fail-fast 只停止领取新表，已经启动的加载仍会完成并写入逐表结果：

```csharp
using var context = new DataTableContext(new FileSystemDataSource("./Data"));
DataTableManagerExtension.Register(context);
var preheat = await context.PreheatAsync(
    Priority.Critical | Priority.Normal,
    new PreheatOptions(maxConcurrency: 4, failFast: true),
    cancellationToken);

foreach (var table in preheat.Tables)
{
    Console.WriteLine($"{table.TableType.Name}/{table.Name}: {table.Status}");
}
```

`PreheatResult` 包含汇总 `Stats`、稳定排序的逐表状态和 `StopReason`。状态为 `CacheHit`、`Loaded`、`Failed`、`Canceled` 或 `NotStarted`；运行中调用方取消返回部分结果，调用前已经取消仍直接抛出 `OperationCanceledException`。

### I/O 与解析线程

payload I/O 始终异步读取并完整缓冲。同步二进制解析默认使用 `DataTableParseExecution.BackgroundThread`；不支持后台线程的 Unity WebGL Player 会自动在调用上下文解析。`CallingContext` 表示在 I/O 后的 async continuation 上解析：Unity 捕获主线程同步上下文时会回到主线程，没有同步上下文时不承诺固定线程。若需要显式采用该策略，可设置：

```csharp
DataTableManager.ParseExecution = DataTableParseExecution.CallingContext;
```

大表在调用上下文解析可能产生可见卡顿，应在目标平台实测启动和热更新流程。
使用后台解析时，行的 `Deserialize` 和自定义类型转换必须是纯数据、线程安全代码，不能访问 Unity 对象；确实依赖 Unity 主线程的自定义解析应选择 `CallingContext`。

## 代码生成器

查看真实命令契约：

```bash
dotnet dtgen --help
dotnet dtgen data --help
dotnet dtgen validate --help
```

完整生成示例：

```bash
dotnet dtgen \
  -i ./Tables \
  -patterns "*.xlsx" -patterns "*.xlsm" -patterns "*.csv" \
  -co ./Generated \
  -do ./Data \
  -n MyGame \
  -p DR \
  -t "CLIENT && !SERVER" \
  -f
```

解析与诊断示例：

```bash
dotnet dtgen \
  -i ./Tables \
  -patterns "*.xlsx" -patterns "*.xlsm" -patterns "*.csv" \
  -co ./Generated \
  -do ./Data \
  --formula-policy ValidateOnly \
  --column-comment-marker-text "#列注释标志" \
  --row-comment-marker-text "#行注释标志" \
  --skip-cell-marker "#" \
  --array-nested-separators "|#-" \
  --diagnostics-json-output ./artifacts/diagnostics.json
```

`--strict-name-validation` 与 `--validate-formula-consistency` 默认已启用；通常无需传入。它们是 bool 开关，命令中不要在开关后追加 `true`。

只重新生成数据文件时使用 `data` 子命令：

```bash
dotnet dtgen data -i ./Tables -patterns "*.xlsx" -patterns "*.xlsm" -patterns "*.csv" -do ./Data -p DR
```

CI 或预提交只校验 Excel/schema、诊断与代码模板可渲染性，不写入 C#、`.bytes` 或 manifest 时，使用 `validate` 子命令：

```bash
dotnet dtgen validate \
  -i ./Tables \
  -patterns "*.xlsx" -patterns "*.xlsm" -patterns "*.csv" \
  -n MyGame \
  -p DR \
  --diagnostics-json-output ./artifacts/diagnostics.json
```

所有长参数使用 kebab-case。CLI 文档示例由测试与实际 `--help` 输出保持一致。

## 表格式与生成物

Excel/CSV 表的前四行依次是 sheet 元信息、列描述、C# 字段名和字段类型，数据从第五行开始。当前支持 `table`、`matrix`、`column`、`kv`、`tree` 和 `graph`；具体布局、索引声明、标签过滤与类型语法见[表类型指南](docs/guides/table-types.md)。A1 可用 `tags=S&C` 声明工作表/子逻辑表标签，`-t` 是布尔表达式（例如 `C && !S`）；未声明标签的表始终包含，`disabletagsfilter` 会同时关闭表级和字段级过滤。

生成结果包含：

- 表类与行类 C# 文件；
- `DataTableManagerExtension.cs` 注册清单；
- 带版本、表标识与 schema hash 的 `.bytes` 文件；
- 运行时使用的 `manifest.json`（CodeAndData/DataOnly 模式）；
- 仅供生成器使用的增量 manifest v2，其输入和输出均为稳定相对标识。

部署时把 `manifest.json` 与同目录 `.bytes` 一起复制。`validate` 模式不会写任何产物；生成失败或事务提交冲突会保留上一版数据与 manifest。

运行时会校验二进制版本、表标识、schema hash、flags 和尾随字节。出现版本或 schema 不匹配时，应从同一输入重新生成代码和数据，不要单独替换其中一项。协议细节见[二进制格式 v3](docs/reference/binary-format-v3.md)。

## 数据源与缓存边界

`UseFileSystem` 适合本地文件，并优先读取同目录 `manifest.json`，缺失时才回退到目录枚举。`NetworkDataSource` 每次请求配置的 manifest URI，不永久缓存，也不会自动校验下载流；网络、Unity StreamingAssets、版本目录、压缩、加密、hash、原始字节缓存和 fallback 的正确组合顺序见[数据源管线](docs/guides/data-source-pipeline.md)。需要特别区分两种预算：

- `EnableEstimatedMemoryBudget` 管理已解析数据表的 LRU 估算预算；
- `CachedDataSource` 管理完整 payload `byte[]` 的独立、按字节计数的有界 LRU 缓存。

二者不是同一份缓存，也都不代表进程总内存上限。

## 升级与兼容

本次 context-first 查询与 `PreheatResult` 是源码级破坏性变更，升级后必须重新生成并重新编译生成代码；运行时 `DataTableManager` facade 和生成的无参 `Register()` 仍保留。二进制协议仍是 v3，数据格式没有改变，但部署需要新增 `manifest.json`。完整步骤和错误说明见 [v3 迁移指南](docs/migration/migration-to-v3.md)。

## 开发与验证

```bash
dotnet build DataTables.sln
dotnet test tests/DataTables.Tests/DataTables.Tests.csproj
```

修改 `src/DataTables` 后，构建会同步 Unity 运行时镜像。提交前还应运行仓库内的 Unity 同步校验脚本并确认镜像没有漂移。

## License

[MIT](LICENSE)
