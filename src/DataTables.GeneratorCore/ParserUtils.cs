using System;
using System.Text.RegularExpressions;
using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

internal static class ParserUtils
{
	private static readonly Regex NameRegex = new Regex(@"^[A-Za-z][A-Za-z0-9_]*$");

	public static int FindNextValidRowIndex(ISheet sheet, int startExclusive)
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

	public static int GetFirstValidRowIndex(ISheet sheet)
	{
		return FindNextValidRowIndex(sheet, sheet.FirstRowNum - 1);
	}

	public static bool ValidRow(IRow? row)
	{
		if (row == null || row.FirstCellNum < 0) return false;
		if (row.FirstCellNum == row.LastCellNum) return false;
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
		return found;
	}

	public static string GetCellString(ICell? cell)
	{
		if (cell == null) return string.Empty;
		switch (cell.CellType)
		{
			case CellType.Blank:
				return string.Empty;
			case CellType.Numeric:
				return DateUtil.IsCellDateFormatted(cell)
					? ((DateTime)cell.DateCellValue).ToString("yyyy-MM-dd HH:mm:ss")
					: cell.NumericCellValue.ToString();
			case CellType.String:
				return cell.StringCellValue.Trim();
			case CellType.Boolean:
				return cell.BooleanCellValue ? "TRUE" : "FALSE";
			case CellType.Formula:
				return GetCellString(cell, cell.CachedFormulaResultType);
			case CellType.Error:
				throw new Exception(FormulaError.ForInt(cell.ErrorCellValue).String);
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	public static string GetCellString(ICell cell, CellType cellType)
	{
		switch (cellType)
		{
			case CellType.Blank:
				return string.Empty;
			case CellType.Numeric:
				return DateUtil.IsCellDateFormatted(cell)
					? ((DateTime)cell.DateCellValue).ToString("yyyy-MM-dd HH:mm:ss")
					: cell.NumericCellValue.ToString();
			case CellType.String:
				return cell.StringCellValue.Trim();
			case CellType.Boolean:
				return cell.BooleanCellValue ? "TRUE" : "FALSE";
			case CellType.Formula:
				return GetCellString(cell, cell.CachedFormulaResultType);
			case CellType.Error:
				throw new Exception(FormulaError.ForInt(cell.ErrorCellValue).String);
			default:
				throw new ArgumentOutOfRangeException();
		}
	}

	public static string GetCellNote(ICell cell)
	{
		if (cell.CellComment == null) return string.Empty;
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

	public static void ParseFieldCommentRow(IRow row, GenerationContext context, ParseOptions options)
	{
		context.Fields = new XField[row.LastCellNum - row.FirstCellNum + 1];
		for (int i = 0; i <= row.LastCellNum - row.FirstCellNum; i++)
		{
			var field = new XField(i + row.FirstCellNum);
			context.Fields[i] = field;

			var cell = row.GetCell(i + row.FirstCellNum);
			var text = GetCellString(cell);
			if (string.IsNullOrEmpty(text) || text.TrimStart().StartsWith("#", StringComparison.Ordinal))
			{
				field.IsIgnore = true;
				field.IsComment = !string.IsNullOrEmpty(text) && text.Trim() == options.RowCommentMarkerText;
				continue;
			}

			if (!string.IsNullOrEmpty(context.DataSetType) && !context.DisableTagsFilter)
			{
				var index = text.LastIndexOf('@');
				if (index != -1)
				{
					var tagText = text.Substring(index + 1);
					if (!string.IsNullOrEmpty(options.FilterColumnTags) && !TagFilterUtils.Evaluate(tagText, options.FilterColumnTags))
					{
						field.IsIgnore = true;
						field.IsTagFiltered = true;
						continue;
					}
					text = text.Substring(0, index);
				}
			}

			field.Title = text;
			field.Note = cell != null ? GetCellNote(cell) : string.Empty;
		}
	}

	public static void ParseColumnFields(ISheet sheet, int startRowIndex, GenerationContext context, ParseOptions options)
	{
		var fields = new System.Collections.Generic.List<XField>();
		for (int r = startRowIndex; r <= sheet.LastRowNum; r++)
		{
			var row = sheet.GetRow(r);
			if (!ValidRow(row)) continue;

			var nameCellText = GetCellString(row!.GetCell(row.FirstCellNum + 1));
			if (!string.IsNullOrEmpty(nameCellText) && nameCellText.Trim() == options.ColumnCommentMarkerText)
			{
				context.ColumnCommentRowIndex = r;
				continue;
			}

			var titleCell = row.GetCell(row.FirstCellNum + 0);
			var titleText = GetCellString(titleCell);
			var field = new XField(r)
			{
				Title = titleText,
				Note = titleCell != null ? GetCellNote(titleCell) : string.Empty,
			};

			if (!context.DisableTagsFilter)
			{
				var idx = titleText.LastIndexOf('@');
				if (idx != -1)
				{
					var tagText = titleText.Substring(idx + 1);
					if (!string.IsNullOrEmpty(options.FilterColumnTags) && !TagFilterUtils.Evaluate(tagText, options.FilterColumnTags))
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

			var nameText = GetCellString(row.GetCell(row.FirstCellNum + 1));
			if (string.IsNullOrEmpty(nameText) || nameText.TrimStart().StartsWith("#", StringComparison.Ordinal))
			{
				field.IsIgnore = true;
				field.IsComment = !string.IsNullOrEmpty(nameText) && nameText.Trim() == options.RowCommentMarkerText;
			}
			else
			{
				if (!NameRegex.IsMatch(nameText))
				{
					throw new FormatException($"数据列名称不合法: {nameText}");
				}
				field.Name = nameText;
			}

			var typeText = GetCellString(row.GetCell(row.FirstCellNum + 2));
			field.TypeName = typeText;

			fields.Add(field);
		}

		context.Fields = fields.ToArray();
	}

	// Deprecated: replaced by TagFilterUtils with boolean expression support
}
