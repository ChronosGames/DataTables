using System;
using System.Collections.Generic;
using System.IO;
using System.Xml.Linq;

namespace DataTables
{
    public abstract class DataMatrixBase<TKey1, TKey2, TValue> : DataTableBase
    {
        private readonly Dictionary<MultiKey<TKey1, TKey2>, TValue> m_DataSet = new Dictionary<MultiKey<TKey1, TKey2>, TValue>();

        protected virtual TValue? DefaultValue => default;

        public override Type Type => typeof(TValue);

        public override int Count => m_DataSet.Count;

        public DataMatrixBase(string name) : base(name)
        { }

        internal override void InitDataSet(int capacity)
        {
            m_DataSet.EnsureCapacity(capacity);
        }

        internal override bool SetDataRow(int index, BinaryReader reader) => Deserialize(reader);

        protected abstract bool Deserialize(BinaryReader reader);

        protected void AddDataSet(TKey1 key1, TKey2 key2, TValue value)
        {
            m_DataSet.Add(new MultiKey<TKey1, TKey2>(key1, key2), value);
        }

        /// <summary>
        /// 获取当前的配置值，为空则取默认值
        /// </summary>
        /// <param name="key1"></param>
        /// <param name="key2"></param>
        /// <returns></returns>
        public TValue? Get(TKey1 key1, TKey2 key2) => m_DataSet.TryGetValue(new MultiKey<TKey1, TKey2>(key1, key2), out var value) ? value : DefaultValue;

        public override void RemoveAllDataRows()
        {
            m_DataSet.Clear();
        }

        internal override void Shutdown()
        {
            m_DataSet.Clear();
        }
    }
}
