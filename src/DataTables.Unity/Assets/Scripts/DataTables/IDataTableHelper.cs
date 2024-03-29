using System;

namespace DataTables
{
    /// <summary>
    /// 数据表辅助器接口。
    /// </summary>
    public interface IDataTableHelper
    {
        void Read(string fileName, Action<byte[]> callback);
    }
}
