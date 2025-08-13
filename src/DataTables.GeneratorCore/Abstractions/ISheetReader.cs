namespace DataTables.GeneratorCore;

public interface ISheetReader
{
	string Name { get; }
	int FirstRowIndex { get; }
	int LastRowIndex { get; }
	IRowReader? GetRow(int index);
}

