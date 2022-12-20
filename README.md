[![GitHub Actions](https://github.com/PhonixGame/DataTables/workflows/Build-Debug/badge.svg)](https://github.com/PhonixGame/DataTables/actions) [![Releases](https://img.shields.io/github/release/PhonixGame/DataTables.svg)](https://github.com/PhonixGame/DataTables/releases)

DataTables
===

DataTable Solution for .NET Core and Unity. 

<!-- ![image](https://user-images.githubusercontent.com/46207/61031896-61890800-a3fb-11e9-86b7-84c821d347a4.png) -->

**4700** times faster than SQLite and achieves zero allocation per query. Also the DB size is small. When SQLite is 3560kb then MasterMemory is only 222kb.

<!-- START doctoc generated TOC please keep comment here to allow auto update -->
<!-- DON'T EDIT THIS SECTION, INSTEAD RE-RUN doctoc TO UPDATE -->
## Table of Contents

- [Concept](#concept)
- [Getting Started(.NET Core)](#getting-startednet-core)
- [Getting Started(Unity)](#getting-startedunity)
- [DataTable configuration](#datatable-configuration)
- [Built-in supported types](#built-in-supported-types)
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

These features are suitable for master data management(write-once, read-heavy) on embedded application such as role-playing game. MasterMemory has better performance than any other database solutions. [PalDB](https://github.com/linkedin/PalDB) developed by LinkedIn has a similar concept(embeddable write-once key-value store), but the implementation and performance characteristics are completely different.

Getting Started(.NET Core)
---
DataTables uses C# to C# code-generator. Runtime library API is the same but how to code-generate has different way between .NET Core and Unity. This sample is for .NET Core(for Unity is in below sections).

Install the core library(Runtime and [Annotations](https://www.nuget.org/packages/DataTables.Annotations)).

> PM> Install-Package [DataTables](https://www.nuget.org/packages/DataTables.API)

Prepare the example excel table definition like following.

| 场景编号  |	 #备注	|   资源名称	| 背景音乐编号       |
| :-------: | :---------: | :-----------: | :---------------: |  
| Id	    |             |   AssetName   | BackgroundMusicId |
| int       |             |	  string      | int               |
| 2000      | 登录        |	Login       | 0                 |
| 2001	    | 初始化角色  |	Initial	     | 2                 |
| 2099      | 主界面      | Menu          | 2                 |
| 2100      | 城镇        | Game          | 2                 |
```

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

| 场景编号  |	 #备注	|   资源名称	| 背景音乐编号       |
| :-------: | :---------: | :-----------: | :---------------: |  
| Id	    |             |   AssetName   | BackgroundMusicId |
| int       |             |	  string      | int               |
| 2000      | 登录        |	Login       | 0                 |
| 2001	    | 初始化角色  |	Initial	     | 2                 |
| 2099      | 主界面      | Menu          | 2                 |
| 2100      | 城镇        | Game          | 2                 |

use the DataTables code generator by commandline. Commandline tool support platforms are `win-x64`, `osx-x64` and `linux-x64`.

```
Usage: DataTables.Generator [options...]

Options:
  -i, -inputDirectory <String>              Input file directory(search recursive). (Required)
  -co, -codeOutputDirectory <String>        Code Output file directory. (Required)
  -do, -dataOutputDirectory <String>        Data Output file directory. (Required)
  -n, -usingNamespace <String>              Namespace of generated files. (Required)
  -p, -prefixClassName <String>             Prefix of class names. (Default: )
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
Element type of datatable must be marked by `[MemoryTable(tableName)]`, datatable is generated from marked type. `string tableName` is saved in database binary, you can rename class name if tableName is same.

`[PrimaryKey(keyOrder = 0)]`, `[SecondaryKey(indexNo, keyOrder)]`, `[NonUnique]` can add to public property, `[PrimaryKey]` must use in MemoryTable, `[SecondaryKey]` is option.

Both `PrimaryKey` and `SecondaryKey` can add to multiple properties, it will be generated `***And***And***...`. `keyOrder` is order of column names, default is zero(sequential in which they appear).

```csharp
[MemoryTable("sample"), MessagePackObject(true)]
public class Sample
{
    [PrimaryKey]
    public int Foo { get; set; }
    [PrimaryKey]
    public int Bar { get; set; }
}

db.Sample.FindByFooAndBar((int Foo, int Bar))

// ----

[MemoryTable("sample"), MessagePackObject(true)]
public class Sample
{
    [PrimaryKey(keyOrder: 1)]
    public int Foo { get; set; }
    [PrimaryKey(keyOrder: 0)]
    public int Bar { get; set; }
}

db.Sample.FindByBarAndFoo((int Bar, int Foo))
```

Default of `FindBy***` return type is single(if not found, returns `null`). It means key is unique by default. If mark `[NonUnique]` in same AttributeList, return type is `RangeView<T>`(if not found, return empty).

```csharp
[MemoryTable("sample"), MessagePackObject(true)]
public class Sample
{
    [PrimaryKey, NonUnique]
    public int Foo { get; set; }
    [PrimaryKey, NonUnique]
    public int Bar { get; set; }
}

RangeView<Sample> q = db.Sample.FindByFooAndBar((int Foo, int Bar))
```

```csharp
[MemoryTable("sample"), MessagePackObject(true)]
public class Sample
{
    [PrimaryKey]
    [SecondaryKey(0)]
    public int Foo { get; set; }
    [SecondaryKey(0)]
    [SecondaryKey(1)]
    public int Bar { get; set; }
}

db.Sample.FindByFoo(int Foo)
db.Sample.FindByFooAndBar((int Foo, int Bar))
db.Sample.FindByBar(int Bar)
```

`[StringComparisonOption]` allow to configure how compare if key is string. Default is `Ordinal`.

```csharp
[MemoryTable("sample"), MessagePackObject(true)]
public class Sample
{
    [PrimaryKey]
    [StringComparisonOption(StringComparison.InvariantCultureIgnoreCase)]
    public string Foo { get; set; }
}
```

If computation property exists, add `[IgnoreMember]` of MessagePack should mark.

```csharp
[MemoryTable("person"), MessagePackObject(true)]
public class Person
{
    [PrimaryKey]
    public int Id { get;}

    public string FirstName { get; }
    public string LastName { get; }

    [IgnoreMember]
    public string FullName => FirstName + LastName;
}
```

Built-in supported types
---
These field types can serialize by default:

* `short`, `int`, `long`, `ushort`, `uint`, `ulong`
* `Enum` : StartWiths Enum string, like EnumItemType
* `Array[]` like int[], string[]
<!-- * Primitives (`int`, `string`, etc...), `Enum`s, `Nullable<>`, `Lazy<>`
* `TimeSpan`,  `DateTime`, `DateTimeOffset`
* `Guid`, `Uri`, `Version`, `StringBuilder`
* `BigInteger`, `Complex`, `Half`
* `Array[]`, `Array[,]`, `Array[,,]`, `Array[,,,]`, `ArraySegment<>`, `BitArray`
* `KeyValuePair<,>`, `Tuple<,...>`, `ValueTuple<,...>`
* `ArrayList`, `Hashtable`
* `List<>`, `LinkedList<>`, `Queue<>`, `Stack<>`, `HashSet<>`, `ReadOnlyCollection<>`, `SortedList<,>`
* `IList<>`, `ICollection<>`, `IEnumerable<>`, `IReadOnlyCollection<>`, `IReadOnlyList<>`
* `Dictionary<,>`, `IDictionary<,>`, `SortedDictionary<,>`, `ILookup<,>`, `IGrouping<,>`, `ReadOnlyDictionary<,>`, `IReadOnlyDictionary<,>`
* `ObservableCollection<>`, `ReadOnlyObservableCollection<>`
* `ISet<>`,
* `ConcurrentBag<>`, `ConcurrentQueue<>`, `ConcurrentStack<>`, `ConcurrentDictionary<,>`
* Immutable collections (`ImmutableList<>`, etc)
* Custom implementations of `ICollection<>` or `IDictionary<,>` with a parameterless constructor
* Custom implementations of `IList` or `IDictionary` with a parameterless constructor


You can add support for custom types, and there are some official/third-party extension packages for:

* ReactiveProperty
* for Unity (`Vector3`, `Quaternion`, etc...)
* F# (Record, FsList, Discriminated Unions, etc...)

Please see the [extensions section](#extensions).

`MessagePack.Nil` is the built-in type representing null/void in MessagePack for C#.
-->

Validator
---
You can validate data by `MemoryDatabase.Validate` method. In default, it check unique key(data duplicated) and you can define custom validate logics.

```csharp
// Implements IValidatable<T> to targeted validation
[MemoryTable("quest_master"), MessagePackObject(true)]
public class Quest : IValidatable<Quest>
{
    // If index is Unique, validate duplicate in default.
    [PrimaryKey]
    public int Id { get; }
    public string Name { get; }
    public int RewardId { get; }
    public int Cost { get; }

    void IValidatable<Quest>.Validate(IValidator<Quest> validator)
    {
        // get the external reference table
        var items = validator.GetReferenceSet<Item>();

        // Custom if logics.
        if (this.RewardId > 0)
        {
            // RewardId must exists in Item.ItemId
            items.Exists(x => x.RewardId, x => x.ItemId);
        }

        // Range check, Cost must be 10..20
        validator.Validate(x => x.Cost >= 10);
        validator.Validate(x => x.Cost <= 20);

        // In this region, only called once so enable to validate overall of tables.
        if (validator.CallOnce())
        {
            var quests = validator.GetTableSet();
            // Check unique othe than index property.
            quests.Where(x => x.RewardId != 0).Unique(x => x.RewardId);
        }
    }
}

[MemoryTable("item_master"), MessagePackObject(true)]
public class Item
{
    [PrimaryKey]
    public int ItemId { get; }
}

void Main()
{
    var db = new MemoryDatabase(bin);

    // Get the validate result.
    var validateResult = db.Validate();
    if (validateResult.IsValidationFailed)
    {
        // Output string format.
        Console.WriteLine(validateResult.FormatFailedResults());

        // Get the raw FaildItem[]. (.Type, .Message, .Data)
        // validateResult.FailedResults
    }
}
```

Following is list of validation methods.

```csharp
// all void methods are assert function, it stores message to ValidateResult if failed.
interface IValidator<T>
{
    ValidatableSet<T> GetTableSet();
    ReferenceSet<T, TRef> GetReferenceSet<TRef>();
    void Validate(Expression<Func<T, bool>> predicate);
    void Validate(Func<T, bool> predicate, string message);
    void ValidateAction(Expression<Func<bool>> predicate);
    void ValidateAction(Func<bool> predicate, string message);
    void Fail(string message);
    bool CallOnce();
}

class ReferenceSet<TElement, TReference>
{
    IReadOnlyList<TReference> TableData { get; }
    void Exists<TProperty>(Expression<Func<TElement, TProperty>> elementSelector, Expression<Func<TReference, TProperty>> referenceElementSelector);
    void Exists<TProperty>(Expression<Func<TElement, TProperty>> elementSelector, Expression<Func<TReference, TProperty>> referenceElementSelector, EqualityComparer<TProperty> equalityComparer);
}

class ValidatableSet<TElement>
{
    IReadOnlyList<TElement> TableData { get; }
    void Unique<TProperty>(Expression<Func<TElement, TProperty>> selector);
    void Unique<TProperty>(Expression<Func<TElement, TProperty>> selector, IEqualityComparer<TProperty> equalityComparer);
    void Unique<TProperty>(Func<TElement, TProperty> selector, string message);
    void Unique<TProperty>(Func<TElement, TProperty> selector, IEqualityComparer<TProperty> equalityComparer, string message);
    void Sequential(Expression<Func<TElement, SByte|Int16|Int32|...>> selector, bool distinct = false);
    ValidatableSet<TElement> Where(Func<TElement, bool> predicate);
}
```

Metadata
---
You can get the table-info, properties, indexes by metadata api. It helps to make custom importer/exporter application.

```csharp
var metaDb = MemoryDatabase.GetMetaDatabase();
foreach (var table in metaDb.GetTableInfos())
{
    // for example, generate CSV header
    var sb = new StringBuilder();
    foreach (var prop in table.Properties)
    {
        if (sb.Length != 0) sb.Append(",");

        // Name can convert to LowerCamelCase or SnakeCase.
        sb.Append(prop.NameSnakeCase);
    }
    File.WriteAllText(table.TableName + ".csv", sb.ToString(), new UTF8Encoding(false));
}
```

If creates console-app, our [ConsoleAppFramework](https://github.com/Cysharp/ConsoleAppFramework/) can easy to make helper applications.

Here is sample of reading and creating dynamic from csv. `builder.AppendDynamic` and `System.Runtime.Serialization.FormatterServices.GetUninitializedObject` will help it.

```csharp
class Program
{
    static void Main(string[] args)
    {
        var csv = @"monster_id,name,max_hp
1,foo,100
2,bar,200";
        var fileName = "monster";

        var builder = new DatabaseBuilder();

        var meta = MemoryDatabase.GetMetaDatabase();
        var table = meta.GetTableInfo(fileName);

        var tableData = new List<object>();

        using (var ms = new MemoryStream(Encoding.UTF8.GetBytes(csv)))
        using (var sr = new StreamReader(ms, Encoding.UTF8))
        using (var reader = new TinyCsvReader(sr))
        {
            while ((reader.ReadValuesWithHeader() is Dictionary<string, string> values))
            {
                // create data without call constructor
                var data = System.Runtime.Serialization.FormatterServices.GetUninitializedObject(table.DataType);

                foreach (var prop in table.Properties)
                {
                    if (values.TryGetValue(prop.NameSnakeCase, out var rawValue))
                    {
                        var value = ParseValue(prop.PropertyInfo.PropertyType, rawValue);
                        if (prop.PropertyInfo.SetMethod == null)
                        {
                            throw new Exception("Target property does not exists set method. If you use {get;}, please change to { get; private set; }, Type:" + prop.PropertyInfo.DeclaringType + " Prop:" + prop.PropertyInfo.Name);
                        }
                        prop.PropertyInfo.SetValue(data, value);
                    }
                    else
                    {
                        throw new KeyNotFoundException($"Not found \"{prop.NameSnakeCase}\" in \"{fileName}.csv\" header.");
                    }
                }

                tableData.Add(data);
            }
        }

        // add dynamic collection.
        builder.AppendDynamic(table.DataType, tableData);

        var bin = builder.Build();
        var database = new MemoryDatabase(bin);
    }

    static object ParseValue(Type type, string rawValue)
    {
        if (type == typeof(string)) return rawValue;

        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
        {
            if (string.IsNullOrWhiteSpace(rawValue)) return null;
            return ParseValue(type.GenericTypeArguments[0], rawValue);
        }

        if (type.IsEnum)
        {
            var value = Enum.Parse(type, rawValue);
            return value;
        }

        switch (Type.GetTypeCode(type))
        {
            case TypeCode.Boolean:
                // True/False or 0,1
                if (int.TryParse(rawValue, out var intBool))
                {
                    return Convert.ToBoolean(intBool);
                }
                return Boolean.Parse(rawValue);
            case TypeCode.Char:
                return Char.Parse(rawValue);
            case TypeCode.SByte:
                return SByte.Parse(rawValue, CultureInfo.InvariantCulture);
            case TypeCode.Byte:
                return Byte.Parse(rawValue, CultureInfo.InvariantCulture);
            case TypeCode.Int16:
                return Int16.Parse(rawValue, CultureInfo.InvariantCulture);
            case TypeCode.UInt16:
                return UInt16.Parse(rawValue, CultureInfo.InvariantCulture);
            case TypeCode.Int32:
                return Int32.Parse(rawValue, CultureInfo.InvariantCulture);
            case TypeCode.UInt32:
                return UInt32.Parse(rawValue, CultureInfo.InvariantCulture);
            case TypeCode.Int64:
                return Int64.Parse(rawValue, CultureInfo.InvariantCulture);
            case TypeCode.UInt64:
                return UInt64.Parse(rawValue, CultureInfo.InvariantCulture);
            case TypeCode.Single:
                return Single.Parse(rawValue, CultureInfo.InvariantCulture);
            case TypeCode.Double:
                return Double.Parse(rawValue, CultureInfo.InvariantCulture);
            case TypeCode.Decimal:
                return Decimal.Parse(rawValue, CultureInfo.InvariantCulture);
            case TypeCode.DateTime:
                return DateTime.Parse(rawValue, CultureInfo.InvariantCulture);
            default:
                if (type == typeof(DateTimeOffset))
                {
                    return DateTimeOffset.Parse(rawValue, CultureInfo.InvariantCulture);
                }
                else if (type == typeof(TimeSpan))
                {
                    return TimeSpan.Parse(rawValue, CultureInfo.InvariantCulture);
                }
                else if (type == typeof(Guid))
                {
                    return Guid.Parse(rawValue);
                }

                // or other your custom parsing.
                throw new NotSupportedException();
        }
    }

    // Non string escape, tiny reader with header.
    public class TinyCsvReader : IDisposable
    {
        static char[] trim = new[] { ' ', '\t' };

        readonly StreamReader reader;
        public IReadOnlyList<string> Header { get; private set; }

        public TinyCsvReader(StreamReader reader)
        {
            this.reader = reader;
            {
                var line = reader.ReadLine();
                if (line == null) throw new InvalidOperationException("Header is null.");

                var index = 0;
                var header = new List<string>();
                while (index < line.Length)
                {
                    var s = GetValue(line, ref index);
                    if (s.Length == 0) break;
                    header.Add(s);
                }
                this.Header = header;
            }
        }

        string GetValue(string line, ref int i)
        {
            var temp = new char[line.Length - i];
            var j = 0;
            for (; i < line.Length; i++)
            {
                if (line[i] == ',')
                {
                    i += 1;
                    break;
                }
                temp[j++] = line[i];
            }

            return new string(temp, 0, j).Trim(trim);
        }

        public string[] ReadValues()
        {
            var line = reader.ReadLine();
            if (line == null) return null;
            if (string.IsNullOrWhiteSpace(line)) return null;

            var values = new string[Header.Count];
            var lineIndex = 0;
            for (int i = 0; i < values.Length; i++)
            {
                var s = GetValue(line, ref lineIndex);
                values[i] = s;
            }
            return values;
        }

        public Dictionary<string, string> ReadValuesWithHeader()
        {
            var values = ReadValues();
            if (values == null) return null;

            var dict = new Dictionary<string, string>();
            for (int i = 0; i < values.Length; i++)
            {
                dict.Add(Header[i], values[i]);
            }

            return dict;
        }

        public void Dispose()
        {
            reader.Dispose();
        }
    }
}
```

Inheritance
---
Currently MasterMemory does not support inheritance. Recommend way to create common method, use interface and extension method. But if you want to create common method with common cached field(made by `OnAfterConstruct`), for workaround, create abstract class and all data properties to abstract.

```csharp
public abstract class FooAndBarBase
{
    // all data properties to virtual
    public virtual int Prop1 { get; protected set; }
    public virtual int Prop2 { get; protected set; }

    [IgnoreMember]
    public int Prop3 => Prop1 + Prop2;

    public IEnumerable<FooAndBarBase> CommonMethod()
    {
        throw new NotImplementedException();
    }
}

[MemoryTable("foo_table"), MessagePackObject(true)]
public class FooTable : FooAndBarBase
{
    [PrimaryKey]
    public override int Prop1 { get; protected set; }
    public override int Prop2 { get; protected set; }
}

[MemoryTable("bar_table"), MessagePackObject(true)]
public class BarTable : FooAndBarBase
{
    [PrimaryKey]
    public override int Prop1 { get; protected set; }
    public override int Prop2 { get; protected set; }
}
```

Optimization
---
When invoking `new MemoryDatabase(byte[] databaseBinary...)`, read and construct database from binary. If binary size is large then construct performance will slow down. `MemoryDatabase` has `ctor(..., int maxDegreeOfParallelism = 1)` option in constructor to construct in parallel.

```csharp
var database = new MemoryDatabase(bin, maxDegreeOfParallelism: Environment.ProcessorCount);
```

The use of Parallel can greatly improve the construct performance. Recommend to use `Environment.ProcessorCount`.

If you want to reduce code size of generated code, Validator and MetaDatabase info can omit in runtime. Generated code has two symbols `DISABLE_MASTERMEMORY_VALIDATOR` and `DISABLE_MASTERMEMORY_METADATABASE`.  By defining them, can be erased from the build code.

Code Generator
---
MasterMemory has two kinds of code-generator. `MSBuild Task`, `.NET Core Global/Local Tools`.

MSBuild Task(`DataTables.MSBuild.Tasks`) is recommended way to use in .NET Core csproj.

```xml
<DataTablesGenerator
    UsingNamespace="string:required"
    InputDirectory="string:required"
    CodeOutputDirectory="string:required"
    DataOutputDirectory="string:required"
    PrefixClassName="string:optional, default= "
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
```

After install, you can call by `dotnet mmgen` command. This is useful to use in CI. Here is the sample of CircleCI config.

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
      - run: dotnet mmgen -i ./ -o ./MasterMemory -n Test
      /* git push or store artifacts or etc...... */
```

License
---
This library is under the MIT License.
