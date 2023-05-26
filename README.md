[![GitHub Actions](https://github.com/PhonixGame/DataTables/workflows/Build-Debug/badge.svg)](https://github.com/PhonixGame/DataTables/actions) [![Releases](https://img.shields.io/github/release/PhonixGame/DataTables.svg)](https://github.com/PhonixGame/DataTables/releases)

DataTables
===

专注于Excel配置表的导出：目前支持.NET Core的服务端与Unity客户端。

<!-- ![image](https://user-images.githubusercontent.com/46207/61031896-61890800-a3fb-11e9-86b7-84c821d347a4.png) -->

<!-- **4700** times faster than SQLite and achieves zero allocation per query. Also the DB size is small. When SQLite is 3560kb then MasterMemory is only 222kb. -->

<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
## Table of Contents

- [Concept](#concept)
- [Getting Started(.NET Core)](#getting-startednet-core)
- [Getting Started(Unity)](#getting-startedunity)
- [DataTable configuration](#datatable-configuration)
- [Optimization](#optimization)
- [Code Generator](#code-generator)
- [License](#license)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

Concept
---

* **Support Many Input File**, Support Excel 2007-365(*.xlsx) as input file.
* **Memory Efficient**, Only use underlying data memory and do aggressively string interning.
* **Performance**, Similar as dictionary lookup.
* **TypeSafe**, 100% Type safe by pre code-generation.
* **Fast load speed**, DataTables Convert Excel files to binary files, so packed files is smaller and load speed is blazing fast.

<!--
These features are suitable for master data management(write-once, read-heavy) on embedded application such as role-playing game. MasterMemory has better performance than any other database solutions. [PalDB](https://github.com/linkedin/PalDB) developed by LinkedIn has a similar concept(embeddable write-once key-value store), but the implementation and performance characteristics are completely different.
-->

Getting Started(.NET Core)
---
DataTables uses C# to C# code-generator. Runtime library API is the same but how to code-generate has different way between .NET Core and Unity. This sample is for .NET Core(for Unity is in below sections).

Install the core library(Runtime and [Annotations](https://www.nuget.org/packages/DataTables.Annotations)).

> PM> Install-Package [DataTables](https://www.nuget.org/packages/DataTables.API)

Prepare the example excel table definition like following.

![ExcelSample](https://user-images.githubusercontent.com/5179057/227073069-1cd264bf-d8ca-4b77-9c71-bce0bce66150.PNG)

Edit the `.csproj`, add [DataTables.MSBuild.Tasks](https://www.nuget.org/packages/DataTables.MSBuild.Tasks) and add configuration like following.

```xml
<ItemGroup>
    <PackageReference Include="DataTables.API" Version="0.2.2" />
    <!-- Install MSBuild Task(with PrivateAssets="All", it means to use dependency only in build time). -->
    <PackageReference Include="DataTables.MSBuild.Tasks" Version="0.2.2" PrivateAssets="All" />
</ItemGroup>

<!-- Call code generator before-build. -->
<Target Name="DataTablesGen" BeforeTargets="BeforeBuild">
    <!-- Configuration of Code-Generator, `UsingNamespace`, `InputDirectory`, `CodeOutputDirectory`, `DataOutputDirectory` and `PrefixClassName`. -->
    <DataTablesGenerator UsingNamespace="$(ProjectName)" InputDirectory="$(ProjectDir)" CodeOutputDirectory="$(ProjectDir)Tables" DataOutputDirectory="$(ProjectDir)Datas" PrefixClassName="DR" />
</Target>
```

After the build, generated files(`DataTableManagerExtension.cs` and `Tables/DR***.cs`) in CodeOutputDirectory, generated data files(`Datas/***.bin`) in DataOutputDirectory.

Finally, you can regsiter and query by these files.

```csharp
// to load datatables, use DataTableManagerExtension to load all datatables.
using DataTables;

public static class DataTableManagerHelper
{
    public static DataTableManager Configure(this DataTableManager manager, string dataPath)
    {
        foreach (var className in DataTableManagerExtension.Names)
        {
            var raw = File.ReadAllBytes(Path.Combine(dataPath, className + ".bin"));
            manager.CreateDataTable(className, raw);
        }
    }
}

// -----------------------

// for query phase, use DataTableManager.
var manager = new DataTableManager();
manager.Configure("xxxxx");
var drScene = manager.GetDataTable<DRScene>().GetDataRow(x => x.Id == 2000);

```

You can invoke all indexed query by IntelliSense.

Getting Started(Unity)
---
Check the [releases](https://github.com/PhonixGame/DataTables/releases) page, download `DataTables.Unity.unitypackage`(runtime) and `DataTables.Generator.zip`(cli code-generator).

Prepare the example table definition like following.

![ExcelSample](https://user-images.githubusercontent.com/5179057/227073069-1cd264bf-d8ca-4b77-9c71-bce0bce66150.PNG)

use the DataTables code generator by commandline. Commandline tool support platforms are `win-x64`, `osx-x64` and `linux-x64`.

```
Usage: DataTables.Generator [options...]

Options:
  -i, -inputDirectory <String>              Input file directory(search recursive). (Required)
  -co, -codeOutputDirectory <String>        Code Output file directory. (Required)
  -do, -dataOutputDirectory <String>        Data Output file directory. (Required)
  -n, -usingNamespace <String>              Namespace of generated files. (Required)
  -p, -prefixClassName <String>             Prefix of class names. (Default: )
  -t, -filterColumnTags <String>            Tags of filter columns. (Default: )
  -f, -forceOverwrite <Boolean>             Overwrite generated files if the content is unchanged. (Default: false)
```

```bash
DataTables.Generator.exe -i "C:\UnitySample" -co "C:\UnitySample\Generated" -do "C:\UnitySample\DataTable" -n "UnitySample" -p "DR"
```

The rest is the same as .NET Core version.

```csharp
// to load datatables, use DataTableManagerExtension to load all datatables.
using DataTables;

public static class DataTableManagerHelper
{
    public static DataTableManager Configure(this DataTableManager manager, string dataPath)
    {
        foreach (var className in DataTableManagerExtension.Names)
        {
            var raw = File.ReadAllBytes(Path.Combine(dataPath, className + ".bin"));
            manager.CreateDataTable(className, raw);
        }
    }
}

// -----------------------

// for query phase, use DataTableManager.
var manager = new DataTableManager();
manager.Configure("xxxxx");
var drScene = manager.GetDataTable<DRScene>().GetDataRow(x => x.Id == 2000);

```

You can invoke all indexed query by IntelliSense.

DataTable configuration
---

标签页定义格式：
* 标签页名称以`#`开头将不会导出；

配置表定义格式：
* 第一行：表头格式定义行，使用`DataTabeGenerator`开头，主要定义表级别的一些配置
* 第二行：列名称定义行，支持列注释、标签过滤等功能
* 第三行：字段名称定义行
* 第四行：字段类型定义行

## 表头格式定义行

以`,`分隔的参数定义，大小写不敏感，支持以下功能：
* DataTabeGenerator: 标识该Sheet支持导出（实际可有可无，不强制要求）；
* Title: 该Sheet的中文名称，将出现在类定义的注释栏里；
* Class: 该Sheet的类名称，同时正确的格式才会导出该Sheet；
* Split: 支持按Sheet进行分表，即同一个Class的若存在SubTitle定义，将会导出成多个数据文件，加载时单次仅仅加载一个文件；
* EnableTagsFilter: 启用对各列按标签进行导出，输入标签由导出工具运行时提供；
* Index: 对指定列进行索引，导出后将提供快捷接口进行查询，查询结果为单个记录；支持同时配置多个Index；支持同时配置多个列，以`&`拼接；
* Group: 对指定列进行分组，导出后将提供快捷接口进行查询，查询结果为多个记录；支持同时配置多个Group；支持同时配置多个列，以`&`拼接；

## 列名称定义行

功能支持：
* 支持在列字段文本以`#`字符开头，代表该列注释，不再参与后续的导出；
* 支持在列字段文本以`@ + 大写英文字母`结尾，代表该列支持按标签导出，一个英文字母代表一个标签，具体导出哪些标签由命令行运行时指定；

## 字段名称定义行

由英文字母数字与下划线组成，同时不能以数字开头；大小写敏感；

## 字段类型定义行

支持以下字段定义：
* `short`, `int`, `long`, `ushort`, `uint`, `ulong`
* `float`, `double`
* `bool`
* `DateTime`
* `Array` : StartWiths Array string, like Array<int>, Array<string>
* `Enum` : StartWiths Enum string, like Enum<ColorT>
* `Dictionary` : StartWiths Map string, like Map<int, int>, Map<int, string>
* `JSON`: 支持将单元格文本转化为JSON对象

Optimization
---

<!--
When invoking `new MemoryDatabase(byte[] databaseBinary...)`, read and construct database from binary. If binary size is large then construct performance will slow down. `MemoryDatabase` has `ctor(..., int maxDegreeOfParallelism = 1)` option in constructor to construct in parallel.

```csharp
var database = new MemoryDatabase(bin, maxDegreeOfParallelism: Environment.ProcessorCount);
```

The use of Parallel can greatly improve the construct performance. Recommend to use `Environment.ProcessorCount`.

If you want to reduce code size of generated code, Validator and MetaDatabase info can omit in runtime. Generated code has two symbols `DISABLE_MASTERMEMORY_VALIDATOR` and `DISABLE_MASTERMEMORY_METADATABASE`.  By defining them, can be erased from the build code.
-->

Code Generator
---
DataTables has two kinds of code-generator. `MSBuild Task`, `.NET Core Global/Local Tools`.

MSBuild Task(`DataTables.MSBuild.Tasks`) is recommended way to use in .NET Core csproj.

```xml
<DataTablesGenerator
    UsingNamespace="string:required"
    InputDirectory="string:required"
    CodeOutputDirectory="string:required"
    DataOutputDirectory="string:required"
    PrefixClassName="string:optional, default= "
    FilterColumnTags="string:optional, default= "
    ForceOverwrite="bool:optional, default=false"
/>
```

`.NET Core Global/Local Tools` can install from NuGet(`DataTables.Generator`), you need to install .NET runtime. Here is the sample command of install global tool.

`dotnet tool install --global DataTables.Generator`

```
Usage: DataTables.Generator [options...]

Options:
  -i, -inputDirectory <String>              Input file directory(search recursive). (Required)
  -co, -codeOutputDirectory <String>        Code Output file directory. (Required)
  -do, -dataOutputDirectory <String>        Data Output file directory. (Required)
  -n, -usingNamespace <String>              Namespace of generated files. (Required)
  -p, -prefixClassName <String>             Prefix of class names. (Default: )
  -t, -filterColumnTags <String>            Tags of filter columns. (Default: )
  -f, -forceOverwrite <Boolean>             Overwrite generated files if the content is unchanged. (Default: false)
```

After install, you can call by `dotnet DataTables.Generator` command. This is useful to use in CI. Here is the sample of CircleCI config.

```yml
version: 2.1
executors:
  dotnet:
    docker:
      - image: mcr.microsoft.com/dotnet/core/sdk:2.2
    environment:
      DOTNET_SKIP_FIRST_TIME_EXPERIENCE: true
      NUGET_XMLDOC_MODE: skip
jobs:
  gen-datatables:
    executor: dotnet
    steps:
      - checkout
      - run: dotnet tool install --global DataTables.Generator
      - run: dotnet DataTables.Generator -i "inputDir" -co "client\Assets\Scripts\Game\DataTables" -do "client\Assets\AssetBundles\DataTables" -n Demo.DataTales
      /* git push or store artifacts or etc...... */
```

License
---
This library is under the MIT License.
