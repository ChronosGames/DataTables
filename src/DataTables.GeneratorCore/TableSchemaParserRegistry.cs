using System;
using System.Collections.Generic;
using System.Linq;

namespace DataTables.GeneratorCore;

public sealed class TableSchemaParserRegistry : ITableSchemaParserRegistry
{
    private readonly Dictionary<string, ITableSchemaParser> m_Parsers;
    private readonly IReadOnlyCollection<string> m_ReservedDataSetTypes;

    public TableSchemaParserRegistry(IEnumerable<ITableSchemaParser> parsers)
    {
        ArgumentNullException.ThrowIfNull(parsers);

        m_Parsers = parsers.ToDictionary(
            parser => parser.DataSetType,
            parser => parser,
            StringComparer.OrdinalIgnoreCase);
        m_ReservedDataSetTypes = TableTypeDescriptorRegistry.Default.ReservedDataSetTypes
            .Where(dataSetType => !m_Parsers.ContainsKey(dataSetType))
            .ToArray();
    }

    public static ITableSchemaParserRegistry CreateDefault()
    {
        return new TableSchemaParserRegistry(TableTypeDescriptorRegistry.Default.Descriptors.Select(descriptor => descriptor.SchemaParser));
    }

    public IReadOnlyCollection<string> SupportedDataSetTypes => m_Parsers.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

    public IReadOnlyCollection<string> ReservedDataSetTypes => m_ReservedDataSetTypes;

    public bool TryGetParser(string dataSetType, out ITableSchemaParser parser)
    {
        return m_Parsers.TryGetValue(dataSetType, out parser!);
    }
}
