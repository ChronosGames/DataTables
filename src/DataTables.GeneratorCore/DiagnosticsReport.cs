using System.Collections.Generic;

namespace DataTables.GeneratorCore;

public sealed class DiagnosticsReport
{
	public int InfoCount { get; set; }
	public int WarningCount { get; set; }
	public int ErrorCount { get; set; }
	public List<Diagnostic> Items { get; set; } = new();
    public List<DiagnosticsMetrics> Metrics { get; set; } = new();
}

