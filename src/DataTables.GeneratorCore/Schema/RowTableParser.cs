using System;
using System.Text.RegularExpressions;


namespace DataTables.GeneratorCore;

public sealed class RowTableParser : ITableSchemaParser
{
	private static readonly Regex NameRegex = new Regex(@"^[A-Za-z][A-Za-z0-9_]*$");

    public int Parse(ISheetReader sheet, GenerationContext context, ParseOptions options, DiagnosticsCollector diagnostics)
	{
        int header = sheet.FirstRowIndex;
		while (header <= sheet.LastRowIndex && !(sheet.GetRow(header)?.IsValid ?? false)) header++;
		if (header == -1) return -1;

        int commentRow = header + 1; while (commentRow <= sheet.LastRowIndex && !(sheet.GetRow(commentRow)?.IsValid ?? false)) commentRow++;
		if (commentRow == -1) return -1;
        DataTables.GeneratorCore.Reader.ReaderParserUtils.ParseFieldCommentRow(sheet.GetRow(commentRow)!, context, options);
		// 统计：忽略字段与 Tag 过滤字段
		var metrics = diagnostics.GetMetrics(context.FileName, context.SheetName);
		foreach (var f in context.Fields)
		{
			if (f.IsIgnore) metrics.IgnoredFieldCount++;
			if (f.IsTagFiltered) metrics.TagFilteredFieldCount++;
		}

        int nameRow = DataTables.GeneratorCore.Reader.ReaderParserUtils.FindNextValidRowIndex(sheet, commentRow);
		if (nameRow == -1) return -1;
        var nameRowObj = sheet.GetRow(nameRow)!;
		foreach (var field in context.Fields)
		{
			if (field.IsIgnore) continue;
			var text = (nameRowObj.GetCell(field.Index)?.GetString() ?? string.Empty).Trim();
			
			// 如果字段名为空或是注释，标记为忽略但不抛出异常
			if (string.IsNullOrEmpty(text) || text.TrimStart().StartsWith("#", StringComparison.Ordinal))
			{
				field.IsIgnore = true;
				field.IsComment = !string.IsNullOrEmpty(text) && text.Trim().StartsWith("#");
				continue;
			}
			
			if (options.StrictNameValidation && !NameRegex.IsMatch(text))
			{
				throw new FormatException($"数据列名称不合法: {text}");
			}
			field.Name = text;
		}

        int typeRow = DataTables.GeneratorCore.Reader.ReaderParserUtils.FindNextValidRowIndex(sheet, nameRow);
		if (typeRow == -1) return -1;
        var typeRowObj = sheet.GetRow(typeRow)!;
		foreach (var field in context.Fields)
		{
			if (field.IsIgnore) continue;
			field.TypeName = (typeRowObj.GetCell(field.Index)?.GetString() ?? string.Empty).Trim();
		}

		return typeRow + 1;
	}
}

