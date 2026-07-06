namespace DataTables.GeneratorCore;

internal sealed class DataMatrixCodeTemplateRenderer : ICodeTemplateRenderer
{
    public string DataSetType => "matrix";

    public string TransformText(GenerationContext context)
    {
        return new DataMatrixTemplate(context).TransformText();
    }
}
