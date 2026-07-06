namespace DataTables.GeneratorCore;

using System;
using System.Text;

public sealed class GraphTableTemplate : DataTableTemplate
{
    public GraphTableTemplate(GenerationContext generationContext) : base(generationContext)
    {
    }

    public override string TransformText()
    {
        var text = base.TransformText();
        var marker = "    #endregion" + Environment.NewLine + "}";
        var idx = text.IndexOf(marker, StringComparison.Ordinal);
        return idx < 0 ? text : text.Insert(idx, BuildGraphApi());
    }

    private string BuildGraphApi()
    {
        var row = GenerationContext.DataRowClassName;
        var table = GenerationContext.DataTableClassName;
        var sb = new StringBuilder();
        void WL(string text = "") => sb.AppendLine(text);

        WL();
        WL("    #region Graph API");
        WL("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        WL($"    public {row}? GetEdge(string edgeId) => GetByEdgeId(edgeId);");
        WL();
        WL("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        WL($"    public bool TryGetEdge(string edgeId, out {row}? edge) => TryGetByEdgeId(edgeId, out edge);");
        WL();
        WL("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        WL($"    public IReadOnlyList<{row}> GetOutgoingEdges(string nodeId) => GetManyByFrom(nodeId) ?? Array.Empty<{row}>();");
        WL();
        WL("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        WL($"    public IReadOnlyList<{row}> GetIncomingEdges(string nodeId) => GetManyByTo(nodeId) ?? Array.Empty<{row}>();");
        WL();
        WL("    public IReadOnlyList<string> GetNeighbors(string nodeId)");
        WL("    {");
        WL("        var result = new List<string>();");
        WL("        var seen = new HashSet<string>(StringComparer.Ordinal);");
        WL("        foreach (var edge in GetOutgoingEdges(nodeId))");
        WL("        {");
        WL("            if (seen.Add(edge.To)) result.Add(edge.To);");
        WL("        }");
        WL("        return result;");
        WL("    }");
        WL();
        WL("    public bool ContainsNode(string nodeId) => ContainsFrom(nodeId) || ContainsTo(nodeId);");
        WL("    public string? GetNode(string nodeId) => ContainsNode(nodeId) ? nodeId : null;");
        WL();
        WL($"    public static {row}? GetEdgeStatic(string edgeId) => DataTableManager.GetDataTableInternal<{table}>()?.GetEdge(edgeId);");
        WL($"    public static bool TryGetEdgeStatic(string edgeId, out {row}? edge)");
        WL("    {");
        WL($"        var table = DataTableManager.GetDataTableInternal<{table}>();");
        WL("        edge = null;");
        WL("        return table != null && table.TryGetEdge(edgeId, out edge);");
        WL("    }");
        WL($"    public static IReadOnlyList<{row}> GetOutgoingEdgesStatic(string nodeId) => DataTableManager.GetDataTableInternal<{table}>()?.GetOutgoingEdges(nodeId) ?? Array.Empty<{row}>();");
        WL($"    public static IReadOnlyList<{row}> GetIncomingEdgesStatic(string nodeId) => DataTableManager.GetDataTableInternal<{table}>()?.GetIncomingEdges(nodeId) ?? Array.Empty<{row}>();");
        WL($"    public static IReadOnlyList<string> GetNeighborsStatic(string nodeId) => DataTableManager.GetDataTableInternal<{table}>()?.GetNeighbors(nodeId) ?? Array.Empty<string>();");
        WL($"    public static bool ContainsNodeStatic(string nodeId) => DataTableManager.GetDataTableInternal<{table}>()?.ContainsNode(nodeId) == true;");
        WL($"    public static string? GetNodeStatic(string nodeId) => DataTableManager.GetDataTableInternal<{table}>()?.GetNode(nodeId);");
        WL("    #endregion");
        return sb.ToString();
    }
}
