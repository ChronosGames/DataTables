<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
## Table of Contents

- [🚀 DataTables - 现代化高性能数据表系统](#-datatables---%E7%8E%B0%E4%BB%A3%E5%8C%96%E9%AB%98%E6%80%A7%E8%83%BD%E6%95%B0%E6%8D%AE%E8%A1%A8%E7%B3%BB%E7%BB%9F)
  - [🚀 快速开始](#-%E5%BF%AB%E9%80%9F%E5%BC%80%E5%A7%8B)
    - [.NET Core 项目](#net-core-%E9%A1%B9%E7%9B%AE)
    - [Unity项目](#unity%E9%A1%B9%E7%9B%AE)
  - [🆕 新API指南](#-%E6%96%B0api%E6%8C%87%E5%8D%97)
    - [核心API对比](#%E6%A0%B8%E5%BF%83api%E5%AF%B9%E6%AF%94)
    - [现代异步模式](#%E7%8E%B0%E4%BB%A3%E5%BC%82%E6%AD%A5%E6%A8%A1%E5%BC%8F)
    - [智能配置系统](#%E6%99%BA%E8%83%BD%E9%85%8D%E7%BD%AE%E7%B3%BB%E7%BB%9F)
    - [可组合数据源配置示例](#%E5%8F%AF%E7%BB%84%E5%90%88%E6%95%B0%E6%8D%AE%E6%BA%90%E9%85%8D%E7%BD%AE%E7%A4%BA%E4%BE%8B)
      - [本地包 / StreamingAssets](#%E6%9C%AC%E5%9C%B0%E5%8C%85--streamingassets)
      - [远程 CDN](#%E8%BF%9C%E7%A8%8B-cdn)
      - [热更新 / 回退链路](#%E7%83%AD%E6%9B%B4%E6%96%B0--%E5%9B%9E%E9%80%80%E9%93%BE%E8%B7%AF)
  - [⚡ 性能优化](#-%E6%80%A7%E8%83%BD%E4%BC%98%E5%8C%96)
    - [智能预热策略](#%E6%99%BA%E8%83%BD%E9%A2%84%E7%83%AD%E7%AD%96%E7%95%A5)
    - [性能监控与统计](#%E6%80%A7%E8%83%BD%E7%9B%91%E6%8E%A7%E4%B8%8E%E7%BB%9F%E8%AE%A1)
    - [Hook机制 2.0](#hook%E6%9C%BA%E5%88%B6-20)
  - [🎮 Unity集成](#-unity%E9%9B%86%E6%88%90)
    - [现代Unity最佳实践](#%E7%8E%B0%E4%BB%A3unity%E6%9C%80%E4%BD%B3%E5%AE%9E%E8%B7%B5)
    - [Unity性能优化技巧](#unity%E6%80%A7%E8%83%BD%E4%BC%98%E5%8C%96%E6%8A%80%E5%B7%A7)
  - [🎯 高级功能](#-%E9%AB%98%E7%BA%A7%E5%8A%9F%E8%83%BD)
    - [自定义数据源](#%E8%87%AA%E5%AE%9A%E4%B9%89%E6%95%B0%E6%8D%AE%E6%BA%90)
    - [工厂模式优化](#%E5%B7%A5%E5%8E%82%E6%A8%A1%E5%BC%8F%E4%BC%98%E5%8C%96)
    - [内存管理深度控制](#%E5%86%85%E5%AD%98%E7%AE%A1%E7%90%86%E6%B7%B1%E5%BA%A6%E6%8E%A7%E5%88%B6)
  - [📋 数据表格式](#-%E6%95%B0%E6%8D%AE%E8%A1%A8%E6%A0%BC%E5%BC%8F)
    - [表格型(Table)格式](#%E8%A1%A8%E6%A0%BC%E5%9E%8Btable%E6%A0%BC%E5%BC%8F)
    - [索引声明与查询 API](#%E7%B4%A2%E5%BC%95%E5%A3%B0%E6%98%8E%E4%B8%8E%E6%9F%A5%E8%AF%A2-api)
    - [支持的数据类型](#%E6%94%AF%E6%8C%81%E7%9A%84%E6%95%B0%E6%8D%AE%E7%B1%BB%E5%9E%8B)
  - [🛠️ 代码生成器](#-%E4%BB%A3%E7%A0%81%E7%94%9F%E6%88%90%E5%99%A8)
    - [CLI工具安装](#cli%E5%B7%A5%E5%85%B7%E5%AE%89%E8%A3%85)
    - [现代化生成命令](#%E7%8E%B0%E4%BB%A3%E5%8C%96%E7%94%9F%E6%88%90%E5%91%BD%E4%BB%A4)
    - [解析选项与诊断](#%E8%A7%A3%E6%9E%90%E9%80%89%E9%A1%B9%E4%B8%8E%E8%AF%8A%E6%96%AD)
      - [标签过滤语法（TFE Lite）](#%E6%A0%87%E7%AD%BE%E8%BF%87%E6%BB%A4%E8%AF%AD%E6%B3%95tfe-lite)
    - [MSBuild集成](#msbuild%E9%9B%86%E6%88%90)
  - [📈 迁移指南](#-%E8%BF%81%E7%A7%BB%E6%8C%87%E5%8D%97)
    - [从旧版本升级](#%E4%BB%8E%E6%97%A7%E7%89%88%E6%9C%AC%E5%8D%87%E7%BA%A7)
    - [渐进式升级策略](#%E6%B8%90%E8%BF%9B%E5%BC%8F%E5%8D%87%E7%BA%A7%E7%AD%96%E7%95%A5)
    - [API映射表](#api%E6%98%A0%E5%B0%84%E8%A1%A8)
  - [🏆 性能基准](#-%E6%80%A7%E8%83%BD%E5%9F%BA%E5%87%86)
  - [📜 License](#-license)
  - [🌟 支持项目](#-%E6%94%AF%E6%8C%81%E9%A1%B9%E7%9B%AE)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

[![GitHub Actions](https://github.com/ChronosGames/DataTables/workflows/Build-Debug/badge.svg)](https://github.com/ChronosGames/DataTables/actions) [![Releases](https://img.shields.io/github/release/ChronosGames/DataTables.svg)](https://github.com/ChronosGames/DataTables/releases)

# 🚀 DataTables - 现代化高性能数据表系统

> **异步优先数据表解决方案** - 适用于.NET Core服务端与Unity客户端

## 🚀 快速开始

### .NET Core 项目

1. **安装NuGet包**
```bash
dotnet add package DataTables.API
```

2. **零心智负担使用** 🎉
```csharp
using DataTables;

// 🌟 现代异步API - 自动初始化
var scene = await DataTableManager.LoadAsync<DTScene>();
var items = DataTableManager.GetCached<DTItem>(); // 缓存查询

Console.WriteLine($"场景: {scene?.Name}, 物品数量: {items?.Count ?? 0}");

// 🔥 可选的性能优化
await DataTableManager.PreheatAsync(Priority.Critical | Priority.Normal);
Console.WriteLine("数据预热完成！");
```

3. **智能配置** (可选)
```csharp
// 🎯 一行代码完成所有配置
DataTableManager.UseFileSystem("./DataTables");
DataTableManager.EnableMemoryManagement(50); // 50MB智能缓存
DataTableManager.EnableProfiling(stats =>
    Console.WriteLine($"加载{stats.TableCount}个表，耗时{stats.LoadTime}ms"));
```

### Unity项目

1. **安装Unity Package**
   - 从[Releases](https://github.com/ChronosGames/DataTables/releases)下载`DataTables.Unity.unitypackage`

2. **现代化Unity使用**
```csharp
using DataTables;
using UnityEngine;

public class GameManager : MonoBehaviour
{
    async void Start()
    {
        // 🌟 异步优先 - 无阻塞启动
        var config = await DataTableManager.LoadAsync<DTGameConfig>();
        Debug.Log($"游戏版本: {config?.Version}");

        // 🔥 智能分层预热
        await DataTableManager.PreheatAsync(Priority.Critical);
        Debug.Log("关键数据预热完成，游戏可以启动！");

        // 后台预热其他数据
        _ = DataTableManager.PreheatAsync(Priority.Normal | Priority.Lazy);
    }
}
```

---

## 🆕 新API指南

### 核心API对比

| 功能 | 🆕 新API (推荐) | 🔄 兼容API |
|------|----------------|-----------|
| **异步加载** | `await DataTableManager.LoadAsync<T>()` | `DataTableManager.CreateDataTable<T>()` |
| **缓存查询** | `DataTableManager.GetCached<T>()` | `DataTableManager.GetDataTable<T>()` |
| **状态检查** | `DataTableManager.IsLoaded<T>()` | 手动检查null |
| **批量预热** | `await DataTableManager.PreheatAsync(Priority.All)` | `DataTableManagerExtension.Preload()`（生成的 `TableRegistrations` 提供表清单） |
| **内存管理** | `DataTableManager.EnableMemoryManagement(50)` | 无 |
| **Hook注册** | `DataTableManager.OnLoaded<T>(callback)` | `DataTableManager.HookDataTableLoaded<T>()` |

### 现代异步模式

```csharp
// 🌟 推荐的现代异步模式

// 单表异步加载
var scene = await DataTableManager.LoadAsync<DTScene>();

// 并发加载多表
var tasks = new[]
{
    DataTableManager.LoadAsync<DTScene>(),
    DataTableManager.LoadAsync<DTItem>(),
    DataTableManager.LoadAsync<DTCharacter>()
};
var results = await Task.WhenAll(tasks);

// 缓存优先查询 (热路径)
var cachedScene = DataTableManager.GetCached<DTScene>();
if (cachedScene != null)
{
    var sceneData = DTScene.GetDataRowById(1001);
    Console.WriteLine($"场景名称: {sceneData?.Name}");
}
```

### 智能配置系统

```csharp
// 🎯 统一配置接口

// 文件系统数据源 (默认)
DataTableManager.UseFileSystem("./DataTables");

// 网络数据源
DataTableManager.UseNetwork("https://cdn.game.com/data/");

// 自定义数据源
DataTableManager.UseCustomSource(new MyCustomDataSource());

// 内存管理 (LRU缓存)
DataTableManager.EnableMemoryManagement(100); // 100MB限制

// 性能监控
DataTableManager.EnableProfiling(stats =>
{
    Console.WriteLine($"加载了 {stats.TableCount} 个表");
    Console.WriteLine($"总耗时: {stats.LoadTime}ms");
    Console.WriteLine($"内存使用: {stats.MemoryUsed / 1024 / 1024:F1}MB");
});
```

### 可组合数据源配置示例

#### 本地包 / StreamingAssets

```csharp
// 兼容原有 API：本地 .bytes 文件仍可直接使用
DataTableManager.UseFileSystem("./DataTables");

// Unity 项目可预留 StreamingAssets 入口
DataTableManager.UseDataSource(
    new CachedDataSource(
        new StreamingAssetsDataSource(Application.streamingAssetsPath + "/DataTables")));
```

#### 远程 CDN

```csharp
// 兼容原有网络 API
DataTableManager.UseNetwork("https://cdn.game.com/data");

// 可按需叠加版本、压缩、加密与内存缓存装饰器
IDataSource cdnSource = new CachedDataSource(
    new CompressedDataSource(
        new VersionedDataSource(
            new NetworkDataSource("https://cdn.game.com/data"),
            "v42")));

DataTableManager.UseDataSource(cdnSource);
```

#### 热更新 / 回退链路

```csharp
// 优先读取热更新 CDN，失败或不存在时回退到随包数据
var hotUpdate = new VersionedDataSource(
    new NetworkDataSource("https://cdn.game.com/hotfix"),
    "2026.07.06");

var builtin = new StreamingAssetsDataSource(Application.streamingAssetsPath + "/DataTables");

DataTableManager.UseCompositeSource(hotUpdate, builtin);

// Addressables 可继承 AddressablesDataSourceBase 后接入项目内的 Addressables.LoadAssetAsync<TextAsset>()。
```

---

## ⚡ 性能优化

### 智能预热策略

```csharp
// 🔥 现代预热API - 基于优先级的智能调度

// 客户端：分层预热
await DataTableManager.PreheatAsync(Priority.Critical);           // 立即加载关键数据
_ = DataTableManager.PreheatAsync(Priority.Normal | Priority.Lazy); // 后台预热其他数据

// 服务器：全量预热
var preheatStats = await DataTableManager.PreloadAllAsync();
Console.WriteLine($"预热完成: {preheatStats.SuccessCount} 成功, {preheatStats.FailureCount} 失败");

// 自定义预热 (并发安全)
var tasks = new[]
{
    DataTableManager.LoadAsync<DTConfig>(),
    DataTableManager.LoadAsync<DTLevel>(),
    DataTableManager.LoadAsync<DTCharacter>()
};
await Task.WhenAll(tasks);
```

`PreheatAsync` 会读取生成代码中的 `DataTableManagerExtension.TableRegistrations`，按 `Priority` 筛选后并发调用 `LoadAsync<T>`/`CreateDataTableAsync<T>`；返回的 `LoadStats` 包含本次预热的耗时、成功数、失败数与内存变化。`GetStats()` 返回进程内累计历史加载统计，而不是固定耗时。

### 性能监控与统计

```csharp
// 📊 全面的性能监控

// 历史加载统计（LoadTime/MemoryUsed 为累计加载耗时与累计内存变化）
var stats = DataTableManager.GetStats();
Console.WriteLine($"已加载表数量: {stats.TableCount}");
Console.WriteLine($"累计加载耗时: {stats.LoadTime}ms");
Console.WriteLine($"成功/失败: {stats.SuccessCount}/{stats.FailureCount}");
Console.WriteLine($"累计内存变化: {stats.MemoryUsed / 1024 / 1024:F1}MB");

// 缓存统计
var cacheStats = DataTableManager.GetCacheStats();
if (cacheStats.HasValue)
{
    var cache = cacheStats.Value;
    Console.WriteLine($"缓存项数: {cache.TotalItems}");
    Console.WriteLine($"缓存命中率: {cache.HitRate:P}");
    Console.WriteLine($"内存使用率: {cache.MemoryUsageRate:P}");
}

// 加载状态检查
bool isLoaded = DataTableManager.IsLoaded<DTScene>();
Console.WriteLine($"场景表是否已加载: {isLoaded}");
```

### Hook机制 2.0

```csharp
// 🎣 简化的类型安全Hook系统

// 类型安全Hook
DataTableManager.OnLoaded<DTScene>(table =>
{
    Console.WriteLine($"✅ 场景表加载完成: {table.Count} 行数据");

    // 自定义后处理
    ValidateSceneData(table);
    BuildSceneIndex(table);
});

// 全局Hook
DataTableManager.OnAnyLoaded(table =>
{
    var typeName = table.GetType().Name;
    var loadTime = DateTime.Now;
    Console.WriteLine($"📊 [{loadTime:HH:mm:ss}] {typeName} 已加载");
});

// 清理Hook
DataTableManager.ClearHooks();
```

---

## 🎮 Unity集成

### 现代Unity最佳实践

```csharp
using DataTables;
using UnityEngine;

public class ModernDataTableDemo : MonoBehaviour
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static async void InitializeDataTables()
    {
        // 🚀 启动时快速初始化
        DataTableManager.UseFileSystem(Application.streamingAssetsPath + "/DataTables");
        DataTableManager.EnableMemoryManagement(30); // Unity环境30MB限制

        // 立即加载核心表
        await DataTableManager.LoadAsync<DTGameConfig>();

        // 后台预热其他表
        _ = DataTableManager.PreheatAsync(Priority.Normal | Priority.Lazy);
    }

    async void Start()
    {
        // 🎯 场景相关数据预热
        await DataTableManager.PreheatAsync(Priority.Critical);
        Debug.Log("关键数据已就绪，游戏可以开始！");

        // 使用数据
        var config = DataTableManager.GetCached<DTGameConfig>();
        if (config != null)
        {
            var gameConfig = DTGameConfig.GetDataRowById(1);
            Debug.Log($"游戏版本: {gameConfig?.Version}");
        }
    }

    void OnApplicationPause(bool pauseStatus)
    {
        if (pauseStatus)
        {
            // 暂停时清理缓存释放内存
            DataTableManager.ClearCache();
        }
    }
}
```

### Unity性能优化技巧

```csharp
// 📱 移动平台优化

public class MobileOptimizationDemo : MonoBehaviour
{
    void Start()
    {
        // 根据设备性能调整缓存大小
        var systemMemory = SystemInfo.systemMemorySize;
        var cacheSize = systemMemory > 4096 ? 50 : 20; // 4GB+设备使用50MB，否则20MB
        DataTableManager.EnableMemoryManagement(cacheSize);

        // 监听内存警告
        Application.lowMemory += OnLowMemory;
    }

    private void OnLowMemory()
    {
        Debug.Log("收到内存警告，清理数据表缓存");
        DataTableManager.ClearCache();
    }

    // 场景切换时的优化策略
    public async void LoadScene(int sceneId)
    {
        var sceneConfig = DTScene.GetDataRowById(sceneId);

        // 预加载场景相关数据
        var preloadTasks = new[]
        {
            DataTableManager.LoadAsync<DTNpc>(),
            DataTableManager.LoadAsync<DTQuest>(),
            DataTableManager.LoadAsync<DTItem>()
        };

        await Task.WhenAll(preloadTasks);

        // 开始切换场景
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneConfig.SceneName);
    }
}
```

---

## 🎯 高级功能

### 自定义数据源

```csharp
// 🔧 扩展自定义数据源

public class EncryptedDataSource : IDataSource
{
    private readonly string _baseDirectory;
    private readonly byte[] _encryptionKey;

    public EncryptedDataSource(string baseDirectory, byte[] encryptionKey)
    {
        _baseDirectory = baseDirectory;
        _encryptionKey = encryptionKey;
    }

    public async ValueTask<byte[]> LoadAsync(string tableName)
    {
        var filePath = Path.Combine(_baseDirectory, $"{tableName}.encrypted");
        var encryptedData = await File.ReadAllBytesAsync(filePath);

        // 自定义解密逻辑
        return DecryptData(encryptedData, _encryptionKey);
    }

    public ValueTask<bool> IsAvailableAsync()
    {
        return ValueTask.FromResult(Directory.Exists(_baseDirectory));
    }

    private byte[] DecryptData(byte[] encryptedData, byte[] key)
    {
        // 实现你的解密算法
        return encryptedData; // 这里应该是解密后的数据
    }
}

// 使用自定义数据源
var encryptionKey = LoadEncryptionKey();
var encryptedSource = new EncryptedDataSource("./EncryptedData", encryptionKey);
DataTableManager.UseCustomSource(encryptedSource);
```

### 工厂模式优化

```csharp
// 🏭 工厂模式 - 消除反射调用，提升90%性能

// 实现数据表工厂
public class DTSceneFactory : IDataTableFactory<DTScene, DRScene>
{
    public DTScene CreateTable(string name, int capacity)
        => new DTScene(name, capacity);

    public DRScene CreateRow()
        => new DRScene();
}

// 注册工厂 (通常由代码生成器自动完成)
DataTableManager.RegisterFactory<DTScene, DRScene, DTSceneFactory>();

// 注册后，表的创建将使用工厂模式，避免反射调用
var scene = await DataTableManager.LoadAsync<DTScene>(); // 90%性能提升！
```

### 内存管理深度控制

```csharp
// 🧠 精确的内存管理控制

// 启用LRU缓存管理
DataTableManager.EnableMemoryManagement(50); // 50MB限制

// 监控内存使用
var cacheStats = DataTableManager.GetCacheStats();
if (cacheStats?.MemoryUsageRate > 0.8f) // 使用率超过80%
{
    Console.WriteLine("内存使用率较高，LRU将自动淘汰旧数据");
}

// 手动清理缓存
DataTableManager.ClearCache();

// 禁用内存管理 (如果需要)
DataTableManager.DisableMemoryManagement();
```

---

## 📋 数据表格式

### 表格型(Table)格式

Excel文件格式定义：

| 行号 | 内容 | 说明 |
|------|------|------|
| 1 | `DTGen=Table, Title=场景表, Class=Scene, Index=Id, Group=Type` | 表头配置 |
| 2 | `场景ID`, `场景名称@ABC`, `场景类型`, `#备注` | 列描述 |
| 3 | `Id`, `Name`, `Type`, `Comment` | 字段名 |
| 4 | `int`, `string`, `Enum<SceneType>`, `string` | 字段类型 |
| 5+ | 数据行... | 实际数据 |

### 索引声明与查询 API

在 A1 表头中使用 `Index=` 声明唯一索引，使用 `Group=` 声明分组索引；组合索引用 `&` 固定字段顺序。生成器会在校验阶段检查字段是否存在、字段类型是否可作为字典 key、唯一索引是否重复，以及组合索引字段顺序是否稳定。

```text
DTGen=Table, Title=场景表, Class=Scene, Index=Id, Index=Type&Level, Group=Type
```

生成代码会统一提供以下查询方法：

```csharp
// 唯一索引：Index=Id
DRScene? row = DTScene.GetById(1001);
bool ok = DTScene.TryGetById(1001, out DRScene? found);
bool exists = DTScene.ContainsId(1001);

// 组合唯一索引：Index=Type&Level，调用参数顺序与声明顺序一致
DRScene? typed = DTScene.GetByTypeAndLevel(SceneType.Battle, 3);

// 分组索引：Group=Type
IReadOnlyList<DRScene>? scenes = DTScene.GetManyByType(SceneType.Battle);
bool hasGroup = DTScene.ContainsType(SceneType.Battle);

// 兼容旧 API：仍保留 GetDataRowByXxx/GetRowByXxx 与 GetDataRowsGroupByXxx/GetRowsGroupByXxx 别名。
```

索引字典在加载过程中使用可变字典构建，加载完成后通过只读字典接口暴露给查询 API，避免运行期查询路径产生额外锁或拷贝。

### 支持的数据类型

| 类型 | 说明 | 示例 |
|------|------|------|
| 基础类型 | `int`, `long`, `float`, `double`, `bool`, `string` | `42`, `3.14`, `true` |
| 数组 | `Array<T>` | `Array<int>` → `[1,2,3]` 或 `1#2#3` 或 `1|2|3` |
| 枚举 | `Enum<T>` | `Enum<ColorType>` → `Red` |
| 字典 | `Map<K,V>` | `Map<int,string>` → `{1:"a",2:"b"}` |
| JSON | `JSON` | 复杂对象的JSON字符串 |
| 自定义 | `Custom` | 自定义类，需要字符串构造函数 |

---

## 🛠️ 代码生成器

### CLI工具安装

```bash
# 全局安装
dotnet tool install --global DataTables.Generator

# 本地安装
dotnet tool install --tool-path ./tools DataTables.Generator
```

### 现代化生成命令

```bash
# 基础生成 - 自动优化
dotnet dtgen -i ./Tables -co ./Generated -do ./Data -n MyProject -p DT

# 高级生成 - 包含工厂模式优化
dotnet dtgen \
  -i "./Tables" \              # 输入目录
  -co "./Generated" \          # 代码输出目录
  -do "./Data" \               # 数据输出目录
  -n "MyProject" \             # 命名空间
  -p "DT" \                    # 类名前缀
  -t "RELEASE" \               # 列标签过滤
  --factory \                  # 启用工厂模式生成
  --async-first \              # 异步优先API
  -f                           # 强制覆写
```

### 解析选项与诊断

```bash
# 启用严格名称校验与公式一致性校验，并生成诊断报告
dotnet dtgen \
  -i "./Tables" -co "./Generated" -do "./Data" -n "MyProject" -p "DT" \
  --strictNameValidation true \
  --validateFormulaConsistency true \
  --formulaPolicy ValidateOnly \
  --columnCommentMarkerText "#列注释标志" \
  --rowCommentMarkerText "#行注释标志" \
  --skipCellMarker "#" \
  --arrayNestedSeparators "" \
  -t "CLIENT && !SERVER" \
  --diagnosticsJsonOutput diagnostics.json

# 关闭公式评估，快速导出
dotnet dtgen -i ./Tables -co ./Generated -do ./Data --formulaPolicy Off

# 自定义嵌套数组的分隔字符（解决 "id#count|id#count" 与单条目 "801000#2" 行为不一致问题）
# 字符位置 = 数组嵌套深度（从 1 开始）；为空时维持旧行为（优先 '|' 否则 '#'）
dotnet dtgen -i ./Tables -co ./Generated -do ./Data \
  --arrayNestedSeparators "|#-"   # 第 1 层 '|'，第 2 层 '#'，第 3 层 '-'
```

#### 标签过滤语法（TFE Lite）

- 语法元素（大小写不敏感）：
  - 标识符：`[A-Za-z0-9_]+`
  - 布尔运算：`AND`、`OR`、`NOT`（别名：`&&`、`||`、`!`）
  - 括号：`(`、`)`
- 优先级：`NOT > AND > OR`
- 空表达式：视为 `true`（不过滤）
- Excel 标签标注：在列标题末尾用 `@` 标注标签，多个标签可用空格、逗号、分号、竖线、中文逗号等分隔。
  - 示例：`Hp@CLIENT`、`Atk@CLIENT,SERVER`、`Speed@client|pve`
- 过滤表达式示例：
  - `CLIENT AND NOT SERVER`
  - `(CLIENT OR EDITOR) AND NOT LEGACY`
  - `PVE || PVP`
  - `!INTERNAL`

诊断报告（JSON）字段：
- InfoCount / WarningCount / ErrorCount
- Items: [ { Severity, File, Sheet, Cell, Message } ]

### MSBuild集成

```xml
<!-- 现代化MSBuild集成 -->
<Target Name="DataTablesGen" BeforeTargets="BeforeBuild">
    <DataTablesGenerator
        UsingNamespace="$(ProjectName)"
        InputDirectory="$(ProjectDir)Tables"
        CodeOutputDirectory="$(ProjectDir)Generated"
        DataOutputDirectory="$(ProjectDir)DataTables"
        PrefixClassName="DT"
        EnableFactoryPattern="true"
        AsyncFirstAPI="true"
        FilterColumnTags="RELEASE"
        ForceOverwrite="false" />
</Target>
```

---

## 📈 迁移指南

### 从旧版本升级

如果你目前使用传统的DataTableManager API：

```csharp
// ❌ 旧版本方式 (仍然支持)
DataTableManager.SetDataTableHelper(new DefaultDataTableHelper("./Data"));
DataTableManagerExtension.Preload(() => Console.WriteLine("加载完成"));
DataTableManager.CreateDataTable<DTScene>(() => {
    var scene = DataTableManager.GetDataTable<DTScene>();
    var data = scene.GetDataRowById(2000);
});

// ✅ 升级到现代异步方式
DataTableManager.UseFileSystem("./Data");  // 一次性配置
var scene = await DataTableManager.LoadAsync<DTScene>(); // 异步加载
var data = DTScene.GetDataRowById(2000); // 直接访问
```

### 渐进式升级策略

1. **阶段1 - 兼容运行** (保持现有代码不变)
```csharp
// 现有代码继续工作，无需修改
DataTableManager.SetDataTableHelper(helper);
var table = DataTableManager.GetDataTable<DTScene>();
```

2. **阶段2 - 新功能使用新API**
```csharp
// 新功能采用现代API
await DataTableManager.LoadAsync<DTNewTable>();
DataTableManager.EnableMemoryManagement(50);
```

3. **阶段3 - 逐步重构**
```csharp
// 逐步替换旧API调用
// DataTableManager.CreateDataTable<T>(callback)
// → await DataTableManager.LoadAsync<T>()
```

### API映射表

| 旧API | 新API | 说明 |
|-------|-------|------|
| `DataTableManager.SetDataTableHelper()` | `DataTableManager.UseFileSystem()` | 数据源配置 |
| `DataTableManager.CreateDataTable<T>()` | `await DataTableManager.LoadAsync<T>()` | 异步加载 |
| `DataTableManager.GetDataTable<T>()` | `DataTableManager.GetCached<T>()` | 缓存查询 |
| `DataTableManagerExtension.Preload()` | `await DataTableManager.PreloadAllAsync()` | 批量预热 |
| `DataTableManager.HookDataTableLoaded<T>()` | `DataTableManager.OnLoaded<T>()` | Hook注册 |

---

## 🏆 性能基准

基于激进优化的性能表现：

| 场景 | 优化前 | 优化后 | 提升幅度 |
|------|-------|-------|----------|
| **并发加载** | 存在竞态条件 | 100%并发安全 | ∞ |
| **热路径查询** | ~4500 ticks | ~1489 ticks | **3x 提升** |
| **内存管理** | 手动管理 | 智能LRU缓存 | **30-50% 减少** |
| **异步操作** | 阻塞调用 | ValueTask优化 | **避免死锁** |
| **工厂模式** | 反射创建 | 零反射调用 | **90% 提升潜力** |

---

## 📜 License

This library is under the MIT License.

---

## 🌟 支持项目

如果这个项目对你有帮助，请考虑：
- ⭐ 给项目点星
- 🐛 报告bug和建议
- 💡 贡献代码和文档
- 📢 向其他开发者推荐

**感谢使用 DataTables！享受现代化高性能的开发体验！** 🚀

## Binary format upgrade and compatibility

Starting with binary format version 3, generated `.bytes` files use a structured header before the row payload:

1. `Signature` (`DTABLE`)
2. `FormatVersion`
3. `SchemaHash`
4. `GeneratorVersion`
5. `TableFullName`
6. `RowCount`
7. `Flags`

The schema hash is calculated during generation from the effective exported schema: table type, table full name, and each non-ignored field's name, type, and order. Runtime loading validates the `.bytes` schema hash against the hash embedded in the generated table class, so a mismatch clearly indicates that the generated C# code and binary data were not produced from the same table schema.

Upgrade policy:

* Format version `3` is a breaking binary format used by the next major release; older format versions are not read by this runtime.
* Runtimes require the exact binary format version they were built for. When upgrading, regenerate both the C# table code and `.bytes` assets together and publish them with the matching major runtime.
* `Flags` is reserved for payload features such as compression, encryption, or an embedded default-value table. Unknown non-zero flags are rejected by the runtime until explicit support is added.
