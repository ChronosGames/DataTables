using System;
using System.Collections.Generic;

namespace DataTables.GeneratorCore;

public sealed class CsvCellReader : ICellReader
{
	private readonly string? m_Value;
	public CsvCellReader(string? value) { m_Value = value; }
	public string GetString() => m_Value?.Trim() ?? string.Empty;
	public string GetNote() => string.Empty;
}

public sealed class CsvRowReader : IRowReader
{
	private readonly string?[] m_Cells;
	public CsvRowReader(string?[] cells) { m_Cells = cells; }
	public bool IsValid
	{
		get
		{
			for (int i = 0; i < m_Cells.Length; i++)
			{
				if (!string.IsNullOrWhiteSpace(m_Cells[i])) return true;
			}
			return false;
		}
	}
	public int FirstCellIndex => 0;
	public int LastCellIndex => m_Cells.Length;
	public ICellReader? GetCell(int index)
	{
		if (index < 0 || index >= m_Cells.Length) return null;
		return new CsvCellReader(m_Cells[index]);
	}
}

public sealed class CsvSheetReader : ISheetReader
{
	private readonly List<CsvRowReader> m_Rows;
	public CsvSheetReader(IEnumerable<string?[]> rows, string name = "CSV")
	{
		m_Rows = new List<CsvRowReader>();
		foreach (var r in rows)
		{
			m_Rows.Add(new CsvRowReader(r));
		}
		Name = name;
	}

	public static CsvSheetReader FromString(string content, char separator = ',')
	{
		var lines = content.Replace("\r\n", "\n").Split('\n');
		var list = new List<string?[]>();
		foreach (var line in lines)
		{
			if (line == null) { list.Add(Array.Empty<string>()); continue; }
			var parts = line.Split(separator);
			list.Add(parts);
		}
		return new CsvSheetReader(list);
	}

	public string Name { get; }
	public int FirstRowIndex => 0;
	public int LastRowIndex => m_Rows.Count - 1;
	public IRowReader? GetRow(int index)
	{
		if (index < 0 || index >= m_Rows.Count) return null;
		return m_Rows[index];
	}
}

