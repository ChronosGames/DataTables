using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

public interface IDataTableBinaryWriter
{
    void GenerateDataFile(string filePath, string outputDir, bool forceOverwrite, ISheet sheet, ILogger logger);
}
