using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace DataTables
{
    public abstract class DataTable<T> : DataTableBase, IDataTable<T> where T : class, IDataRow, new()
    {
        private readonly T[] m_DataSet;

        /// <summary>
        /// 初始化数据表的新实例。
        /// </summary>
        /// <param name="name">数据表名称。</param>
        /// <param name="capacity">数据表容量。</param>
        public DataTable(string name, int capacity)
            : base(name)
        {
            m_DataSet = new T[capacity];
        }

        /// <summary>
        /// 获取数据表行的类型。
        /// </summary>
        public override Type Type
        {
            get
            {
                return typeof(T);
            }
        }

        /// <summary>
        /// 获取数据表行数。
        /// </summary>
        public override int Count => m_DataSet.Length;

        /// <summary>
        /// 获取第几行的数据表行。
        /// </summary>
        /// <param name="key">指定的行数，必须在[0, Count)范围内</param>
        /// <returns></returns>
        public T this[int key] => m_DataSet[key];

        /// <summary>
        /// 检查是否存在数据表行。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <returns>是否存在数据表行。</returns>
        public bool HasDataRow(Predicate<T> condition)
        {
            if (condition == null)
            {
                throw new Exception("Condition is invalid.");
            }

            foreach (var dataRow in m_DataSet)
            {
                if (condition(dataRow))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// 获取符合条件的数据表行。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <returns>符合条件的数据表行。</returns>
        /// <remarks>当存在多个符合条件的数据表行时，仅返回第一个符合条件的数据表行。</remarks>
        public T? GetDataRow(Predicate<T> condition)
        {
            if (condition == null)
            {
                throw new Exception("Condition is invalid.");
            }

            foreach (var dataRow in m_DataSet)
            {
                if (condition(dataRow))
                {
                    return dataRow;
                }
            }

            return null;
        }

        /// <summary>
        /// 获取符合条件的数据表行。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <returns>符合条件的数据表行。</returns>
        public IEnumerable<T> GetDataRows(Predicate<T> condition)
        {
            if (condition == null)
            {
                throw new Exception("Condition is invalid.");
            }

            return m_DataSet.Where(x => condition(x));
        }

        /// <summary>
        /// 获取符合条件的数据表行。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <param name="results">符合条件的数据表行。</param>
        public void GetDataRows(Predicate<T> condition, List<T> results)
        {
            if (condition == null)
            {
                throw new Exception("Condition is invalid.");
            }

            if (results == null)
            {
                throw new Exception("Results is invalid.");
            }

            results.Clear();
            foreach (var dataRow in m_DataSet)
            {
                if (condition(dataRow))
                {
                    results.Add(dataRow);
                }
            }
        }

        /// <summary>
        /// 获取排序后的数据表行。
        /// </summary>
        /// <param name="comparison">要排序的条件。</param>
        /// <returns>排序后的数据表行。</returns>
        public T[] GetDataRows(Comparison<T> comparison)
        {
            if (comparison == null)
            {
                throw new Exception("Comparison is invalid.");
            }

            if (m_DataSet.Length == 0)
            {
                return Array.Empty<T>();
            }

            var result = new T[m_DataSet.Length];
            Array.Copy(m_DataSet, result, m_DataSet.Length);
            Array.Sort(m_DataSet, comparison);
            return result;
        }

        /// <summary>
        /// 获取排序后的数据表行。
        /// </summary>
        /// <param name="comparison">要排序的条件。</param>
        /// <param name="results">排序后的数据表行。</param>
        public void GetDataRows(Comparison<T> comparison, List<T> results)
        {
            if (comparison == null)
            {
                throw new Exception("Comparison is invalid.");
            }

            if (results == null)
            {
                throw new Exception("Results is invalid.");
            }

            results.Clear();
            results.AddRange(m_DataSet);
            results.Sort(comparison);
        }

        /// <summary>
        /// 获取排序后的符合条件的数据表行。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <param name="comparison">要排序的条件。</param>
        /// <returns>排序后的符合条件的数据表行。</returns>
        public IOrderedEnumerable<T> GetDataRows(Predicate<T> condition, Comparison<T> comparison)
        {
            if (condition == null)
            {
                throw new Exception("Condition is invalid.");
            }

            if (comparison == null)
            {
                throw new Exception("Comparison is invalid.");
            }

            return m_DataSet.Where(x => condition(x)).OrderBy(_ => comparison);
        }

        /// <summary>
        /// 获取排序后的符合条件的数据表行。
        /// </summary>
        /// <param name="condition">要检查的条件。</param>
        /// <param name="comparison">要排序的条件。</param>
        /// <param name="results">排序后的符合条件的数据表行。</param>
        public void GetDataRows(Predicate<T> condition, Comparison<T> comparison, List<T> results)
        {
            if (condition == null)
            {
                throw new Exception("Condition is invalid.");
            }

            if (comparison == null)
            {
                throw new Exception("Comparison is invalid.");
            }

            if (results == null)
            {
                throw new Exception("Results is invalid.");
            }

            results.Clear();
            results.AddRange(m_DataSet.Where(x => condition(x)));

            results.Sort(comparison);
        }

        /// <summary>
        /// 获取所有数据表行。
        /// </summary>
        /// <returns>所有数据表行。</returns>
        public T[] GetAllDataRows()
        {
            return m_DataSet;
        }

        /// <summary>
        /// 获取所有数据表行。
        /// </summary>
        /// <param name="results">所有数据表行。</param>
        public void GetAllDataRows(List<T> results)
        {
            if (results == null)
            {
                throw new Exception("Results is invalid.");
            }

            results.Clear();
            results.AddRange(m_DataSet);
        }

        /// <summary>
        /// 增加数据表行。
        /// </summary>
        /// <param name="index">将要设置的数据表所在行索引。</param>
        /// <param name="binaryReader">要解析的数据表行二进制流。</param>
        /// <returns>是否增加数据表行成功。</returns>
        public override bool ParseDataRow(int index, BinaryReader binaryReader)
        {
            try
            {
                T dataRow = new T();
                if (!dataRow.Deserialize(binaryReader))
                {
                    return false;
                }

                InternalAddDataRow(index, dataRow);
                return true;
            }
            catch (Exception exception)
            {
                throw new Exception(string.Format("Can not parse data row bytes for data table '{0}' with exception '{1}'.", typeof(T), exception), exception);
            }
        }

        /// <summary>
        /// 清空所有数据表行。
        /// </summary>
        public override void RemoveAllDataRows()
        { }

        /// <summary>
        /// 返回循环访问集合的枚举数。
        /// </summary>
        /// <returns>循环访问集合的枚举数。</returns>
        public IEnumerator<T> GetEnumerator()
        {
            return ((IEnumerable<T>)m_DataSet).GetEnumerator();
        }

        /// <summary>
        /// 返回循环访问集合的枚举数。
        /// </summary>
        /// <returns>循环访问集合的枚举数。</returns>
        IEnumerator IEnumerable.GetEnumerator()
        {
            return m_DataSet.GetEnumerator();
        }

        /// <summary>
        /// 关闭并清理数据表。
        /// </summary>
        internal override void Shutdown()
        { }

        protected virtual void InternalAddDataRow(int index, T dataRow)
        {
            m_DataSet[index] = dataRow;
        }
    }
}
