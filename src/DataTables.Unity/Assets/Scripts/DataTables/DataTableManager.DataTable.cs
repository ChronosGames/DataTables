using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace DataTables
{
    public sealed partial class DataTableManager : IDataTableManager
    {
        private sealed class DataTable<T> : DataTableBase, IDataTable<T> where T : class, IDataRow, new()
        {
            private readonly List<T> m_DataSet;

            /// <summary>
            /// 初始化数据表的新实例。
            /// </summary>
            /// <param name="name">数据表名称。</param>
            public DataTable(string name)
                : base(name)
            {
                m_DataSet = new List<T>();
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
            public override int Count
            {
                get
                {
                    return m_DataSet.Count;
                }
            }

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
            public T GetDataRow(Predicate<T> condition)
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
            public T[] GetDataRows(Predicate<T> condition)
            {
                if (condition == null)
                {
                    throw new Exception("Condition is invalid.");
                }

                List<T> results = new List<T>();
                foreach (var dataRow in m_DataSet)
                {
                    if (condition(dataRow))
                    {
                        results.Add(dataRow);
                    }
                }

                return results.ToArray();
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

                List<T> results = new List<T>();
                foreach (var dataRow in m_DataSet)
                {
                    results.Add(dataRow);
                }

                results.Sort(comparison);
                return results.ToArray();
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
                foreach (var dataRow in m_DataSet)
                {
                    results.Add(dataRow);
                }

                results.Sort(comparison);
            }

            /// <summary>
            /// 获取排序后的符合条件的数据表行。
            /// </summary>
            /// <param name="condition">要检查的条件。</param>
            /// <param name="comparison">要排序的条件。</param>
            /// <returns>排序后的符合条件的数据表行。</returns>
            public T[] GetDataRows(Predicate<T> condition, Comparison<T> comparison)
            {
                if (condition == null)
                {
                    throw new Exception("Condition is invalid.");
                }

                if (comparison == null)
                {
                    throw new Exception("Comparison is invalid.");
                }

                List<T> results = new List<T>();
                foreach (var dataRow in m_DataSet)
                {
                    if (condition(dataRow))
                    {
                        results.Add(dataRow);
                    }
                }

                results.Sort(comparison);
                return results.ToArray();
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
                foreach (var dataRow in m_DataSet)
                {
                    if (condition(dataRow))
                    {
                        results.Add(dataRow);
                    }
                }

                results.Sort(comparison);
            }

            /// <summary>
            /// 获取所有数据表行。
            /// </summary>
            /// <returns>所有数据表行。</returns>
            public T[] GetAllDataRows()
            {
                int index = 0;
                T[] results = new T[m_DataSet.Count];
                foreach (var dataRow in m_DataSet)
                {
                    results[index++] = dataRow;
                }

                return results;
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
                foreach (var dataRow in m_DataSet)
                {
                    results.Add(dataRow);
                }
            }

            /// <summary>
            /// 增加数据表行。
            /// </summary>
            /// <param name="binaryReader">要解析的数据表行二进制流。</param>
            /// <returns>是否增加数据表行成功。</returns>
            public override bool AddDataRow(BinaryReader binaryReader)
            {
                try
                {
                    T dataRow = new T();
                    if (!dataRow.Deserialize(binaryReader))
                    {
                        return false;
                    }

                    InternalAddDataRow(dataRow);
                    return true;
                }
                catch (Exception exception)
                {
                    if (exception is Exception)
                    {
                        throw;
                    }

                    throw new Exception(string.Format("Can not parse data row bytes for data table '{0}' with exception '{1}'.", new TypeNamePair(typeof(T), Name), exception), exception);
                }
            }

            /// <summary>
            /// 清空所有数据表行。
            /// </summary>
            public override void RemoveAllDataRows()
            {
                m_DataSet.Clear();
            }

            /// <summary>
            /// 返回循环访问集合的枚举数。
            /// </summary>
            /// <returns>循环访问集合的枚举数。</returns>
            public IEnumerator<T> GetEnumerator()
            {
                return m_DataSet.GetEnumerator();
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
            {
                m_DataSet.Clear();
            }

            private void InternalAddDataRow(T dataRow)
            {
                //if (m_DataSet.Contains(dataRow))
                //{
                //    throw new Exception(string.Format("Already exist in data table '{0}'.", new TypeNamePair(typeof(T), Name)));
                //}

                m_DataSet.Add(dataRow);
            }
        }
    }
}
