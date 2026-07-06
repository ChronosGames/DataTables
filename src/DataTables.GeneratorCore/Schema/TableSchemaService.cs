using System.Diagnostics;
using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

public sealed class TableSchemaService : ITableSchemaService
{
    private readonly GenerationContext m_Context;
    private readonly ParseOptions m_Options;
    private readonly DiagnosticsCollector m_Diagnostics;

    public TableSchemaService(GenerationContext context, ParseOptions options, DiagnosticsCollector diagnostics)
    {
        m_Context = context;
        m_Options = options;
        m_Diagnostics = diagnostics;
    }

    public int CreateGenerationContext(ISheet sheet)
    {
        var sw = Stopwatch.StartNew();
        int headerRowIndex = ParserUtils.GetFirstValidRowIndex(sheet);
        if (headerRowIndex == -1)
        {
            m_Diagnostics.Warn(m_Context.FileName, m_Context.SheetName, string.Empty, "未找到有效表头行");
            return -1;
        }

        var headerRow = sheet.GetRow(headerRowIndex);
        SheetInfoParser.Parse(ParserUtils.GetCellString(headerRow.GetCell(headerRow.FirstCellNum)), m_Context);

        if (string.IsNullOrEmpty(m_Context.DataSetType))
        {
            return -1;
        }

        ITableSchemaParser parser = m_Context.DataSetType == "matrix"
            ? new MatrixTableParser()
            : m_Context.DataSetType == "column" ? new ColumnTableParser() : new RowTableParser();

        int nextIndex = parser.Parse(new NpoiSheetReader(sheet), m_Context, m_Options, m_Diagnostics);
        if (nextIndex == -1)
        {
            m_Diagnostics.Warn(m_Context.FileName, m_Context.SheetName, string.Empty, "表头或字段信息不完整，解析终止");
            return -1;
        }

        sw.Stop();
        m_Diagnostics.GetMetrics(m_Context.FileName, m_Context.SheetName).ParseElapsedMs += sw.ElapsedMilliseconds;
        return nextIndex;
    }
}
