namespace DataTables.GeneratorCore;

internal interface ICodeTemplateRenderer
{
    string DataSetType { get; }

    string TransformText(GenerationContext context);
}
