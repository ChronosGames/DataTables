using System;

namespace DataTables.GeneratorCore.Reader;

internal static class ReaderParserUtils
{
	public static int FindNextValidRowIndex(ISheetReader sheet, int startExclusive)
	{
		for (int i = startExclusive + 1; i <= sheet.LastRowIndex; i++)
		{
			if (sheet.GetRow(i)?.IsValid ?? false)
			{
				return i;
			}
		}
		return -1;
	}

	public static int GetFirstValidRowIndex(ISheetReader sheet)
	{
		return FindNextValidRowIndex(sheet, sheet.FirstRowIndex - 1);
	}

	public static void ParseFieldCommentRow(IRowReader row, GenerationContext context, ParseOptions options)
	{
		context.Fields = new XField[row.LastCellIndex - row.FirstCellIndex + 1];
		for (int i = 0; i <= row.LastCellIndex - row.FirstCellIndex; i++)
		{
			var field = new XField(i + row.FirstCellIndex);
			context.Fields[i] = field;

			var cell = row.GetCell(i + row.FirstCellIndex);
			var text = cell?.GetString() ?? string.Empty;
			if (string.IsNullOrEmpty(text) || text.TrimStart().StartsWith("#", StringComparison.Ordinal))
			{
				field.IsIgnore = true;
				field.IsComment = !string.IsNullOrEmpty(text) && text.Trim() == options.RowCommentMarkerText;
				continue;
			}

			if (!string.IsNullOrEmpty(context.DataSetType) && !context.DisableTagsFilter)
			{
				var index = text.LastIndexOf('@');
				var filter = options.FilterColumnTags ?? string.Empty;
				if (!string.IsNullOrEmpty(filter))
				{
					// 指定了标签过滤：保留没有标签的字段和匹配标签的字段
					if (index != -1)
					{
						var tagText = text.Substring(index + 1);
						bool match = false;
						for (int ci = 0; ci < filter.Length && !match; ci++)
						{
							char f = char.ToUpperInvariant(filter[ci]);
							foreach (var tc in tagText)
							{
								if (char.ToUpperInvariant(tc) == f)
								{
									match = true;
									break;
								}
							}
						}
						if (!match)
						{
							field.IsIgnore = true;
							field.IsTagFiltered = true;
							continue;
						}
					}
					// 没有标签的字段保留（对所有环境可用）
					if (index != -1)
					{
						text = text.Substring(0, index);
					}
				}
				else if (index != -1)
				{
					// 未指定过滤，则不过滤，只移除 @ 标签显示
					text = text.Substring(0, index);
				}
			}

			field.Title = text;
			field.Note = cell != null ? cell.GetNote() : string.Empty;
		}
	}

	public static void ParseColumnFields(ISheetReader sheet, int startRowIndex, GenerationContext context, ParseOptions options)
	{
		var fields = new System.Collections.Generic.List<XField>();
		for (int r = startRowIndex; r <= sheet.LastRowIndex; r++)
		{
			var row = sheet.GetRow(r);
			if (!(row?.IsValid ?? false)) continue;

			// Column 模式：约定 A:Title, B:Name, C:Type
            string norm(string s) => (s ?? string.Empty).Trim();
            var nameCellText = norm(row!.GetCell(1)?.GetString() ?? string.Empty);
            var titleCellText = norm(row.GetCell(0)?.GetString() ?? string.Empty);
            var typeCellText = norm(row.GetCell(2)?.GetString() ?? string.Empty);
            if (!string.IsNullOrEmpty(options.ColumnCommentMarkerText))
			{
                var marker = options.ColumnCommentMarkerText.Trim();
                if (nameCellText == marker || titleCellText == marker || typeCellText == marker)
                {
                    context.ColumnCommentRowIndex = r;
                    continue;
                }
			}

			var titleCell = row.GetCell(0);
			var titleText = titleCell?.GetString() ?? string.Empty;
			var field = new XField(r)
			{
				Title = titleText,
				Note = titleCell != null ? titleCell.GetNote() : string.Empty,
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

			var nameText = (row.GetCell(1)?.GetString() ?? string.Empty).Trim();
			if (string.IsNullOrEmpty(nameText) || nameText.TrimStart().StartsWith("#", StringComparison.Ordinal))
			{
				field.IsIgnore = true;
				field.IsComment = !string.IsNullOrEmpty(nameText) && nameText.Trim() == options.RowCommentMarkerText;
			}
			else
			{
				if (!new System.Text.RegularExpressions.Regex(@"^[A-Za-z][A-Za-z0-9_]*$").IsMatch(nameText))
				{
					if (options.StrictNameValidation)
					{
						throw new FormatException($"数据列名称不合法: {nameText}");
					}
				}
				field.Name = nameText;
			}

			var typeText = row.GetCell(2)?.GetString() ?? string.Empty;
			field.TypeName = typeText?.Trim() ?? string.Empty;

            fields.Add(field);
		}

		context.Fields = fields.ToArray();
	}

	// Deprecated: replaced by TagFilterUtils with boolean expression support
}

