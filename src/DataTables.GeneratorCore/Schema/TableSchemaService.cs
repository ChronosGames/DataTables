using System.Diagnostics;
using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

public sealed class TableSchemaService : ITableSchemaService
{
    private static readonly ITableSchemaParserRegistry s_TableSchemaParserRegistry = TableSchemaParserRegistry.CreateDefault();

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

        if (!TagFilterUtils.TryValidateTagSet(m_Context.TableTags, out var tagSetError))
        {
            m_Context.Failed = true;
            m_Diagnostics.Error(m_Context.FileName, m_Context.SheetName, "A1", tagSetError);
            return -1;
        }

        if (!m_Context.DisableTagsFilter && !string.IsNullOrWhiteSpace(m_Options.FilterColumnTags))
        {
            if (!TagFilterUtils.TryValidateExpression(m_Options.FilterColumnTags, out var expressionError))
            {
                m_Context.Failed = true;
                m_Diagnostics.Error(m_Context.FileName, m_Context.SheetName, "A1", expressionError);
                return -1;
            }

            if (!string.IsNullOrWhiteSpace(m_Context.TableTags)
                && !TagFilterUtils.Evaluate(m_Context.TableTags, m_Options.FilterColumnTags))
            {
                m_Context.Skiped = true;
                return -1;
            }
        }

        if (!s_TableSchemaParserRegistry.TryGetParser(m_Context.DataSetType, out var parser))
        {
            var supportedTypes = string.Join(", ", s_TableSchemaParserRegistry.SupportedDataSetTypes);
            var reservedTypes = string.Join(", ", s_TableSchemaParserRegistry.ReservedDataSetTypes);
            m_Diagnostics.Error(
                m_Context.FileName,
                m_Context.SheetName,
                "A1",
                $"未知 DTGen 类型。文件: {m_Context.FileName}, Sheet: {m_Context.SheetName}, 声明值: {m_Context.DataSetType}, 支持的类型: {supportedTypes}, 预留类型: {reservedTypes}");
            return -1;
        }

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
