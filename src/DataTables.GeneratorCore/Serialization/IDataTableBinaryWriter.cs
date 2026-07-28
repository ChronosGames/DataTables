namespace DataTables.GeneratorCore;

internal interface IDataTableBinaryWriter
{
    void GenerateDataFile(string outputDir, string comparisonOutputDir, bool forceOverwrite, TableDataSnapshot snapshot, ILogger logger);
}
