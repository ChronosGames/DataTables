using System;
using System.Collections.Generic;
using System.Linq;

namespace DataTables.GeneratorCore;

internal sealed class TableTypeDescriptor
{
    public TableTypeDescriptor(
        string dataSetType,
        ITableSchemaParser schemaParser,
        ITableDataMaterializer dataMaterializer,
        IEnumerable<ITableDataValidationPass> validationPasses,
        ITableDataSerializationPlan serializationPlan,
        ICodeTemplateRenderer codeRenderer)
    {
        DataSetType = dataSetType;
        SchemaParser = schemaParser;
        DataMaterializer = dataMaterializer;
        ValidationPasses = validationPasses.OrderBy(pass => pass.Phase).ToArray();
        SerializationPlan = serializationPlan;
        CodeRenderer = codeRenderer;
    }

    public string DataSetType { get; }

    public ITableSchemaParser SchemaParser { get; }

    public ITableDataMaterializer DataMaterializer { get; }

    public IReadOnlyList<ITableDataValidationPass> ValidationPasses { get; }

    public ITableDataSerializationPlan SerializationPlan { get; }

    public ICodeTemplateRenderer CodeRenderer { get; }
}

internal sealed class TableTypeDescriptorRegistry
{
    private static readonly string[] s_ReservedDataSetTypes =
    [
        "localized",
        "partitioned",
        "versioned",
        "patch",
    ];

    private readonly Dictionary<string, TableTypeDescriptor> m_Descriptors;

    private TableTypeDescriptorRegistry(IEnumerable<TableTypeDescriptor> descriptors)
    {
        m_Descriptors = descriptors.ToDictionary(
            descriptor => descriptor.DataSetType,
            descriptor => descriptor,
            StringComparer.OrdinalIgnoreCase);
    }

    public static TableTypeDescriptorRegistry Default { get; } = CreateDefault();

    public IReadOnlyCollection<TableTypeDescriptor> Descriptors => m_Descriptors.Values
        .OrderBy(descriptor => descriptor.DataSetType, StringComparer.OrdinalIgnoreCase)
        .ToArray();

    public IReadOnlyCollection<string> SupportedDataSetTypes => m_Descriptors.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyCollection<string> ReservedDataSetTypes => s_ReservedDataSetTypes;

    public bool TryGetDescriptor(string dataSetType, out TableTypeDescriptor descriptor)
    {
        return m_Descriptors.TryGetValue(dataSetType, out descriptor!);
    }

    private static TableTypeDescriptorRegistry CreateDefault()
    {
        var cellConversion = new CellConversionValidationPass();
        var serialization = new SnapshotDataSerializationPlan();

        return new TableTypeDescriptorRegistry(
        [
            new TableTypeDescriptor(
                "table",
                new RowTableParser(),
                new TableDataMaterializer(TableDataLayout.Row),
                [cellConversion],
                serialization,
                new DataTableCodeTemplateRenderer("table")),
            new TableTypeDescriptor(
                "matrix",
                new MatrixTableParser(),
                new TableDataMaterializer(TableDataLayout.Matrix),
                [cellConversion],
                serialization,
                new DataMatrixCodeTemplateRenderer()),
            new TableTypeDescriptor(
                "column",
                new ColumnTableParser(),
                new TableDataMaterializer(TableDataLayout.Column),
                [cellConversion],
                serialization,
                new DataTableCodeTemplateRenderer("column")),
            new TableTypeDescriptor(
                "kv",
                new KvTableParser(),
                new TableDataMaterializer(TableDataLayout.KeyValue),
                [cellConversion],
                serialization,
                new KvTableCodeTemplateRenderer()),
            new TableTypeDescriptor(
                "tree",
                new TreeTableParser(),
                new TableDataMaterializer(TableDataLayout.Row),
                [cellConversion, new TreeRowValidationPass(), new TreeRelationshipValidationPass()],
                serialization,
                new TreeTableCodeTemplateRenderer()),
            new TableTypeDescriptor(
                "graph",
                new GraphTableParser(),
                new TableDataMaterializer(TableDataLayout.Row),
                [cellConversion, new GraphRowValidationPass()],
                serialization,
                new GraphTableCodeTemplateRenderer()),
        ]);
    }
}
