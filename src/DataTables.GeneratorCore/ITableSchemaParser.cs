namespace DataTables.GeneratorCore;

public interface ITableSchemaParser
{
	/// <summary>
	/// 解析元数据并返回数据起始行（table/matrix）或字段起始行（column）。返回 -1 表示无法解析。
	/// </summary>
	int Parse(ISheetReader sheet, GenerationContext context, ParseOptions options, DiagnosticsCollector diagnostics);
}

