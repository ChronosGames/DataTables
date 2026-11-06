using System;
using System.ComponentModel.DataAnnotations;
using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor : IDisposable
{
    private readonly GenerationContext m_Context;
    private readonly IFormulaEvaluator m_FormulaEvaluator;
    private readonly string m_Tags;
    private readonly ParseOptions m_Options;
    private readonly DiagnosticsCollector m_Diagnostics;

    /// <summary>
    /// 初始数据行序号
    /// </summary>
    private int m_FirstDataRowIndex;

    public DataTableProcessor(GenerationContext context, IFormulaEvaluator formulaEvaluator, string tags)
    {
        m_Context = context;
        m_FormulaEvaluator = formulaEvaluator;
        m_Tags = tags;
        m_Options = new ParseOptions { FilterColumnTags = tags };
        m_Diagnostics = new DiagnosticsCollector();

        m_FirstDataRowIndex = -1;
        ApplyArraySeparatorOptions(m_Options);
    }

    public DiagnosticsCollector Diagnostics => m_Diagnostics;

    public ParseOptions Options => m_Options;

    public DataTableProcessor(GenerationContext context, IFormulaEvaluator formulaEvaluator, ParseOptions options, DiagnosticsCollector diagnostics)
    {
        m_Context = context;
        m_FormulaEvaluator = formulaEvaluator;
        m_Options = options ?? new ParseOptions();
        m_Tags = m_Options.FilterColumnTags;
        m_Diagnostics = diagnostics ?? new DiagnosticsCollector();

        m_FirstDataRowIndex = -1;
        ApplyArraySeparatorOptions(m_Options);
    }

    /// <summary>
    /// 将 <see cref="ParseOptions.ArrayNestedSeparators"/> 应用到 <see cref="ArrayDataProcessor"/>，
    /// 以便后续的数组写入流程按项目级别的分隔符配置工作。
    /// </summary>
    private static void ApplyArraySeparatorOptions(ParseOptions options)
    {
        ArrayDataProcessor.NestedSeparators = string.IsNullOrEmpty(options.ArrayNestedSeparators)
            ? null
            : options.ArrayNestedSeparators;
    }

    public void CreateGenerationContext(ISheet sheet)
    {
        m_FirstDataRowIndex = new TableSchemaService(m_Context, m_Options, m_Diagnostics).CreateGenerationContext(sheet);
    }

    private string GetCellString(ICell? cell)
    {
        if (cell == null)
        {
            return string.Empty;
        }

        return GetCellString(cell, cell.CellType);
    }

    private string GetCellString(ICell cell, CellType cellType)
    {
        switch (cellType)
        {
            case CellType.Blank:
                return string.Empty;
            case CellType.Numeric:
            {
                // 如果单元格为数字类型，根据单元格的样式格式化成字符串
                if (DateUtil.IsCellDateFormatted(cell))
                {
                    return cell.DateCellValue?.ToString("yyyy-MM-dd HH:mm:ss") ?? string.Empty;
                }
                else
                {
                    return cell.NumericCellValue.ToString();

                }
            }
            case CellType.String:
                return cell.StringCellValue.Trim();
            case CellType.Boolean:
                return cell.BooleanCellValue ? "TRUE" : "FALSE";
            case CellType.Formula:
            {
                var oldPlainText = GetCellString(cell, cell.CachedFormulaResultType);
                ValidateFormulaCellString(cell, oldPlainText);
                return oldPlainText;
            }
            case CellType.Error:
                throw new FormatException(FormulaError.ForInt(cell.ErrorCellValue).String);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    public bool ValidateGenerationContext()
    {
        return new TableSchemaValidator(m_Context, m_Options).Validate(m_FirstDataRowIndex);
    }

    private void ValidateFormulaCellString(ICell cell, string value)
    {
        if (!m_Options.ValidateFormulaConsistency || m_Options.FormulaPolicy == FormulaEvaluationPolicy.Off)
        {
            return;
        }

        CellValue? result;
        try
        {
            result = m_FormulaEvaluator.Evaluate(cell);
        }
        catch (Exception)
        {
            return;
        }

        switch (result.CellType)
        {
            case CellType.Numeric:
            {
                // 如果单元格为数字类型，根据单元格的样式格式化成字符串
                if (result.NumberValue.ToString() != value)
                {
                    throw new ValidationException($"出现公式列文本不一致: {value} != {result.NumberValue}");
                }
                return;
            }
            case CellType.String:
            {
                if (result.StringValue.Trim() != value)
                {
                    throw new ValidationException($"出现公式列文本不一致: {value} != {result.StringValue}");
                }
                return;
            }
            case CellType.Boolean:
            {
                if (result.BooleanValue ^ kBoolMap[value])
                {
                    throw new ValidationException($"出现公式列文本不一致: {value} != {result.BooleanValue}");
                }

                return;
            }
        }
    }


    internal TableDataSnapshot MaterializeData(ISheet sheet)
    {
        var descriptor = GetTableTypeDescriptor();
        var snapshot = descriptor.DataMaterializer.Materialize(new TableDataMaterializationContext(
            m_Context,
            sheet,
            m_FirstDataRowIndex,
            m_Diagnostics,
            GetCellString));
        var metrics = m_Diagnostics.GetMetrics(m_Context.FileName, m_Context.SheetName);
        metrics.SnapshotRowCount = snapshot.Rows.Count;
        metrics.SnapshotEstimatedBytes = snapshot.EstimatedSizeInBytes;
        return snapshot;
    }

    internal bool ValidateData(TableDataSnapshot snapshot)
    {
        var descriptor = GetTableTypeDescriptor();
        var validationContext = new TableDataValidationContext(m_Context, snapshot, m_Diagnostics);
        var errorCount = m_Diagnostics.ErrorCount;
        foreach (var validationPass in descriptor.ValidationPasses)
        {
            validationPass.Validate(validationContext);
            if (m_Diagnostics.ErrorCount > errorCount)
            {
                return false;
            }
        }

        return true;
    }

    internal void GenerateDataFile(string outputDir, string comparisonOutputDir, bool forceOverwrite, TableDataSnapshot snapshot, ILogger logger)
    {
        var descriptor = GetTableTypeDescriptor();
        new DataTableBinaryWriter(m_Context, descriptor.SerializationPlan)
            .GenerateDataFile(outputDir, comparisonOutputDir, forceOverwrite, snapshot, logger);
    }

    private TableTypeDescriptor GetTableTypeDescriptor()
    {
        if (!TableTypeDescriptorRegistry.Default.TryGetDescriptor(m_Context.DataSetType, out var descriptor))
        {
            throw new InvalidOperationException($"未注册 DTGen={m_Context.DataSetType} 的表类型描述符。");
        }

        return descriptor;
    }

    internal static DataProcessor GetDataProcessorForSerialization(string type)
    {
        return DataProcessorUtility.GetDataProcessor(type);
    }

    public static string GetDeserializeMethodString(GenerationContext context, XField property)
    {
        var processor = DataProcessorUtility.GetDataProcessor(property.TypeName);
        return processor.GenerateDeserializeCode(context, processor.Type.Name, property.Name, 0);
    }

    /// <summary>
    /// 生成字段的类型定义文本
    /// </summary>
    /// <param name="property"></param>
    /// <returns></returns>
    public static string GetLanguageKeyword(XField property)
    {
        var processor = DataProcessorUtility.GetDataProcessor(property.TypeName);
        return processor.LanguageKeyword;
    }

    /// <summary>
    /// 生成字段的类型值定义文本
    /// </summary>
    /// <param name="field"></param>
    /// <param name="text"></param>
    /// <returns></returns>
    public static string GetLanguageValue(XField field, string text)
    {
        return DataProcessorUtility.GetDataProcessor(field.TypeName).GenerateTypeValue(text);
    }

    /// <summary>
    /// 计算7BitEncodedInt编码后的字节数
    /// </summary>
    internal static int GetCompactIntSize(int value)
    {
        if (value < 0x80) return 1;
        if (value < 0x4000) return 2;
        if (value < 0x200000) return 3;
        if (value < 0x10000000) return 4;
        return 5;
    }

    public void Dispose() { }
}
