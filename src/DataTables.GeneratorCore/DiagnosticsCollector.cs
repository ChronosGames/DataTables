using System.Collections.Generic;

namespace DataTables.GeneratorCore;

public enum DiagnosticSeverity
{
	Info,
	Warning,
	Error
}

public sealed class Diagnostic
{
	public Diagnostic(DiagnosticSeverity severity, string file, string sheet, string cell, string message)
	{
		Severity = severity;
		File = file;
		Sheet = sheet;
		Cell = cell;
		Message = message;
	}

	public DiagnosticSeverity Severity { get; }
	public string File { get; }
	public string Sheet { get; }
	public string Cell { get; }
	public string Message { get; }
}

public sealed class DiagnosticsCollector
{
	private readonly List<Diagnostic> m_Diagnostics = new();
    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, DiagnosticsMetrics> m_Metrics = new();

	public IReadOnlyList<Diagnostic> Items => m_Diagnostics;

    public DiagnosticsMetrics GetMetrics(string file, string sheet)
    {
        var key = file + "|" + sheet;
        return m_Metrics.GetOrAdd(key, _ => new DiagnosticsMetrics(file, sheet));
    }

    public System.Collections.Generic.IEnumerable<DiagnosticsMetrics> GetAllMetrics()
    {
        return m_Metrics.Values;
    }

	public void Info(string file, string sheet, string cell, string message)
	{
		m_Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Info, file, sheet, cell, message));
	}

	public void Warn(string file, string sheet, string cell, string message)
	{
		m_Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Warning, file, sheet, cell, message));
	}

	public void Error(string file, string sheet, string cell, string message)
	{
		m_Diagnostics.Add(new Diagnostic(DiagnosticSeverity.Error, file, sheet, cell, message));
	}
}

public sealed class DiagnosticsMetrics
{
    public DiagnosticsMetrics(string file, string sheet)
    {
        File = file; Sheet = sheet;
    }
    public string File { get; }
    public string Sheet { get; }

    public int IgnoredFieldCount { get; set; }
    public int TagFilteredFieldCount { get; set; }
    public int SkippedColumnCount { get; set; }
    public int MatrixDefaultSkippedCount { get; set; }
    public long ParseElapsedMs { get; set; }
    public long GenerateElapsedMs { get; set; }
}

