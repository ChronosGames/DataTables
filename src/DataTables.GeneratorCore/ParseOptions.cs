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

	public CultureInfo Culture { get; set; } = CultureInfo.InvariantCulture;
}

