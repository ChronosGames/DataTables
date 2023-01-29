using System;

namespace DataTables.GeneratorCore
{
    public sealed partial class DataTableProcessor
    {
        public abstract class GenericDataProcessor<T> : DataProcessor
        {
            public override Type Type => typeof(T);

            public abstract T Parse(string value);
        }
    }
}
