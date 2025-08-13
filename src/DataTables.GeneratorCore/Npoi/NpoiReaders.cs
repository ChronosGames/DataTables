using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

public sealed class NpoiCellReader : ICellReader
{
	private readonly ICell? m_Cell;
	public NpoiCellReader(ICell? cell) { m_Cell = cell; }
	public string GetString() => ParserUtils.GetCellString(m_Cell);
	public string GetNote() => m_Cell != null ? ParserUtils.GetCellNote(m_Cell) : string.Empty;
}

public sealed class NpoiRowReader : IRowReader
{
	private readonly IRow? m_Row;
	public NpoiRowReader(IRow? row) { m_Row = row; }
	public bool IsValid => ParserUtils.ValidRow(m_Row);
	public int FirstCellIndex => m_Row?.FirstCellNum ?? -1;
	public int LastCellIndex => m_Row?.LastCellNum ?? -1;
	public ICellReader? GetCell(int index) => new NpoiCellReader(m_Row?.GetCell(index));
}

public sealed class NpoiSheetReader : ISheetReader
{
	private readonly ISheet m_Sheet;
	public NpoiSheetReader(ISheet sheet) { m_Sheet = sheet; }
	public string Name => m_Sheet.SheetName;
	public int FirstRowIndex => m_Sheet.FirstRowNum;
	public int LastRowIndex => m_Sheet.LastRowNum;
	public IRowReader? GetRow(int index) => new NpoiRowReader(m_Sheet.GetRow(index));
}

