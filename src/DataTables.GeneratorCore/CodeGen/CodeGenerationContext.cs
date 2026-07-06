namespace DataTables.GeneratorCore;

public sealed class CodeGenerationContext
{
    public CodeGenerationContext(GenerationContext generationContext)
    {
        GenerationContext = generationContext;
    }

    public GenerationContext GenerationContext { get; }
}
