using System.IO;

namespace DataTables
{
    /// <summary>
    /// 数据表行接口。
    /// </summary>
    public interface IDataRow
    {
        /// <summary>
        /// 解锁数据表行
        /// </summary>
        /// <param name="binaryReader">要解析的数据表行二进制流</param>
        /// <returns>是否解析数据表行成功</returns>
        bool Deserialize(BinaryReader binaryReader);
    }
}
