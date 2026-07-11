namespace DataTables.GeneratorCore;

using System;
using System.Linq;
using System.Text;

public sealed class TreeTableTemplate : DataTableTemplate
{
    public TreeTableTemplate(GenerationContext generationContext) : base(generationContext)
    {
    }

    public override string TransformText()
    {
        var sb = new StringBuilder(base.TransformText());
        var marker = "    #endregion" + Environment.NewLine + "}";
        var insert = BuildTreeApi();
        var text = sb.ToString();
        var idx = text.IndexOf(marker, StringComparison.Ordinal);
        return idx < 0 ? text : text.Insert(idx, insert);
    }

    private string BuildTreeApi()
    {
        var row = GenerationContext.DataRowClassName;
        var table = GenerationContext.DataTableClassName;
        var sb = new StringBuilder();
        void WL(string text = "") => sb.AppendLine(text);

        WL();
        WL("    #region Tree API");
        WL($"    public IReadOnlyList<{row}> GetRoots() => (IReadOnlyList<{row}>?)GetDataRowsGroupByParentId(string.Empty) ?? Array.Empty<{row}>();");
        WL();
        WL("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        WL($"    public IReadOnlyList<{row}>? GetChildren(string id) => GetDataRowsGroupByParentId(id);");
        WL();
        WL("    [MethodImpl(MethodImplOptions.AggressiveInlining)]");
        WL($"    public {row}? GetParent(string id)");
        WL("    {");
        WL("        var node = GetDataRowById(id);");
        WL("        return node == null || string.IsNullOrEmpty(node.ParentId) ? null : GetDataRowById(node.ParentId);");
        WL("    }");
        WL();
        WL($"    public IEnumerable<{row}> TraverseDepthFirst(string id)");
        WL("    {");
        WL("        var root = GetDataRowById(id);");
        WL("        if (root == null) yield break;");
        WL("        var stack = new Stack<IEnumerator<" + row + ">>();");
        WL("        yield return root;");
        WL("        stack.Push((GetChildren(root.Id) ?? Array.Empty<" + row + ">()).Reverse().GetEnumerator());");
        WL("        while (stack.Count > 0)");
        WL("        {");
        WL("            var it = stack.Peek();");
        WL("            if (!it.MoveNext())");
        WL("            {");
        WL("                it.Dispose();");
        WL("                stack.Pop();");
        WL("                continue;");
        WL("            }");
        WL("            var current = it.Current;");
        WL("            yield return current;");
        WL("            stack.Push((GetChildren(current.Id) ?? Array.Empty<" + row + ">()).Reverse().GetEnumerator());");
        WL("        }");
        WL("    }");
        WL();
        WL($"    public static IReadOnlyList<{row}> Roots => DataTableManager.GetDataTableInternal<{table}>()?.GetRoots() ?? Array.Empty<{row}>();");
        WL($"    public static IReadOnlyList<{row}> StaticRoots => Roots;");
        WL($"    public static IReadOnlyList<{row}> RootsStatic => Roots;");
        WL($"    public static IReadOnlyList<{row}>? GetChildrenStatic(string id) => DataTableManager.GetDataTableInternal<{table}>()?.GetChildren(id);");
        WL($"    public static {row}? GetParentStatic(string id) => DataTableManager.GetDataTableInternal<{table}>()?.GetParent(id);");
        WL($"    public static IEnumerable<{row}> TraverseDepthFirstStatic(string id) => DataTableManager.GetDataTableInternal<{table}>()?.TraverseDepthFirst(id) ?? Array.Empty<{row}>();");
        WL("    #endregion");
        return sb.ToString();
    }
}
