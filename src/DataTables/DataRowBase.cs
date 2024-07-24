using System.IO;

namespace DataTables
{
    public abstract class DataRowBase : IDataRow
    {
        public abstract bool Deserialize(BinaryReader binaryReader);
    }
}
