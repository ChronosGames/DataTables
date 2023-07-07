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

        public void Read(Type dataTableType, string name, Action<byte[]> callback)
        {
            var raw = File.ReadAllBytes(Path.Combine(m_DataDir, dataTableType.Name + (string.IsNullOrEmpty(name) ? string.Empty : '.' + name) + ".bytes"));
            callback(raw);
        }
    }
}
