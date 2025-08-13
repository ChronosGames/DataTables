namespace DataTables.GeneratorCore;

public interface IRowReader
{
	bool IsValid { get; }
	int FirstCellIndex { get; }
	int LastCellIndex { get; }
	ICellReader? GetCell(int index);
}

