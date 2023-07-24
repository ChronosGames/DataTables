[![GitHub Actions](https://github.com/PhonixGame/DataTables/workflows/Build-Debug/badge.svg)](https://github.com/PhonixGame/DataTables/actions) [![Releases](https://img.shields.io/github/release/PhonixGame/DataTables.svg)](https://github.com/PhonixGame/DataTables/releases)

DataTables
===

适用于.NET Core的服务端与Unity客户端的数据表解决方案。

<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
## Table of Contents

- [Concept](#concept)
- [DataTable configuration](#datatable-configuration)
- [表格型(Table)定义格式：](#%E8%A1%A8%E6%A0%BC%E5%9E%8Btable%E5%AE%9A%E4%B9%89%E6%A0%BC%E5%BC%8F)
  - [表头格式定义行](#%E8%A1%A8%E5%A4%B4%E6%A0%BC%E5%BC%8F%E5%AE%9A%E4%B9%89%E8%A1%8C)
  - [列名称定义行](#%E5%88%97%E5%90%8D%E7%A7%B0%E5%AE%9A%E4%B9%89%E8%A1%8C)
  - [字段名称定义行](#%E5%AD%97%E6%AE%B5%E5%90%8D%E7%A7%B0%E5%AE%9A%E4%B9%89%E8%A1%8C)
  - [字段类型定义行](#%E5%AD%97%E6%AE%B5%E7%B1%BB%E5%9E%8B%E5%AE%9A%E4%B9%89%E8%A1%8C)
- [矩阵型(Matrix)定义格式：](#%E7%9F%A9%E9%98%B5%E5%9E%8Bmatrix%E5%AE%9A%E4%B9%89%E6%A0%BC%E5%BC%8F)
  - [表头格式定义行](#%E8%A1%A8%E5%A4%B4%E6%A0%BC%E5%BC%8F%E5%AE%9A%E4%B9%89%E8%A1%8C-1)
- [Getting Started(.NET Core)](#getting-startednet-core)
- [Getting Started(Unity)](#getting-startedunity)
- [UPM Package](#upm-package)
  - [Install via git URL](#install-via-git-url)
  - [Install via OpenUPM](#install-via-openupm)
- [Optimization](#optimization)
- [Code Generator](#code-generator)
- [License](#license)

<!-- END doctoc generated TOC please keep comment here to allow auto update -->

Concept
---

* **支持常见的数据表格式**, 如Excel 2007-365(*.xlsx), CSV等。
* **支持数据表的并行导出**, 通过使用并行导出，大幅提高数据表的导出速度。
* **支持表格型与矩阵形的数据配置**, 支持常见的数据表配置以及二维矩阵表配置。
* **导出数据定义代码文件**, 通过数据表中定义的代码格式自动生成对应的数据格式代码文件，并提供方便的API接口，方便在终端环境内读取数据文件。
* **导出数据内容二进制文件**, 通过紧凑组织的二进制文件，加快读取性能以及缩减配置文件体积大小。

DataTable configuration
---

标签页(sheet)定义格式：
* 标签页名称以`#`开头将不会导出；

## 表格型(Table)定义格式：
* 第一行：表头格式定义行，使用`DTGen`开头，主要定义表级别的一些配置
* 第二行：列名称定义行，支持列注释、标签过滤等功能
* 第三行：字段名称定义行
* 第四行：字段类型定义行

### 表头格式定义行

以`,`分隔的参数定义，大小写不敏感，支持以下功能：
* DTGen: 标识该Sheet支持导出（实际可有可无，不强制要求），默认是`DTGen=Table`；
* Title: 该Sheet的中文名称，将出现在类定义的注释栏里；
* Class: 该Sheet的类名称，同时正确的格式才会导出该Sheet；
* Child: 支持按Sheet进行分表，即同一个Class的若存在SubTitle定义，将会导出成多个数据文件，加载时单次仅仅加载一个文件；
* EnableTagsFilter: 启用对各列按标签进行导出，输入标签由导出工具运行时提供；
* Index: 对指定列进行索引，导出后将提供快捷接口进行查询，查询结果为单个记录；支持同时配置多个Index；支持同时配置多个列，以`&`拼接；
* Group: 对指定列进行分组，导出后将提供快捷接口进行查询，查询结果为多个记录；支持同时配置多个Group；支持同时配置多个列，以`&`拼接；

### 列名称定义行

功能支持：
* 支持在列字段文本以`#`字符开头，代表该列注释，不再参与后续的导出；
* 支持在列字段文本以`@ + 大写英文字母`结尾，代表该列支持按标签导出，一个英文字母代表一个标签，具体导出哪些标签由命令行运行时指定；

### 字段名称定义行

由英文字母数字与下划线组成，同时不能以数字开头；大小写敏感；

### 字段类型定义行

支持以下字段定义：
* `short`, `int`, `long`, `ushort`, `uint`, `ulong`
* `float`, `double`
* `bool`
* `DateTime`
* `Array` : StartWiths Array string, like Array<int>, Array<string>
* `Enum` : StartWiths Enum string, like Enum<ColorT>
* `Dictionary` : StartWiths Map string, like Map<int, int>, Map<int, string>
* `JSON`: 支持将单元格文本转化为JSON对象
* `Custom`: 支持自定义类的导出, 自定义类必须拥有带一个字符串形参的构造函数

## 矩阵型(Matrix)定义格式：
* 第一行：表头格式定义行，使用`DTGen=Matrix`开头，主要定义表级别的一些配置
* 第一列：X轴值内容，剔除头两个单元格；
* 第二行：Y轴值内容，剔除头一个单元格；

### 表头格式定义行

以`,`分隔的参数定义，大小写不敏感，支持以下功能：
* DTGen: 标识该Sheet支持导出, 以`DTGen=Matrix`识别；
* Title: 该Sheet的中文名称，将出现在类定义的注释栏里；
* Class: 该Sheet的类名称，同时正确的格式才会导出该Sheet；
* Matrix: 定义X轴、Y轴以及单元格的值类型，如`Matrix=<X轴值类型>&<Y轴值类型>&<单元格值类型>`；

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
    <PackageReference Include="DataTables.API" Version="0.8.4" />
    <!-- Install MSBuild Task(with PrivateAssets="All", it means to use dependency only in build time). -->
    <PackageReference Include="DataTables.MSBuild.Tasks" Version="0.8.4" PrivateAssets="All" />
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
// 预加载指定数据表，然后，进行查询

var manager = new DataTableManager();

// 使用默认的数据表数据文件加载器
manager.SetDataTableHelper(new DefaultDataTableHelper("<Data Files Dir>"));

// 预加载DTScene数据表
manager.CreateDataTable<DTScene>(null);

// 由于默认数据表加载器是同步调用的方式，若可在Preload之后直接查询数据，否则要放在callback内
var drScene1 = manager.GetDataTable<DRScene>().GetDataRow(x => x.Id == 2000);
var drScene2 = manager.GetDataTable<DRScene>().GetDataRowById(2000);

// -----------------------
// 预加载全部数据表，然后，查询任意数据表的内容示例：

var manager = new DataTableManager();

// 使用默认的数据表数据文件加载器
manager.SetDataTableHelper(new DefaultDataTableHelper("<Data Files Dir>"));

// 预加载所有的数据表
manager.Preload(() => Console.WriteLine("数据表全部加载完毕"));

// 由于默认数据表加载器是同步调用的方式，若可在Preload之后直接查询数据，否则要放在callback内
var drScene1 = manager.GetDataTable<DRScene>().GetDataRow(x => x.Id == 2000);
var drScene2 = manager.GetDataTable<DRScene>().GetDataRowById(2000);
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
// 预加载指定数据表，然后，进行查询

var manager = new DataTableManager();

// 使用默认的数据表数据文件加载器
manager.SetDataTableHelper(new DefaultDataTableHelper("<Data Files Dir>"));

// 预加载DTScene数据表
manager.CreateDataTable<DTScene>(null);

// 由于默认数据表加载器是同步调用的方式，若可在Preload之后直接查询数据，否则要放在callback内
var drScene1 = manager.GetDataTable<DRScene>().GetDataRow(x => x.Id == 2000);
var drScene2 = manager.GetDataTable<DRScene>().GetDataRowById(2000);

// -----------------------
// 预加载全部数据表，然后，查询任意数据表的内容示例：

var manager = new DataTableManager();

// 使用默认的数据表数据文件加载器
manager.SetDataTableHelper(new DefaultDataTableHelper("<Data Files Dir>"));

// 预加载所有的数据表
manager.Preload(() => Console.WriteLine("数据表全部加载完毕"));

// 由于默认数据表加载器是同步调用的方式，若可在Preload之后直接查询数据，否则要放在callback内
var drScene1 = manager.GetDataTable<DRScene>().GetDataRow(x => x.Id == 2000);
var drScene2 = manager.GetDataTable<DRScene>().GetDataRowById(2000);
```

You can invoke all indexed query by IntelliSense.

UPM Package
---
### Install via git URL

Requires a version of unity that supports path query parameter for git packages (Unity >= 2019.3.4f1, Unity >= 2020.1a21). You can add `https://github.com/PhonixGame/DataTables.git?path=src/DataTables.Unity/Assets/Scripts/DataTables` to Package Manager

![image](https://user-images.githubusercontent.com/46207/79450714-3aadd100-8020-11ea-8aae-b8d87fc4d7be.png)

![image](https://user-images.githubusercontent.com/46207/83702872-e0f17c80-a648-11ea-8183-7469dcd4f810.png)

or add `"game.phonix.datatables": "https://github.com/PhonixGame/DataTables.git?path=src/DataTables.Unity/Assets/Scripts/DataTables"` to `Packages/manifest.json`.

If you want to set a target version, UniTask uses the `*.*.*` release tag so you can specify a version like `#0.9.5`. For example `https://github.com/PhonixGame/DataTables.git?path=src/DataTables.Unity/Assets/Scripts/DataTables#0.9.5`.

### Install via OpenUPM

The package is available on the [openupm registry](https://openupm.com). It's recommended to install it via [openupm-cli](https://github.com/openupm/openupm-cli).

```
openupm add game.phonix.datatables
```

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
