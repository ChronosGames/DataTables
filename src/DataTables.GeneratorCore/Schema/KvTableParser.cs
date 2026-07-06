using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace DataTables.GeneratorCore;

public sealed class KvTableParser : ITableSchemaParser
{
    private static readonly Regex NameRegex = new Regex(@"^[A-Za-z][A-Za-z0-9_]*$");

    public string DataSetType => "kv";

    public int Parse(ISheetReader sheet, GenerationContext context, ParseOptions options, DiagnosticsCollector diagnostics)
    {
        int header = sheet.FirstRowIndex;
        while (header <= sheet.LastRowIndex && !(sheet.GetRow(header)?.IsValid ?? false)) header++;
        if (header == -1) return -1;

        int columnRowIndex = Reader.ReaderParserUtils.FindNextValidRowIndex(sheet, header);
        if (columnRowIndex == -1) return -1;

        var columnRow = sheet.GetRow(columnRowIndex)!;
        var columns = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (int i = columnRow.FirstCellIndex; i < columnRow.LastCellIndex; i++)
        {
            var name = (columnRow.GetCell(i)?.GetString() ?? string.Empty).Trim();
            if (!string.IsNullOrEmpty(name)) columns[name] = i;
        }

        if (!columns.TryGetValue("Key", out var keyCol) || !columns.TryGetValue("Type", out var typeCol) || !columns.TryGetValue("Value", out var valueCol))
        {
            diagnostics.Error(context.FileName, context.SheetName, ParserUtils.GetCellAddress(columnRowIndex, columnRow.FirstCellIndex), "kv 表必须包含 Key、Type、Value 列");
            return -1;
        }

        var commentCol = columns.TryGetValue("Comment", out var foundCommentCol) ? foundCommentCol : -1;
        var fields = new List<XField>();
        var keyRows = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (int r = columnRowIndex + 1; r <= sheet.LastRowIndex; r++)
        {
            var row = sheet.GetRow(r);
            if (row == null || !row.IsValid) continue;

            var key = (row.GetCell(keyCol)?.GetString() ?? string.Empty).Trim();
            var type = (row.GetCell(typeCol)?.GetString() ?? string.Empty).Trim();
            var value = (row.GetCell(valueCol)?.GetString() ?? string.Empty).Trim();
            var comment = commentCol >= 0 ? (row.GetCell(commentCol)?.GetString() ?? string.Empty).Trim() : string.Empty;

            if (string.IsNullOrEmpty(key)) throw new FormatException($"kv Key 不能为空: {ParserUtils.GetCellAddress(r, keyCol)}");
            if (!NameRegex.IsMatch(key)) throw new FormatException($"kv Key 无法转换为合法 C# 成员名: {key} ({ParserUtils.GetCellAddress(r, keyCol)})");
            if (!keyRows.TryAdd(key, r)) throw new FormatException($"kv Key 重复: {key} 首次出现行 {keyRows[key] + 1}, 重复出现行 {r + 1}");
            if (string.IsNullOrEmpty(type)) throw new FormatException($"kv Type 不能为空: {ParserUtils.GetCellAddress(r, typeCol)}");
            if (string.IsNullOrEmpty(value)) throw new FormatException($"kv Value 不能为空: {ParserUtils.GetCellAddress(r, valueCol)}");

            // 生成期先解析类型，非法类型会在此处暴露。
            DataProcessorUtility.GetDataProcessor(type);

            fields.Add(new XField(valueCol)
            {
                Name = key,
                TypeName = type,
                Title = string.IsNullOrEmpty(comment) ? key : comment,
                TypeCell = ParserUtils.GetCellAddress(r, typeCol),
                Note = value,
            });
        }

        context.Fields = fields.ToArray();
        return columnRowIndex + 1;
    }
}
