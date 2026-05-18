using System.Globalization;

namespace DataTables.GeneratorCore;

public enum FormulaEvaluationPolicy
{
	Off,
	ValidateOnly,
	ForceEvaluate
}

public sealed class ParseOptions
{
	public bool StrictNameValidation { get; set; } = true;

	public bool ValidateFormulaConsistency { get; set; } = true;

	public FormulaEvaluationPolicy FormulaPolicy { get; set; } = FormulaEvaluationPolicy.ValidateOnly;

	public string ColumnCommentMarkerText { get; set; } = "#列注释标志";

	public string RowCommentMarkerText { get; set; } = "#行注释标志";

	public string SkipCellMarker { get; set; } = "#";

	public string FilterColumnTags { get; set; } = string.Empty;

	/// <summary>
	/// 嵌套数组纯文本格式的分隔符序列。每个字符依次代表第 1、2、3… 层数组的分隔符。
	/// 例如 "#|-" 表示最外层使用 '#'、第二层使用 '|'、第三层使用 '-'。
	/// 为空时使用兼容模式：优先 '|'，否则 '#'，无法表达多层嵌套。
	/// </summary>
	public string ArrayNestedSeparators { get; set; } = string.Empty;

	public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;
}

