namespace DataTables.GeneratorCore;

internal sealed class GraphTableCodeTemplateRenderer : ICodeTemplateRenderer
{
    public string DataSetType => "graph";

    public string TransformText(GenerationContext context)
    {
        return new GraphTableTemplate(context).TransformText();
    }
}
