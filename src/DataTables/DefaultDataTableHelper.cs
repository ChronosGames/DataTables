using System;
using System.IO;

namespace DataTables
{
    public class DefaultDataTableHelper : IDataTableHelper
    {
        private readonly string m_DataDir;

        public DefaultDataTableHelper(string dataDir)
        {
            m_DataDir = dataDir;
        }

        public byte[] Read(Type dataTableType, string name)
        {
            return File.ReadAllBytes(Path.Combine(m_DataDir, dataTableType.Name + (string.IsNullOrEmpty(name) ? string.Empty : '.' + name) + ".bytes"));
        }
    }
}
