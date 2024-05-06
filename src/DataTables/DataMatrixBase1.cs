using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DataTables
{
    public abstract class DataMatrixBase1<TKey1, TKey2, TValue> : DataTableBase
        where TValue : struct
    {
        private Tuple<TKey1, TKey2, TValue>[] m_DataSet = Array.Empty<Tuple<TKey1, TKey2, TValue>>();
        private readonly Dictionary<ValueTuple<TKey1, TKey2>, TValue> m_Dict1 = new Dictionary<(TKey1, TKey2), TValue>();

        protected virtual TValue DefaultValue => default;

        public override Type Type => typeof(TValue);

        public override int Count => m_DataSet.Length;

        public DataMatrixBase1(string name) : base(name)
        { }

        internal override void InitDataSet(int capacity)
        {
            m_DataSet = new Tuple<TKey1, TKey2, TValue>[capacity];
        }

        internal override bool SetDataRow(int index, BinaryReader reader) => Deserialize(index, reader);

        protected abstract bool Deserialize(int index, BinaryReader reader);

        protected void AddDataSet(int index, TKey1 key1, TKey2 key2, TValue value)
        {
            m_DataSet[index] = Tuple.Create(key1, key2, value);
            m_Dict1.Add(ValueTuple.Create(key1, key2), value);
        }

        /// <summary>
        /// 获取当前的配置值，为空则取默认值
        /// </summary>
        /// <param name="key1"></param>
        /// <param name="key2"></param>
        /// <returns></returns>
        public TValue? Get(TKey1 key1, TKey2 key2)
        {
            return m_Dict1.TryGetValue(ValueTuple.Create(key1, key2), out var value) ? value : DefaultValue;
        }

        /// <summary>
        /// 查询指定Key2与Value对应的Key1列表
        /// </summary>
        /// <param name="key2"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public IEnumerable<TKey1> FindKey1(TKey2 key2, TValue value)
        {
            for (var i = 0; i < m_DataSet.Length; i++)
            {
                var pair = m_DataSet[i];
                if (EqualityComparer<TKey2>.Default.Equals(pair.Item2, key2) && EqualityComparer<TValue>.Default.Equals(pair.Item3, value))
                {
                    yield return pair.Item1;
                }
            }
        }

        /// <summary>
        /// 查询指定Key1与Value对应的Key2列表
        /// </summary>
        /// <param name="key1"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public IEnumerable<TKey2> FindKey2(TKey1 key1, TValue value)
        {
            for (var i = 0; i < m_DataSet.Length; i++)
            {
                var pair = m_DataSet[i];
                if (EqualityComparer<TKey1>.Default.Equals(pair.Item1, key1) && EqualityComparer<TValue>.Default.Equals(pair.Item3, value))
                {
                    yield return pair.Item2;
                }
            }
        }

        /// <summary>
        /// 查询指定条件的结果
        /// </summary>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public IEnumerable<Tuple<TKey1, TKey2, TValue>> Where(Func<TKey1, TKey2, TValue, bool> predicate)
        {
            return m_DataSet.Where(x => predicate(x.Item1, x.Item2, x.Item3));
        }

        public override void RemoveAllDataRows()
        {
            m_DataSet = Array.Empty<Tuple<TKey1, TKey2, TValue>>();
            m_Dict1.Clear();
        }

        internal override void Shutdown()
        {
            m_DataSet = Array.Empty<Tuple<TKey1, TKey2, TValue>>();
            m_Dict1.Clear();
        }
    }
}
