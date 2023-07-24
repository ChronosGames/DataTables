using System;
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

    private readonly GenerationContext m_Context;
    private readonly string m_Tags;

    private FileStream? m_FileStream;
    private BinaryWriter? m_BinaryWriter;

    /// <summary>
    /// 当前读取到第几行
    /// </summary>
    private int m_ReadRowIndex;

    private int m_RowTitle;
    private int m_RowFieldComment;
    private int m_RowFieldName;
    private int m_RowFieldType;

    public DataTableProcessor(GenerationContext context, string tags)
    {
        m_Context = context;
        m_Tags = tags;

        m_RowTitle = -1;
        m_RowFieldComment = -1;
        m_RowFieldName = -1;
        m_RowFieldType = -1;
    }

    public void CreateGenerateContext(ISheet sheet)
    {
        for (int i = sheet.FirstRowNum; i <= sheet.LastRowNum; i++)
        {
            var row = sheet.GetRow(i);
            if (!ValidRow(row))
            {
                continue;
            }

            switch (m_ReadRowIndex)
            {
                case 0: // 标题行
                {
                    if (m_RowTitle == -1)
                    {
                        m_RowTitle = row.RowNum;
                    }

                    ParseSheetInfoRow(GetCellString(row.GetCell(row.FirstCellNum)));
                    break;
                }
                case 1: // 字段注释行
                {
                    if (m_Context.DataSetType == "matrix")
                    {

                    }
                    else
                    {
                        if (m_RowFieldComment == -1)
                        {
                            m_RowFieldComment = row.RowNum;
                        }

                        ParseFieldCommentRow(row);
                    }
                    break;
                }
                case 2: // 字段名称行
                {
                    if (m_Context.DataSetType == "matrix")
                    {

                    }
                    else
                    {
                        if (m_RowFieldName == -1)
                        {
                            m_RowFieldName = row.RowNum;
                        }

                        ParseFieldNameRow(row);
                    }
                    break;
                }
                case 3: // 字段类型行
                {
                    if (m_Context.DataSetType == "matrix")
                    {

                    }
                    else
                    {
                        if (m_RowFieldType == -1)
                        {
                            m_RowFieldType = row.RowNum;
                        }

                        ParseFieldTypeRow(row);
                    }
                    break;
                }
            }

            if (++m_ReadRowIndex > 3)
            {
                return;
            }
        }
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

    private bool VaildDataRow(IRow? row)
    {
        if (row == null || row.FirstCellNum < 0)
        {
            return false;
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

    private static string GetCellString(ICell? cell)
    {
        if (cell == null)
        {
            return string.Empty;
        }

        return GetCellString(cell, cell.CellType);
    }

    private static string GetCellString(ICell cell, CellType cellType)
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
                    return cell.DateCellValue.ToString("yyyy-MM-dd HH:mm:ss");
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
                return GetCellString(cell, cell.CachedFormulaResultType);
            case CellType.Error:
                return FormulaError.ForInt(cell.ErrorCellValue).String;
            default:
                return cell.ToString().Trim();
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

    public bool ValidateGenerateContext()
    {
        if (string.IsNullOrEmpty(m_Context.ClassName))
        {
            return false;
        }

        // 是否存在有效列
        if (!m_Context.Fields.Any(x => !x.IsIgnore))
        {
            return false;
        }

        if (m_RowFieldType == -1)
        {
            throw new Exception("表格头部信息不全");
        }

        // 检查是否存在正确的索引配置
        foreach (var index in m_Context.Indexs)
        {
            foreach (var fieldName in index)
            {
                if (!m_Context.Fields.Any(x => !x.IsIgnore && x.Name == fieldName))
                {
                    throw new Exception($"Index配置中发现不存在的字段: {fieldName}");
                }
            }
        }

        // 检查是否存在正确的分组配置
        foreach (var group in m_Context.Groups)
        {
            foreach (var fieldName in group)
            {
                if (!m_Context.Fields.Any(x => !x.IsIgnore && x.Name == fieldName))
                {
                    throw new Exception($"Group配置中发现不存在的字段: {fieldName}");
                }
            }
        }

        return true;
    }

    private static bool ContainTags(string text, string tags)
    {
        for (int i = 0; i < text.Length; i++)
        {
            if (tags.Contains(text[i]))
            {
                return true;
            }
        }

        return false;
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
                        m_Context.DataSetType = args[1].Trim();
                        break;
                    case "title":
                        m_Context.Title = args[1].Trim();
                        break;
                    case "class":
                        m_Context.ClassName = args[1].Trim();
                        break;
                    case "enabletagsfilter":
                        m_Context.EnableTagsFilter = bool.Parse(args[1].Trim());
                        break;
                    case "index":
                        m_Context.Indexs.Add(args[1].Trim().Split('&'));
                        break;
                    case "group":
                        m_Context.Groups.Add(args[1].Trim().Split('&'));
                        break;
                    case "child":
                        m_Context.Child = args[1].Trim();
                        break;
                }
            }
            else if (args.Length == 1)
            {
                switch (args[0].Trim().ToLower())
                {
                    case "enabletagsfilter":
                        m_Context.EnableTagsFilter = true;
                        break;
                }
            }
        }

        return !string.IsNullOrEmpty(m_Context.ClassName);
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
                continue;
            }

            // 是否允许导出
            if (m_Context.EnableTagsFilter)
            {
                var index = text.LastIndexOf('@');
                if (index != -1)
                {
                    if (!string.IsNullOrEmpty(m_Tags) && !ContainTags(text.Substring(index + 1).ToUpper(), m_Tags.ToUpper()))
                    {
                        field.IsIgnore = true;
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
            if (!NameRegex.IsMatch(text))
            {
                field.IsIgnore = true;
                continue;
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

                    logger.Debug("  > Generate {0}.bytes to: {1} (skiped)", m_Context.RealClassName, outputFileName);
                    return;
                }
            }
        }

        try
        {
            using (FileStream fileStream = new FileStream(outputFileName, FileMode.Create, FileAccess.Write))
            {
                using (BinaryWriter binaryWriter = new BinaryWriter(fileStream, Encoding.UTF8))
                {
                    // 写入行数
                    binaryWriter.Write(0);

                    int dataRowCount = 0;
                    for (int i = m_RowFieldType + 1; i <= sheet.LastRowNum; i++)
                    {
                        var row = sheet.GetRow(i);
                        if (!VaildDataRow(row))
                        {
                            continue;
                        }

                        dataRowCount++;
                        WriteRowBytes(binaryWriter, row);
                    }

                    var endPosition = fileStream.Position;
                    fileStream.Position = 0;
                    binaryWriter.Write(dataRowCount);
                    fileStream.Position = endPosition;
                }
            }

            logger.Debug("  > Generate {0}.bytes to: {1}.", m_Context.RealClassName, outputFileName);
        }
        catch (Exception exception)
        {
            // 记录出错日志
            logger.Error("  > Generate {0}.bytes failure, exception is '{1}'.", m_Context.RealClassName, exception);
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

    private void WriteRowBytes(BinaryWriter writer, IRow row)
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
    }

    public static string GetDeserializeMethodString(GenerationContext context, XField property)
    {
        var processor = DataProcessorUtility.GetDataProcessor(property.TypeName);
        return processor.GenerateDeserializeCode(context, processor.Type.Name, property.Name, 0);
    }

    public static string GetLanguageKeyword(XField property)
    {
        var processor = DataProcessorUtility.GetDataProcessor(property.TypeName);
        return processor.LanguageKeyword;
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

    public void Dispose()
    {
        if (m_BinaryWriter != null)
        {
            m_BinaryWriter.Dispose();
            m_BinaryWriter = null;
        }

        if (m_FileStream != null)
        {
            m_FileStream.Dispose();
            m_FileStream = null;
        }
    }
}
