using System;
using System.Collections.Generic;
using System.Linq;

namespace DataTables.GeneratorCore;

public sealed class TableSchemaParserRegistry : ITableSchemaParserRegistry
{
    private static readonly string[] s_ReservedDataSetTypes =
    [
        "localized",
        "tree",
        "partitioned",
        "versioned",
        "patch"
    ];

    private readonly Dictionary<string, ITableSchemaParser> m_Parsers;

    public TableSchemaParserRegistry(IEnumerable<ITableSchemaParser> parsers)
    {
        ArgumentNullException.ThrowIfNull(parsers);

        m_Parsers = parsers.ToDictionary(
            parser => parser.DataSetType,
            parser => parser,
            StringComparer.OrdinalIgnoreCase);
    }

    public static ITableSchemaParserRegistry CreateDefault()
    {
        return new TableSchemaParserRegistry(
        [
            new RowTableParser(),
            new MatrixTableParser(),
            new ColumnTableParser(),
            new KvTableParser(),
            new TreeTableParser(),
            new GraphTableParser()
        ]);
    }

    public IReadOnlyCollection<string> SupportedDataSetTypes => m_Parsers.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyCollection<string> ReservedDataSetTypes => s_ReservedDataSetTypes;

    public bool TryGetParser(string dataSetType, out ITableSchemaParser parser)
    {
        return m_Parsers.TryGetValue(dataSetType, out parser!);
    }
}
