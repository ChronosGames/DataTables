namespace DataTables.GeneratorCore;

internal sealed class KvTableCodeTemplateRenderer : ICodeTemplateRenderer
{
    public string DataSetType => "kv";

    public string TransformText(GenerationContext context)
    {
        return new KvTableTemplate(context).TransformText();
    }
}
