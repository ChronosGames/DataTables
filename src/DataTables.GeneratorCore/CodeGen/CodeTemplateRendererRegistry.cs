using System;
using System.Collections.Generic;
using System.Linq;

namespace DataTables.GeneratorCore;

internal sealed class CodeTemplateRendererRegistry
{
    private readonly Dictionary<string, ICodeTemplateRenderer> m_Renderers;

    public CodeTemplateRendererRegistry(IEnumerable<ICodeTemplateRenderer> renderers)
    {
        ArgumentNullException.ThrowIfNull(renderers);

        m_Renderers = renderers.ToDictionary(
            renderer => renderer.DataSetType,
            renderer => renderer,
            StringComparer.OrdinalIgnoreCase);
    }

    public static CodeTemplateRendererRegistry CreateDefault()
    {
        return new CodeTemplateRendererRegistry(
        [
            new DataTableCodeTemplateRenderer("table"),
            new DataMatrixCodeTemplateRenderer(),
            new DataTableCodeTemplateRenderer("column"),
            new KvTableCodeTemplateRenderer(),
        ]);
    }

    public bool TryGetRenderer(string dataSetType, out ICodeTemplateRenderer renderer)
    {
        return m_Renderers.TryGetValue(dataSetType, out renderer!);
    }
}
