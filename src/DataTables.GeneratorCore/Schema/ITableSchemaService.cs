using NPOI.SS.UserModel;

namespace DataTables.GeneratorCore;

public interface ITableSchemaService
{
    int CreateGenerationContext(ISheet sheet);
}
