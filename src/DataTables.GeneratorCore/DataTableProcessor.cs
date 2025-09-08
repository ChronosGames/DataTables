using System;
using System.ComponentModel.DataAnnotations;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

public sealed partial class DataTableProcessor : IDisposable
{
    private static readonly Regex NameRegex = new Regex(@"^[A-Za-z][A-Za-z0-9_]*$");
    private const string DATA_TABLE_SIGNATURE = "DTABLE";
    private const int DATA_TABLE_VERSION = 2;

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
    }

    public void CreateGenerationContext(ISheet sheet)
    {
        var sw = Stopwatch.StartNew();
        // 定位首个有效行并解析表头
        int headerRowIndex = ParserUtils.GetFirstValidRowIndex(sheet);
        if (headerRowIndex == -1)
        {
            m_Diagnostics.Warn(m_Context.FileName, m_Context.SheetName, string.Empty, "未找到有效表头行");
            return;
        }

        var headerRow = sheet.GetRow(headerRowIndex);
        ParseSheetInfoRow(GetCellString(headerRow.GetCell(headerRow.FirstCellNum)));

        // 按模式选择解析器
        ITableSchemaParser parser = m_Context.DataSetType == "matrix"
            ? new MatrixTableParser()
            : m_Context.DataSetType == "column" ? new ColumnTableParser() : new RowTableParser();
        // 使用抽象 Reader 包装 NPOI Sheet
        int nextIndex = parser.Parse(new NpoiSheetReader(sheet), m_Context, m_Options, m_Diagnostics);
        if (nextIndex == -1)
        {
            m_Diagnostics.Warn(m_Context.FileName, m_Context.SheetName, string.Empty, "表头或字段信息不完整，解析终止");
            return;
        }

        // 不同模式的 nextIndex 语义不同；column 返回字段起始行，其余返回数据起始行
        m_FirstDataRowIndex = m_Context.DataSetType == "column" ? nextIndex : nextIndex;

        sw.Stop();
        m_Diagnostics.GetMetrics(m_Context.FileName, m_Context.SheetName).ParseElapsedMs += sw.ElapsedMilliseconds;
    }

    private static bool ValidRow(IRow? row)
    {
        if (row == null)
        {
            return false;
        }

        if (row.FirstCellNum < 0)
        {
            return false;
        }

        if (row.FirstCellNum == row.LastCellNum)
        {
            return false;
        }

        // 是否空行
        bool found = false;
        for (int i = row.FirstCellNum; i < row.LastCellNum; i++)
        {
            var cell0 = row.GetCell(i);
            if (cell0 != null && cell0.CellType != CellType.Blank)
            {
                found = true;
                break;
            }
        }
        if (!found)
        {
            return false;
        }

        return true;
    }

    private static int FindNextValidRowIndex(ISheet sheet, int startExclusive)
    {
        for (int i = startExclusive + 1; i <= sheet.LastRowNum; i++)
        {
            if (ValidRow(sheet.GetRow(i)))
            {
                return i;
            }
        }
        return -1;
    }

    private static int GetFirstValidRowIndex(ISheet sheet)
    {
        return FindNextValidRowIndex(sheet, sheet.FirstRowNum - 1);
    }

    // 是否有效行
    private bool IgnoreDataRow(IRow? row)
    {
        if (row == null || row.FirstCellNum < 0)
        {
            return false;
        }

        // 此行是否被注释
        foreach (var field in m_Context.Fields)
        {
            if (!field.IsComment)
            {
                continue;
            }

            var cellString = GetCellString(row.GetCell(field.Index));
            if (!string.IsNullOrEmpty(cellString) && cellString == "#")
            {
                return false;
            }
        }

        // 是否空行
        foreach (var field in m_Context.Fields)
        {
            if (field.IsIgnore) { continue; }

            if (!string.IsNullOrEmpty(GetCellString(row.GetCell(field.Index))))
            {
                return true;
            }
        }

        return false;
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
                    return ((DateTime)cell.DateCellValue).ToString("yyyy-MM-dd HH:mm:ss");
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
                throw new Exception(FormulaError.ForInt(cell.ErrorCellValue).String);
            default:
                throw new ArgumentOutOfRangeException();
        }
    }

    private static string GetCellNote(ICell cell)
    {
        if (cell.CellComment == null)
        {
            return string.Empty;
        }

        var author = cell.CellComment.Author;
        var plain = cell.CellComment.String.String;
        if (plain.StartsWith($"{author}:\n", StringComparison.Ordinal))
        {
            return plain.Substring(author.Length + 2);
        }
        else if (plain.StartsWith($"{author}:\r\n", StringComparison.Ordinal))
        {
            return plain.Substring(author.Length + 3);
        }

        return plain;
    }

    public bool ValidateGenerationContext()
    {
        if (string.IsNullOrEmpty(m_Context.ClassName))
        {
            return false;
        }

        // 是否存在有效列
        if (m_Context.Fields.All(x => x.IsIgnore))
        {
            return false;
        }

        if (m_FirstDataRowIndex == -1)
        {
            throw new Exception("表格头部信息不全");
        }

        // 规范化并检查索引配置（大小写不敏感匹配到实际字段名）
        var fieldMap = m_Context.Fields
            .Where(f => !string.IsNullOrEmpty(f.Name))
            .GroupBy(f => f.Name, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        for (int idx = 0; idx < m_Context.Indexs.Count; idx++)
        {
            var index = m_Context.Indexs[idx];
            for (int i = 0; i < index.Length; i++)
            {
                var rawName = (index[i] ?? string.Empty).Trim();
                if (!fieldMap.TryGetValue(rawName, out var field))
                {
                    // 检查是否存在但被忽略的字段
                    var ignoredField = m_Context.Fields.FirstOrDefault(f =>
                        string.Equals(f.Name, rawName, StringComparison.OrdinalIgnoreCase) ||
                        (!string.IsNullOrEmpty(f.Title) && string.Equals(f.Title, rawName, StringComparison.OrdinalIgnoreCase)));

                    if (ignoredField != null && ignoredField.IsIgnore)
                    {
                        var reason = ignoredField.IsTagFiltered ? $"因标签过滤被忽略（当前过滤标签: {m_Options.FilterColumnTags}）" :
                                   ignoredField.IsComment ? "为注释列被忽略" : "被忽略";
                        throw new Exception($"Index配置引用了被忽略的字段: {rawName} ({reason})。请调整标签过滤参数或修改Index配置。");
                    }

                    var available = string.Join(", ", m_Context.Fields.Where(f => !string.IsNullOrEmpty(f.Name)).Select(f => f.Name));
                    throw new Exception($"Index配置中发现不存在的字段: {rawName}. 可用字段: [{available}]");
                }
                if (field.IsIgnore)
                {
                    var reason = field.IsTagFiltered ? $"因标签过滤被忽略（当前过滤标签: {m_Options.FilterColumnTags}）" : field.IsComment ? "为注释列被忽略" : "被忽略";
                    throw new Exception($"Index配置引用了被忽略的字段: {rawName} ({reason})。请调整标签过滤参数或修改Index配置。");
                }
                // 用实际字段名回填，确保后续代码生成一致
                if (!string.Equals(index[i], field.Name, StringComparison.Ordinal))
                {
                    index[i] = field.Name;
                }
            }
        }

        // 规范化并检查分组配置（大小写不敏感匹配到实际字段名）
        for (int g = 0; g < m_Context.Groups.Count; g++)
        {
            var group = m_Context.Groups[g];
            for (int i = 0; i < group.Length; i++)
            {
                var rawName = (group[i] ?? string.Empty).Trim();
                if (!fieldMap.TryGetValue(rawName, out var field))
                {
                    var available = string.Join(", ", m_Context.Fields.Where(f => !string.IsNullOrEmpty(f.Name)).Select(f => f.Name));
                    throw new Exception($"Group配置中发现不存在的字段: {rawName}. 可用字段: [{available}]");
                }
                if (field.IsIgnore)
                {
                    var reason = field.IsTagFiltered ? "(因标签过滤被忽略)" : field.IsComment ? "(为注释列被忽略)" : "(被忽略)";
                    throw new Exception($"Group配置引用了被忽略的字段: {rawName} {reason}");
                }
                if (!string.Equals(group[i], field.Name, StringComparison.Ordinal))
                {
                    group[i] = field.Name;
                }
            }
        }

        return true;
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
        catch (Exception e)
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


    // 解析第一行的表头信息
    private bool ParseSheetInfoRow(string cellString)
    {
        var arr = cellString.Split(',');
        foreach (var pair in arr)
        {
            var args = pair.Split('=');
            if (args.Length == 2)
            {
                switch (args[0].Trim().ToLower())
                {
                    case "dtgen":
                        m_Context.DataSetType = args[1].Trim().ToLower();
                        break;
                    case "title":
                        m_Context.Title = args[1].Trim();
                        break;
                    case "class":
                        m_Context.ClassName = args[1].Trim();
                        break;
                    case "disabletagsfilter":
                        m_Context.DisableTagsFilter = bool.Parse(args[1].Trim());
                        break;
                    case "index":
                    {
                        var fields = args[1]
                            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        m_Context.Indexs.Add(fields);
                        break;
                    }
                    case "group":
                    {
                        var fields = args[1]
                            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        m_Context.Groups.Add(fields);
                        break;
                    }
                    case "priority":
                        // 支持在首行通过 priority=Critical|Normal|Lazy 指定表预热优先级（大小写不敏感）
                        {
                            var raw = args[1].Trim();
                            var norm = raw.Equals("critical", StringComparison.OrdinalIgnoreCase) ? "Critical"
                                : raw.Equals("normal", StringComparison.OrdinalIgnoreCase) ? "Normal"
                                : raw.Equals("lazy", StringComparison.OrdinalIgnoreCase) ? "Lazy"
                                : "Normal";
                            m_Context.Priority = norm;
                        }
                        break;
                    case "child":
                        m_Context.Child = args[1].Trim();
                        break;
                    case "matrix":
                    {
                        var fields = args[1]
                            .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
                        m_Context.Fields =
                        [
                            new XField(0) { Name = DataMatrixTemplate.kKey1, TypeName = fields[0] },
                            new XField(1) { Name = DataMatrixTemplate.kKey2, TypeName = fields[1] },
                            new XField(2) { Name = DataMatrixTemplate.kValue, TypeName = fields[2] },
                        ];
                        break;
                    }
                    case "matrixdefaultvalue":
                    {
                        m_Context.MatrixDefaultValue = args[1].Trim();
                        break;
                    }
                }
            }
            else if (args.Length == 1)
            {
                switch (args[0].Trim().ToLower())
                {
                    case "disabletagsfilter":
                        m_Context.DisableTagsFilter = true;
                        break;
                }
            }
        }

        return !string.IsNullOrEmpty(m_Context.ClassName);
    }

    private void ParseMatrixInfo(IRow row)
    {
        for (int cellNum = row.FirstCellNum; cellNum < row.LastCellNum; cellNum++)
        {
            var cellString = GetCellString(row.GetCell(cellNum));
            if (string.IsNullOrEmpty(cellString))
            {
                continue;
            }

            m_Context.ColumnIndexToKey2.Add(cellNum, cellString);
        }
    }

    private void ParseFieldCommentRow(IRow row)
    {
        m_Context.Fields = new XField[row.LastCellNum - row.FirstCellNum + 1];

        for (int i = 0; i <= row.LastCellNum - row.FirstCellNum; i++)
        {
            var field = new XField(i + row.FirstCellNum);
            m_Context.Fields[i] = field;

            // 修正列名行文本为空时解析报错
            var cell = row.GetCell(i + row.FirstCellNum);
            var text = GetCellString(cell);
            if (string.IsNullOrEmpty(text) || text.TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                field.IsIgnore = true;
                field.IsComment = !string.IsNullOrEmpty(text) && text.Trim() == "#行注释标志";
                continue;
            }

            // 是否允许导出
            if (!m_Context.DisableTagsFilter)
            {
                var index = text.LastIndexOf('@');
                if (index != -1)
                {
                    var tagText = text.Substring(index + 1);
                    if (!string.IsNullOrEmpty(m_Tags) && !TagFilterUtils.Evaluate(tagText, m_Tags))
                    {
                        field.IsIgnore = true;
                        field.IsTagFiltered = true;
                        continue;
                    }
                    text = text.Substring(0, index);
                }
            }

            field.Title = text;
            field.Note = GetCellNote(cell);
        }
    }

    private void ParseFieldNameRow(IRow row)
    {
        foreach (var field in m_Context.Fields)
        {
            if (field.IsIgnore)
            {
                continue;
            }

            var text = GetCellString(row.GetCell(field.Index));

            // 如果字段名为空或是注释，标记为忽略但不抛出异常
            if (string.IsNullOrEmpty(text) || text.TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                field.IsIgnore = true;
                field.IsComment = !string.IsNullOrEmpty(text) && text.Trim().StartsWith("#");
                continue;
            }

            if (!NameRegex.IsMatch(text))
            {
                throw new FormatException($"数据列名称不合法: {text}");
            }

            field.Name = text;
        }
    }

    private void ParseFieldTypeRow(IRow row)
    {
        foreach (var field in m_Context.Fields)
        {
            if (field.IsIgnore)
            {
                continue;
            }

            var text = GetCellString(row.GetCell(field.Index));
            field.TypeName = text;
        }
    }

    internal void GenerateDataFile(string filePath, string outputDir, bool forceOverwrite, ISheet sheet, ILogger logger)
    {
        int startTickCount = Environment.TickCount;
        string outputFileName = Path.Combine(outputDir, m_Context.GetDataOutputFilePath());

        // 判断是否存在配置表变更（以修改时间为准），若不存在则直接跳过
        if (!forceOverwrite)
        {
            var processPath = Process.GetCurrentProcess().MainModule!.FileName;
            var processLastWriteTime = File.GetLastWriteTime(processPath);
            var excelLastWriteTime = File.GetLastWriteTime(filePath);

            if (File.Exists(outputFileName))
            {
                var dataLastWriteTime = File.GetLastWriteTime(outputFileName);
                if (dataLastWriteTime > excelLastWriteTime && dataLastWriteTime > processLastWriteTime)
                {
                    // 标记为跳过
                    m_Context.Skiped = true;

                    logger.Debug("  > Generate {0}.bytes to: {1} (skiped) - {2}ms", m_Context.DataRowClassName, outputFileName, Environment.TickCount - startTickCount);
                    return;
                }
            }
        }

        try
        {
            using var fileStream = new FileStream(outputFileName, FileMode.Create, FileAccess.Write, FileShare.None, bufferSize: 4096, useAsync: true);
            using var binaryWriter = new BinaryWriter(fileStream, Encoding.UTF8, leaveOpen: true);

            // 写入数据表签名
            binaryWriter.Write(DATA_TABLE_SIGNATURE);

            // 写入数据表版本
            binaryWriter.Write(DATA_TABLE_VERSION);

            // 写入行数占位
            binaryWriter.Write(ushort.MinValue);

            // 写入数据集
            int dataRowCount = WriteDataRows(sheet, binaryWriter);

            // 重写行数 (跳过签名和版本，定位到行数位置)
            // String写入格式: 7BitEncodedInt(length) + UTF8字节
            byte[] signatureBytes = Encoding.UTF8.GetBytes(DATA_TABLE_SIGNATURE);
            long countPosition = GetCompactIntSize(signatureBytes.Length) + signatureBytes.Length + sizeof(int);
            fileStream.Seek(countPosition, SeekOrigin.Begin);
            binaryWriter.Write((ushort)dataRowCount);

            logger.Debug("  > Generate {0}.bytes to: {1}. - {2}ms", m_Context.DataRowClassName, outputFileName, Environment.TickCount - startTickCount);
        }
        catch (Exception exception)
        {
            // 记录出错日志
            logger.Error("  > Generate {0}.bytes failure, exception is '{1}'. - {2}ms", m_Context.DataRowClassName, exception, Environment.TickCount - startTickCount);
            Console.ResetColor();

            // 记录出错的情况
            m_Context.Failed = true;

            // 删除旧文件
            if (File.Exists(outputFileName))
            {
                File.Delete(outputFileName);
            }
        }
    }

    private int WriteDataRows(ISheet sheet, BinaryWriter writer)
    {
        int dataRowCount = 0;

        if (m_Context.DataSetType == "column")
        {
            // 计算最大列号
            int maxLastCellNum = 0;
            for (int r = m_FirstDataRowIndex; r <= sheet.LastRowNum; r++)
            {
                var row = sheet.GetRow(r);
                if (row == null) continue;
                if (row.LastCellNum > maxLastCellNum) maxLastCellNum = row.LastCellNum;
            }

            // 若存在列注释标记行，则记录其行索引
            if (m_Context.ColumnCommentRowIndex >= 0)
            {
                // 已设置则使用
            }

            for (int c = m_Context.ColumnFirstDataColIndex; c < maxLastCellNum; c++)
            {
                // 列注释行：以 # 开头的单元格跳过该列
                if (m_Context.ColumnCommentRowIndex >= 0)
                {
                    var markRow = sheet.GetRow(m_Context.ColumnCommentRowIndex);
                    var mk = GetCellString(markRow?.GetCell(c));
                    if (!string.IsNullOrEmpty(mk) && mk.TrimStart().StartsWith("#", StringComparison.Ordinal))
                    {
                        m_Diagnostics.GetMetrics(m_Context.FileName, m_Context.SheetName).SkippedColumnCount++;
                        continue;
                    }
                }

                // 若整列为空，则跳过
                bool hasAnyValue = false;
                foreach (var field in m_Context.Fields)
                {
                    if (field.IsIgnore) { continue; }
                    var row = sheet.GetRow(field.Index);
                    var text = GetCellString(row?.GetCell(c));
                    if (!string.IsNullOrEmpty(text))
                    {
                        hasAnyValue = true;
                        break;
                    }
                }
                if (!hasAnyValue)
                {
                    continue;
                }

                dataRowCount += WriteColumnBytes(writer, sheet, c);
            }
        }
        else
        {
            for (int i = m_FirstDataRowIndex; i <= sheet.LastRowNum; i++)
            {
                var row = sheet.GetRow(i);
                if (!IgnoreDataRow(row))
                {
                    continue;
                }

                dataRowCount += WriteRowBytes(writer, row);
            }
        }

        return dataRowCount;
    }

    /// <summary>
    /// 将指定数据行写入目标流中
    /// </summary>
    /// <param name="writer"></param>
    /// <param name="row"></param>
    /// <returns>是否写入该行的数据</returns>
    /// <exception cref="Exception"></exception>
    private int WriteRowBytes(BinaryWriter writer, IRow row)
    {
        if (m_Context.DataSetType == "matrix")
        {
            var processor1 = DataProcessorUtility.GetDataProcessor(m_Context.GetField(DataMatrixTemplate.kKey1)!.TypeName);
            var processor2 = DataProcessorUtility.GetDataProcessor(m_Context.GetField(DataMatrixTemplate.kKey2)!.TypeName);
            var processor3 = DataProcessorUtility.GetDataProcessor(m_Context.GetField(DataMatrixTemplate.kValue)!.TypeName);

            int dataRowCount = 0;

            foreach (var pair in m_Context.ColumnIndexToKey2)
            {
                var cellString = GetCellString(row.GetCell(pair.Key));
                if (string.IsNullOrEmpty(cellString) || cellString == m_Context.MatrixDefaultValue)
                {
                    if (cellString == m_Context.MatrixDefaultValue)
                    {
                        m_Diagnostics.GetMetrics(m_Context.FileName, m_Context.SheetName).MatrixDefaultSkippedCount++;
                    }
                    continue;
                }

                // 累计数量
                dataRowCount++;

                // 写入TKey1的值
                try
                {
                    processor1.WriteToStream(writer, GetCellString(row.GetCell(row.FirstCellNum)));
                }
                catch (Exception e)
                {
                    throw new Exception($"解析单元格内容时出错: {GetRowColString(row.RowNum, row.FirstCellNum)}", e);
                }

                // 写入TKey2的值
                try
                {
                    processor2.WriteToStream(writer, pair.Value);
                }
                catch (Exception e)
                {
                    throw new Exception($"解析单元格内容时出错: {GetRowColString(1, pair.Key)}", e);
                }

                // 写入TValue值
                try
                {
                    processor3.WriteToStream(writer, cellString);
                }
                catch (Exception e)
                {
                    throw new Exception($"解析单元格内容时出错: {GetRowColString(row.RowNum, pair.Key)}", e);
                }
            }

            return dataRowCount;
        }
        else
        {
            foreach (var field in m_Context.Fields)
            {
                if (field.IsIgnore) { continue; }

                var processor = DataProcessorUtility.GetDataProcessor(field.TypeName);
                try
                {
                    processor.WriteToStream(writer, GetCellString(row.GetCell(field.Index)));
                }
                catch (Exception e)
                {
                    throw new Exception($"解析单元格内容时出错: {GetRowColString(row.RowNum, field.Index)}", e);
                }
            }

            return 1;
        }
    }

    private int WriteColumnBytes(BinaryWriter writer, ISheet sheet, int columnIndex)
    {
        foreach (var field in m_Context.Fields)
        {
            if (field.IsIgnore) { continue; }

            var processor = DataProcessorUtility.GetDataProcessor(field.TypeName);
            try
            {
                var row = sheet.GetRow(field.Index);
                var cell = row?.GetCell(columnIndex);
                processor.WriteToStream(writer, GetCellString(cell));
            }
            catch (Exception e)
            {
                throw new Exception($"解析单元格内容时出错: {GetRowColString(field.Index, columnIndex)}", e);
            }
        }

        return 1;
    }

    private void ParseColumnFields(ISheet sheet, int startRowIndex)
    {
        // 统计有效字段行数量，建立 Fields 数组
        var fields = new System.Collections.Generic.List<XField>();

        for (int r = startRowIndex; r <= sheet.LastRowNum; r++)
        {
            var row = sheet.GetRow(r);
            if (!ValidRow(row))
            {
                continue;
            }

            // 检测列注释标记行
            var nameCellText = GetCellString(row!.GetCell(row.FirstCellNum + 1));
            if (!string.IsNullOrEmpty(nameCellText) && nameCellText.Trim() == "#列注释标志")
            {
                m_Context.ColumnCommentRowIndex = r;
                continue;
            }

            // A:Title
            var titleCell = row.GetCell(row.FirstCellNum + 0);
            var titleText = GetCellString(titleCell);
            var field = new XField(r)
            {
                Title = titleText,
                Note = titleCell != null ? GetCellNote(titleCell) : string.Empty,
            };

            // 标签过滤
            if (!m_Context.DisableTagsFilter)
            {
                var idx = titleText.LastIndexOf('@');
                if (idx != -1)
                {
                    var tagText = titleText.Substring(idx + 1);
                    if (!string.IsNullOrEmpty(m_Tags) && !TagFilterUtils.Evaluate(tagText, m_Tags))
                    {
                        field.IsIgnore = true;
                        field.IsTagFiltered = true;
                    }
                    else
                    {
                        field.Title = titleText.Substring(0, idx);
                    }
                }
            }

            // B:Name
            var nameText = GetCellString(row.GetCell(row.FirstCellNum + 1));
            if (string.IsNullOrEmpty(nameText) || nameText.TrimStart().StartsWith("#", StringComparison.Ordinal))
            {
                field.IsIgnore = true;
                field.IsComment = !string.IsNullOrEmpty(nameText) && nameText.Trim() == "#行注释标志";
            }
            else
            {
                if (!NameRegex.IsMatch(nameText))
                {
                    throw new FormatException($"数据列名称不合法: {nameText}");
                }
                field.Name = nameText;
            }

            // C:Type
            var typeText = GetCellString(row.GetCell(row.FirstCellNum + 2));
            field.TypeName = typeText;

            fields.Add(field);
        }

        m_Context.Fields = fields.ToArray();
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

    private static string GetRowColString(int row, int col)
    {
        return string.Format("{0}{1}", ConvertToDigit(col), row + 1);
    }

    private static string ConvertToDigit(int num)
    {
        if (num < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(num));
        }
        else if (num < 26)
        {
            return Convert.ToString((char)('A' + num));
        }
        else
        {
            return ConvertToDigit(num / 26 - 1) + ConvertToDigit(num % 26);
        }
    }

    /// <summary>
    /// 计算7BitEncodedInt编码后的字节数
    /// </summary>
    private static int GetCompactIntSize(int value)
    {
        if (value < 0x80) return 1;
        if (value < 0x4000) return 2;
        if (value < 0x200000) return 3;
        if (value < 0x10000000) return 4;
        return 5;
    }

    public void Dispose() { }
}
