namespace DataTables.GeneratorCore;

internal interface ITableDataValidator
{
    string DataSetType { get; }

    void Validate(TableDataValidationContext context);
}
