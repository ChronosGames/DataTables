using System;
using System.Collections.Generic;
using System.Text;

namespace DataTables
{
    public abstract class DataRowBase : IDataRow
    {
        public abstract bool Deserialize(byte[] raw, int offset, int length);
    }
}
