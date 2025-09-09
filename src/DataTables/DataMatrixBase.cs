using System;
using System.Collections.Generic;
using System.IO;

namespace DataTables
{
    public abstract class DataMatrixBase<TKey1, TKey2, TValue> : DataTableBase
    {
        private readonly TKey1[] m_Keies1;
        private readonly TKey2[] m_Keies2;
        private readonly TValue[] m_Values;

        protected IEqualityComparer<TKey1> m_Key1Comparer = EqualityComparer<TKey1>.Default;
        protected IEqualityComparer<TKey2> m_Key2Comparer = EqualityComparer<TKey2>.Default;
        protected IEqualityComparer<TValue> m_ValueComparer = EqualityComparer<TValue>.Default;

        protected virtual TValue DefaultValue => default;

        public override Type Type => typeof(TValue);

        public override int Count => m_Keies1.Length;

        public DataMatrixBase(string name, int capacity) : base(name)
        {
            m_Keies1 = new TKey1[capacity];
            m_Keies2 = new TKey2[capacity];
            m_Values = new TValue[capacity];
        }

        public override bool ParseDataRow(int index, BinaryReader reader) => throw new NotSupportedException();

        protected void SetDataRow(int index, TKey1 key1, TKey2 key2, TValue value)
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
                if (m_Key1Comparer.Equals(m_Keies1[i], key1) && m_Key2Comparer.Equals(m_Keies2[i], key2))
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
                if (m_Key2Comparer.Equals(m_Keies2[i], key2) && m_ValueComparer.Equals(m_Values[i], value))
                {
                    yield return m_Keies1[i];
                }
            }
        }

        /// <summary>
        /// 查询指定Key2与Value对应的Key1列表（无GC版本）
        /// </summary>
        /// <param name="result">通过调用方提供的结果列表避免内部产生GC</param>
        /// <param name="key2"></param>
        /// <param name="value"></param>
        public void FindKey1(IList<TKey1> result, TKey2 key2, TValue value)
        {
            result.Clear();

            for (int i = 0; i < m_Keies1.Length; i++)
            {
                if (m_Key2Comparer.Equals(m_Keies2[i], key2) && m_ValueComparer.Equals(m_Values[i], value))
                {
                    result.Add(m_Keies1[i]);
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
                if (m_Key1Comparer.Equals(m_Keies1[i], key1) && m_ValueComparer.Equals(m_Values[i], value))
                {
                    yield return m_Keies2[i];
                }
            }
        }

        /// <summary>
        /// 查询指定Key1与Value对应的Key2列表（无GC版本）
        /// </summary>
        /// <param name="result">通过调用方提供的结果列表避免内部产生GC</param>
        /// <param name="key1"></param>
        /// <param name="value"></param>
        /// <returns></returns>
        public void FindKey2(IList<TKey2> result, TKey1 key1, TValue value)
        {
            result.Clear();

            for (int i = 0; i < m_Keies1.Length; i++)
            {
                if (m_Key1Comparer.Equals(m_Keies1[i], key1) && m_ValueComparer.Equals(m_Values[i], value))
                {
                    result.Add(m_Keies2[i]);
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

        /// <summary>
        /// 查询指定条件的结果（无GC版本）
        /// </summary>
        /// <param name="result">通过调用方提供的结果列表避免内部产生GC</param>
        /// <param name="predicate"></param>
        /// <returns></returns>
        public void Where(IList<Tuple<TKey1, TKey2, TValue>> result, Func<TKey1, TKey2, TValue, bool> predicate)
        {
            result.Clear();

            for (int i = 0; i < m_Keies1.Length; i++)
            {
                if (predicate(m_Keies1[i], m_Keies2[i], m_Values[i]))
                {
                    result.Add(Tuple.Create(m_Keies1[i], m_Keies2[i], m_Values[i]));
                }
            }
        }

        /// <summary>
        /// 获取所有数据行 - Matrix表的DataRow形式访问
        /// </summary>
        /// <returns></returns>
        public virtual IEnumerable<MatrixDataRowBase<TKey1, TKey2, TValue>> GetAllDataRows()
        {
            for (int i = 0; i < m_Keies1.Length; i++)
            {
                var dataRow = CreateDataRowInstance();
                dataRow.SetData(m_Keies1[i], m_Keies2[i], m_Values[i]);
                yield return dataRow;
            }
        }

        /// <summary>
        /// 根据索引获取数据行
        /// </summary>
        /// <param name="index">索引</param>
        /// <returns></returns>
        public virtual MatrixDataRowBase<TKey1, TKey2, TValue>? GetDataRowByIndex(int index)
        {
            if (index < 0 || index >= Count)
                return null;

            var dataRow = CreateDataRowInstance();
            dataRow.SetData(m_Keies1[index], m_Keies2[index], m_Values[index]);
            return dataRow;
        }

        /// <summary>
        /// 查找匹配条件的第一个数据行
        /// </summary>
        /// <param name="predicate">查找条件</param>
        /// <returns></returns>
        public virtual MatrixDataRowBase<TKey1, TKey2, TValue>? FindDataRow(Func<TKey1, TKey2, TValue, bool> predicate)
        {
            for (int i = 0; i < m_Keies1.Length; i++)
            {
                if (predicate(m_Keies1[i], m_Keies2[i], m_Values[i]))
                {
                    var dataRow = CreateDataRowInstance();
                    dataRow.SetData(m_Keies1[i], m_Keies2[i], m_Values[i]);
                    return dataRow;
                }
            }
            return null;
        }

        /// <summary>
        /// 查找所有匹配条件的数据行
        /// </summary>
        /// <param name="predicate">查找条件</param>
        /// <returns></returns>
        public virtual IEnumerable<MatrixDataRowBase<TKey1, TKey2, TValue>> FindDataRows(Func<TKey1, TKey2, TValue, bool> predicate)
        {
            for (int i = 0; i < m_Keies1.Length; i++)
            {
                if (predicate(m_Keies1[i], m_Keies2[i], m_Values[i]))
                {
                    var dataRow = CreateDataRowInstance();
                    dataRow.SetData(m_Keies1[i], m_Keies2[i], m_Values[i]);
                    yield return dataRow;
                }
            }
        }

        /// <summary>
        /// 创建数据行实例 - 由子类实现以提供具体的DataRow类型
        /// </summary>
        /// <returns></returns>
        protected abstract MatrixDataRowBase<TKey1, TKey2, TValue> CreateDataRowInstance();
    }
}
