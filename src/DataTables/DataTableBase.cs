using System;
using System.IO;

namespace DataTables
{
    public abstract class DataTableBase
    {
        private readonly string m_Name;

        /// <summary>
        /// 初始化数据表基类的新实例。
        /// </summary>
        public DataTableBase()
            : this(null)
        {
        }

        /// <summary>
        /// 初始化数据表基类的新实例。
        /// </summary>
        /// <param name="name">数据表名称。</param>
        public DataTableBase(string name)
        {
            m_Name = name ?? string.Empty;
        }

        /// <summary>
        /// 获取数据表名称。
        /// </summary>
        public string Name
        {
            get
            {
                return m_Name;
            }
        }

        /// <summary>
        /// 获取数据表完整名称。
        /// </summary>
        public string FullName
        {
            get
            {
                return Type.ToString();
            }
        }

        /// <summary>
        /// 获取数据表行的类型。
        /// </summary>
        public abstract Type Type
        {
            get;
        }

        /// <summary>
        /// 获取数据表行数。
        /// </summary>
        public abstract int Count
        {
            get;
        }

        /// <summary>
        /// 初始化数据集。
        /// </summary>
        /// <param name="capacity">将要初始化的数据值容量大小</param>
        internal abstract void InitDataSet(int capacity);

        /// <summary>
        /// 增加数据表行。
        /// </summary>
        /// <param name="index">将要增加的数据表行的索引值。</param>
        /// <param name="binaryReader">要解析的数据表行二进制流。</param>
        /// <returns>是否增加数据表行成功。</returns>
        internal abstract bool SetDataRow(int index, BinaryReader binaryReader);

        /// <summary>
        /// 配置表加载完成
        /// <para>可重载该方法以便自定义一些额外的操作</para>
        /// </summary>
        public virtual void OnLoadCompleted() { }

        /// <summary>
        /// 清空所有数据表行。
        /// </summary>
        public abstract void RemoveAllDataRows();

        /// <summary>
        /// 关闭并清理数据表。
        /// </summary>
        internal abstract void Shutdown();
    }
}
