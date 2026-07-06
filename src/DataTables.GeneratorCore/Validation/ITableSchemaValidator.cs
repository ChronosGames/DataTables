namespace DataTables.GeneratorCore;

public interface ITableSchemaValidator
{
    bool Validate(int firstDataRowIndex);
}
