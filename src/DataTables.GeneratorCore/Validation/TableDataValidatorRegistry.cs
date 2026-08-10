using System;
using System.Collections.Generic;
using System.Linq;

namespace DataTables.GeneratorCore;

internal sealed class TableDataValidatorRegistry
{
    private readonly Dictionary<string, ITableDataValidator> m_Validators;

    public TableDataValidatorRegistry(IEnumerable<ITableDataValidator> validators)
    {
        ArgumentNullException.ThrowIfNull(validators);
        m_Validators = validators.ToDictionary(x => x.DataSetType, StringComparer.OrdinalIgnoreCase);
    }

    public static TableDataValidatorRegistry CreateDefault()
    {
        return new TableDataValidatorRegistry(
        [
            new RowTableDataValidator(),
            new NoOpTableDataValidator("matrix"),
            new NoOpTableDataValidator("column"),
            new NoOpTableDataValidator("kv"),
            new TreeTableDataValidator(),
            new GraphTableDataValidator()
        ]);
    }

    public IReadOnlyCollection<string> SupportedDataSetTypes =>
        m_Validators.Keys.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).ToArray();

    public bool TryGetValidator(string dataSetType, out ITableDataValidator validator)
    {
        return m_Validators.TryGetValue(dataSetType, out validator!);
    }

    private sealed class NoOpTableDataValidator(string dataSetType) : ITableDataValidator
    {
        public string DataSetType { get; } = dataSetType;

        public void Validate(TableDataValidationContext context)
        {
        }
    }
}
