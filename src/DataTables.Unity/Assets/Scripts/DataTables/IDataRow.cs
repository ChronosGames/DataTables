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
        /// <param name="raw">要解析的数据表行二进制流</param>
        /// <param name="offset">数据表行二进制流的起始位置</param>
        /// <param name="length">数据表行二进制流的长度</param>
        /// <returns>是否解析数据表行成功</returns>
        bool Deserialize(byte[] raw, int offset, int length);
    }
}
