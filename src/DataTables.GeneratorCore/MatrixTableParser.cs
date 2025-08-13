using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

public sealed class MatrixTableParser : ITableSchemaParser
{
    public int Parse(ISheetReader sheet, GenerationContext context, ParseOptions options, DiagnosticsCollector diagnostics)
	{
        int header = DataTables.GeneratorCore.Reader.ReaderParserUtils.GetFirstValidRowIndex(sheet);
		if (header == -1) return -1;

        int keyRowIndex = DataTables.GeneratorCore.Reader.ReaderParserUtils.FindNextValidRowIndex(sheet, header);
		if (keyRowIndex == -1) return -1;

        var row = sheet.GetRow(keyRowIndex)!;
        for (int cellNum = row.FirstCellIndex; cellNum < row.LastCellIndex; cellNum++)
		{
            var cellString = row.GetCell(cellNum)?.GetString() ?? string.Empty;
			if (string.IsNullOrEmpty(cellString))
			{
				continue;
			}

			context.ColumnIndexToKey2.Add(cellNum, cellString);
		}

		return keyRowIndex + 1;
	}
}

