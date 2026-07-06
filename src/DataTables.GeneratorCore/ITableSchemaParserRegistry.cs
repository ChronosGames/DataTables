using System.Collections.Generic;

namespace DataTables.GeneratorCore;

public interface ITableSchemaParserRegistry
{
    /// <summary>
    /// 当前已注册且可实际解析的 DTGen 类型。
    /// </summary>
    IReadOnlyCollection<string> SupportedDataSetTypes { get; }

    /// <summary>
    /// 已预留给未来扩展的 DTGen 类型。
    /// </summary>
    IReadOnlyCollection<string> ReservedDataSetTypes { get; }

    /// <summary>
    /// 按 DTGen 类型查找解析器。
    /// </summary>
    bool TryGetParser(string dataSetType, out ITableSchemaParser parser);
}
