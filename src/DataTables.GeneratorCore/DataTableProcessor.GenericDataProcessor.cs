namespace DataTables.GeneratorCore
{
    public sealed partial class DataTableProcessor
    {
        public abstract class GenericDataProcessor<T> : DataProcessor
        {
            public abstract T Parse(string value);
        }
    }
}
