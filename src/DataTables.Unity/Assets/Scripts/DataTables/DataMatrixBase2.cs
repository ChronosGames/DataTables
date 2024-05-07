using System;
using System.Collections.Generic;
using System.IO;

namespace DataTables
{
    public abstract class DataMatrixBase<TKey1, TKey2, TValue> : DataTableBase
        where TKey1 : struct
        where TKey2 : struct
        where TValue : struct
    {
        private TKey1[] m_Keies1;
        private TKey2[] m_Keies2;
        private TValue[] m_Values;

        protected virtual TValue DefaultValue => default;

        public override Type Type => typeof(TValue);

        public override int Count => m_Keies1.Length;

        public DataMatrixBase(string name) : base(name)
        {
            m_Keies1 = Array.Empty<TKey1>();
            m_Keies2 = Array.Empty<TKey2>();
            m_Values = Array.Empty<TValue>();
        }

        internal override void InitDataSet(int capacity)
        {
            m_Keies1 = new TKey1[capacity];
            m_Keies2 = new TKey2[capacity];
            m_Values = new TValue[capacity];
        }

        internal override bool SetDataRow(int index, BinaryReader reader) => Deserialize(index, reader);

        protected abstract bool Deserialize(int index, BinaryReader reader);

        protected void AddDataSet(int index, TKey1 key1, TKey2 key2, TValue value)
        {
            m_Keies1[index] = key1;
            m_Keies2[index] = key2;
            m_Values[index] = value;
        }

        /// <summary>
        /// 获取当前的配置值，为空则取默认值
        /// </summary>
        /// <param name="key1"></param>
        /// <param name="key2"></param>
        /// <returns></returns>
        public TValue? Get(TKey1 key1, TKey2 key2)
        {
            for (int i = 0; i < m_Keies1.Length; i++)
            {
                if (m_Keies1[i].Equals(key1) && m_Keies2[i].Equals(key2))
                {
                    return m_Values[i];
                }
            }

            return DefaultValue;
        }

        /// <summary>
        /// 查询指定Key2与Value对应的Key1列表
        /// </summary>
        /// <param name="key2"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public IEnumerable<TKey1> FindKey1(TKey2 key2, TValue value)
        {
            for (int i = 0; i < m_Keies1.Length; i++)
            {
                if (m_Keies2[i].Equals(key2) && m_Values[i].Equals(value))
                {
                    yield return m_Keies1[i];
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
            for (int i = 0; i < m_Keies1.Length; i++)
            {
                if (m_Keies1[i].Equals(key1) && m_Values[i].Equals(value))
                {
                    yield return m_Keies2[i];
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
            for (int i = 0; i < m_Keies1.Length; i++)
            {
                if (predicate(m_Keies1[i], m_Keies2[i], m_Values[i]))
                {
                    yield return Tuple.Create(m_Keies1[i], m_Keies2[i], m_Values[i]);
                }
            }
        }

        public override void RemoveAllDataRows()
        {
            m_Keies1 = Array.Empty<TKey1>();
            m_Keies2 = Array.Empty<TKey2>();
            m_Values = Array.Empty<TValue>();
        }

        internal override void Shutdown()
        {
            m_Keies1 = Array.Empty<TKey1>();
            m_Keies2 = Array.Empty<TKey2>();
            m_Values = Array.Empty<TValue>();
        }
    }
}
