[![GitHub Actions](https://github.com/PhonixGame/DataTables/workflows/Build-Debug/badge.svg)](https://github.com/PhonixGame/DataTables/actions) [![Releases](https://img.shields.io/github/release/PhonixGame/DataTables.svg)](https://github.com/PhonixGame/DataTables/releases)

DataTables
===

רע��Excel���ñ��ĵ�����Ŀǰ֧��.NET Core�ķ������Unity�ͻ��ˡ�

<!-- ![image](https://user-images.githubusercontent.com/46207/61031896-61890800-a3fb-11e9-86b7-84c821d347a4.png) -->

<!-- **4700** times faster than SQLite and achieves zero allocation per query. Also the DB size is small. When SQLite is 3560kb then MasterMemory is only 222kb. -->

<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
## Table of Contents

- [Concept](#concept)
- [Getting Started(.NET Core)](#getting-startednet-core)
- [Getting Started(Unity)](#getting-startedunity)
- [DataTable configuration](#datatable-configuration)
- [��ͷ��ʽ������](#%EF%BF%BD%EF%BF%BD%CD%B7%EF%BF%BD%EF%BF%BD%CA%BD%EF%BF%BD%EF%BF%BD%EF%BF%BD%EF%BF%BD%EF%BF%BD%EF%BF%BD)
- [�����ƶ�����](#%EF%BF%BD%EF%BF%BD%EF%BF%BD%EF%BF%BD%EF%BF%BD%C6%B6%EF%BF%BD%EF%BF%BD%EF%BF%BD%EF%BF%BD%EF%BF%BD)
- [�ֶ����ƶ�����](#%EF%BF%BD%D6%B6%EF%BF%BD%EF%BF%BD%EF%BF%BD%EF%BF%BD%C6%B6%EF%BF%BD%EF%BF%BD%EF%BF%BD%EF%BF%BD%EF%BF%BD)
- [�ֶ����Ͷ�����](#%EF%BF%BD%D6%B6%EF%BF%BD%EF%BF%BD%EF%BF%BD%EF%BF%BD%CD%B7%EF%BF%BD%EF%BF%BD%EF%BF%BD%EF%BF%BD%EF%BF%BD)
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

��ǩҳ�����ʽ��
* ��ǩҳ������`#`��ͷ�����ᵼ����

���ñ������ʽ��
* ��һ�У���ͷ��ʽ�����У�ʹ��`DataTabeGenerator`��ͷ����Ҫ����������һЩ����
* �ڶ��У������ƶ����У�֧����ע�͡���ǩ���˵ȹ���
* �����У��ֶ����ƶ�����
* �����У��ֶ����Ͷ�����

## ��ͷ��ʽ������

��`,`�ָ��Ĳ������壬��Сд�����У�֧�����¹��ܣ�
* DataTabeGenerator: ��ʶ��Sheet֧�ֵ�����ʵ�ʿ��п��ޣ���ǿ��Ҫ�󣩣�
* Title: ��Sheet���������ƣ����������ඨ���ע�����
* Class: ��Sheet�������ƣ�ͬʱ��ȷ�ĸ�ʽ�Żᵼ����Sheet��
* Split: ֧�ְ�Sheet���зֱ�����ͬһ��Class��������SubTitle���壬���ᵼ���ɶ�������ļ�������ʱ���ν�������һ���ļ���
* EnableTagsFilter: ���öԸ��а���ǩ���е����������ǩ�ɵ�����������ʱ�ṩ��
* Index: ��ָ���н����������������ṩ��ݽӿڽ��в�ѯ����ѯ���Ϊ������¼��֧��ͬʱ���ö��Index��֧��ͬʱ���ö���У���`&`ƴ�ӣ�
* Group: ��ָ���н��з��飬�������ṩ��ݽӿڽ��в�ѯ����ѯ���Ϊ�����¼��֧��ͬʱ���ö��Group��֧��ͬʱ���ö���У���`&`ƴ�ӣ�

## �����ƶ�����

����֧�֣�
* ֧�������ֶ��ı���`#`�ַ���ͷ����������ע�ͣ����ٲ�������ĵ�����
* ֧�������ֶ��ı���`@ + ��дӢ����ĸ`��β����������֧�ְ���ǩ������һ��Ӣ����ĸ����һ����ǩ�����嵼����Щ��ǩ������������ʱָ����

## �ֶ����ƶ�����

��Ӣ����ĸ�������»�����ɣ�ͬʱ���������ֿ�ͷ����Сд���У�

## �ֶ����Ͷ�����

֧�������ֶζ��壺
* `short`, `int`, `long`, `ushort`, `uint`, `ulong`
* `float`, `double`
* `bool`
* `DateTime`
* `Array` : StartWiths Array string, like Array<int>, Array<string>
* `Enum` : StartWiths Enum string, like Enum<ColorT>
* `Dictionary` : StartWiths Map string, like Map<int, int>, Map<int, string>
* `JSON`: ֧�ֽ���Ԫ���ı�ת��ΪJSON����

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
