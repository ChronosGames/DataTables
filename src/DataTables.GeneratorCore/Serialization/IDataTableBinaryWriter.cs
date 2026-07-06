using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

internal interface IDataTableBinaryWriter
{
    void GenerateDataFile(string filePath, string outputDir, bool forceOverwrite, ISheet sheet, ILogger logger);
}
