using System;
using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

public sealed class ColumnTableParser : ITableSchemaParser
{
    public int Parse(ISheetReader sheet, GenerationContext context, ParseOptions options, DiagnosticsCollector diagnostics)
	{
        int header = DataTables.GeneratorCore.Reader.ReaderParserUtils.GetFirstValidRowIndex(sheet);
		if (header == -1) return -1;

        int firstField = DataTables.GeneratorCore.Reader.ReaderParserUtils.FindNextValidRowIndex(sheet, header);
		if (firstField == -1) return -1;

		// 向下逐行构建字段
        DataTables.GeneratorCore.Reader.ReaderParserUtils.ParseColumnFields(sheet, firstField, context, options);
		// 统计：忽略字段与 Tag 过滤字段
		var metrics = diagnostics.GetMetrics(context.FileName, context.SheetName);
		foreach (var f in context.Fields)
		{
			if (f.IsIgnore) metrics.IgnoredFieldCount++;
			if (f.IsTagFiltered) metrics.TagFilteredFieldCount++;
		}
		// 数据从字段三列之后开始（A:Title, B:Name, C:Type => D列开始）
        var row = sheet.GetRow(firstField);
        context.ColumnFirstDataColIndex = row?.FirstCellIndex + 3 ?? 3;
		// 行遍历用于定位列注释行，真实写出按列
		return firstField;
	}
}

