using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

internal interface IDataTableBinaryWriter
{
    void GenerateDataFile(string outputDir, string comparisonOutputDir, bool forceOverwrite, ISheet sheet, ILogger logger);
}
