using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DataTables
{
    public abstract class DataRowBase : IDataRow
    {
        public abstract bool Deserialize(BinaryReader binaryReader);
    }
}
