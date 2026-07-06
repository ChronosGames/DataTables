namespace DataTables.GeneratorCore;

internal sealed class TreeTableCodeTemplateRenderer : ICodeTemplateRenderer
{
    public string DataSetType => "tree";

    public string TransformText(GenerationContext context)
    {
        return new TreeTableTemplate(context).TransformText();
    }
}
