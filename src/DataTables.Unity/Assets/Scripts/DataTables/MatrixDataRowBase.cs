using System.IO;

namespace DataTables
{
    /// <summary>
    /// Matrix表的数据行基类 - 包含RowKey, ColumnKey和Value三个核心成员
    /// </summary>
    /// <typeparam name="TRowKey">行键类型</typeparam>
    /// <typeparam name="TColumnKey">列键类型</typeparam>
    /// <typeparam name="TValue">值类型</typeparam>
    public abstract class MatrixDataRowBase<TRowKey, TColumnKey, TValue> : DataRowBase
    {
        /// <summary>
        /// 行键
        /// </summary>
        public TRowKey RowKey { get; set; } = default!;
        
        /// <summary>
        /// 列键
        /// </summary>
        public TColumnKey ColumnKey { get; set; } = default!;
        
        /// <summary>
        /// 值
        /// </summary>
        public TValue Value { get; set; } = default!;

        /// <summary>
        /// 构造函数
        /// </summary>
        protected MatrixDataRowBase()
        {
        }

        /// <summary>
        /// 构造函数
        /// </summary>
        /// <param name="rowKey">行键</param>
        /// <param name="columnKey">列键</param>
        /// <param name="value">值</param>
        protected MatrixDataRowBase(TRowKey rowKey, TColumnKey columnKey, TValue value)
        {
            RowKey = rowKey;
            ColumnKey = columnKey;
            Value = value;
        }

        /// <summary>
        /// 设置数据行的值
        /// </summary>
        /// <param name="rowKey">行键</param>
        /// <param name="columnKey">列键</param>
        /// <param name="value">值</param>
        public void SetData(TRowKey rowKey, TColumnKey columnKey, TValue value)
        {
            RowKey = rowKey;
            ColumnKey = columnKey;
            Value = value;
        }

        /// <summary>
        /// 从二进制读取器反序列化数据
        /// </summary>
        public abstract override bool Deserialize(BinaryReader binaryReader);
    }
}