namespace DataTables.GeneratorCore;

internal sealed class DataTableCodeTemplateRenderer : ICodeTemplateRenderer
{
    public DataTableCodeTemplateRenderer(string dataSetType)
    {
        DataSetType = dataSetType;
    }

    public string DataSetType { get; }

    public string TransformText(GenerationContext context)
    {
        return new DataTableTemplate(context).TransformText();
    }
}
