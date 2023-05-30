// <auto-generated />
#pragma warning disable CS0105
using System;
using System.IO;
using System.Collections.Generic;
using DataTables;


namespace ConsoleApp
{
    public sealed partial class DTDataTableSample : DataTable<DRDataTableSample>
    {
        private Dictionary<int, DRDataTableSample> m_Dict1 = new Dictionary<int, DRDataTableSample>();
        private Dictionary<ConsoleApp.ColorT, DRDataTableSample> m_Dict2 = new Dictionary<ConsoleApp.ColorT, DRDataTableSample>();
        private MultiDictionary<int, short, DRDataTableSample> m_Dict3 = new MultiDictionary<int, short, DRDataTableSample>();
        private MultiDictionary<string, bool, List<DRDataTableSample>> m_Dict4 = new MultiDictionary<string, bool, List<DRDataTableSample>>();
        private Dictionary<string, List<DRDataTableSample>> m_Dict5 = new Dictionary<string, List<DRDataTableSample>>();
        public DTDataTableSample() : base() { }
        public DTDataTableSample(string name) : base(name) { }
        public DRDataTableSample GetDataRowById(int Id)
        {
            if (m_Dict1.TryGetValue(Id, out var result))
            {
                return result;
            }
            else
            {
#if DT_CHECK_NOT_FOUND && UNITY_EDITOR
                UnityEngine.Debug.LogWarningFormat("DTDataTableSample not found index: Id={0}", Id);
#endif
                return null;
            }
        }
        public DRDataTableSample GetDataRowByColor(ConsoleApp.ColorT Color)
        {
            if (m_Dict2.TryGetValue(Color, out var result))
            {
                return result;
            }
            else
            {
#if DT_CHECK_NOT_FOUND && UNITY_EDITOR
                UnityEngine.Debug.LogWarningFormat("DTDataTableSample not found index: Color={0}", Color);
#endif
                return null;
            }
        }
        public DRDataTableSample GetDataRowByIdAndInt16Value(int Id, short Int16Value)
        {
            if (m_Dict3.TryGetValue(Id, Int16Value, out var result))
            {
                return result;
            }
            else
            {
#if DT_CHECK_NOT_FOUND && UNITY_EDITOR
                UnityEngine.Debug.LogWarningFormat("DTDataTableSample not found index: Id={0}, Int16Value={1}", Id, Int16Value);
#endif
                return null;
            }
        }
        public List<DRDataTableSample> GetDataRowsGroupByNameAndBoolValue(string Name, bool BoolValue)
        {
            return m_Dict4.TryGetValue(Name, BoolValue, out var result) ? result : null;
        }
        public List<DRDataTableSample> GetDataRowsGroupByName(string Name)
        {
            return m_Dict5.TryGetValue(Name, out var result) ? result : null;
        }

        protected override void InternalAddDataRow(int index, DRDataTableSample dataRow)
        {
            base.InternalAddDataRow(index, dataRow);

            m_Dict1.Add(dataRow.Id, dataRow);
            m_Dict2.Add(dataRow.Color, dataRow);
            m_Dict3.Add(dataRow.Id, dataRow.Int16Value, dataRow);
            {
                if (m_Dict4.TryGetValue(dataRow.Name, dataRow.BoolValue, out var arr))
                {
                    arr.Add(dataRow);
                }
                else
                {
                    arr = new List<DRDataTableSample>();
                    arr.Add(dataRow);
                    m_Dict4.Add(dataRow.Name, dataRow.BoolValue, arr);
                }
            }
            {
                if (m_Dict5.TryGetValue(dataRow.Name, out var arr))
                {
                    arr.Add(dataRow);
                }
                else
                {
                    arr = new List<DRDataTableSample>();
                    arr.Add(dataRow);
                    m_Dict5.Add(dataRow.Name, arr);
                }
            }
        }
    }

    /// <summary>示例表</summary>
    public sealed partial class DRDataTableSample : DataRowBase
    {
        /// <summary>整数</summary>
        public int Id { get; private set; }
        /// <summary>小整数</summary>
        public short Int16Value { get; private set; }
        /// <summary>大整数</summary>
        public long Int64Value { get; private set; }
        /// <summary>无符号大整数</summary>
        public ulong UInt64Value { get; private set; }
        /// <summary>字符串</summary>
        public string Name { get; private set; }
        /// <summary>布尔</summary>
        public bool BoolValue { get; private set; }
        /// <summary>枚举</summary>
        public ConsoleApp.ColorT Color { get; private set; }
        /// <summary>数组</summary>
        public int[] ArrayValue { get; private set; }
        /// <summary>
        /// 二维数组
        /// <para>使用标准JSON格式串</para>
        /// <para>备注1字符串</para>
        /// </summary>
        public int[][] Array2DValue { get; private set; }
        /// <summary>三维数组</summary>
        public int[][][] Array3DValue { get; private set; }
        /// <summary>枚举与整形的字典</summary>
        public Dictionary<ColorT, int> MapEnumToInt { get; private set; }
        /// <summary>枚举数组</summary>
        public ColorT[] EnumArray { get; private set; }
        /// <summary>JSON类</summary>
        /// <remarks>
        /// 批注示例文本，请注意查阅！
        /// <para>&lt;color&gt;Red&lt;/color&gt;</para>
        /// <para>&lt;b&gt;abc&lt;/b&gt;</para>
        /// </remarks>
        public SampleParent CustomJSON { get; private set; }
        /// <summary>自定义类</summary>
        public CustomSample CustomFieldType { get; private set; }

        public override bool Deserialize(BinaryReader reader)
        {
            Id = reader.Read7BitEncodedInt32();
            Int16Value = reader.ReadInt16();
            Int64Value = reader.Read7BitEncodedInt64();
            UInt64Value = reader.Read7BitEncodedUInt64();
            Name = reader.ReadString();
            BoolValue = reader.ReadBoolean();
            {
                ConsoleApp.ColorT __enumVal = default;
                var __enumStr = reader.ReadString();
                if (!string.IsNullOrEmpty(__enumStr) && !Enum.TryParse(__enumStr, out __enumVal))
                {
                    throw new ArgumentException();
                }
                Color = __enumVal;
            }
            {
                var __ArrayValue_Count1 = reader.Read7BitEncodedInt32();
                ArrayValue = new int[__ArrayValue_Count1];
                for (int x1 = 0; x1 < __ArrayValue_Count1; x1++)
                {
                    int key1;
                    key1 = reader.Read7BitEncodedInt32();
                    ArrayValue[x1] = key1;
                }
            }
            {
                var __Array2DValue_Count1 = reader.Read7BitEncodedInt32();
                Array2DValue = new int[__Array2DValue_Count1][];
                for (int x1 = 0; x1 < __Array2DValue_Count1; x1++)
                {
                    int[] key1;
                    {
                        var __key1_Count3 = reader.Read7BitEncodedInt32();
                        key1 = new int[__key1_Count3];
                        for (int x3 = 0; x3 < __key1_Count3; x3++)
                        {
                            int key3;
                            key3 = reader.Read7BitEncodedInt32();
                            key1[x3] = key3;
                        }
                    }
                    Array2DValue[x1] = key1;
                }
            }
            {
                var __Array3DValue_Count1 = reader.Read7BitEncodedInt32();
                Array3DValue = new int[__Array3DValue_Count1][][];
                for (int x1 = 0; x1 < __Array3DValue_Count1; x1++)
                {
                    int[][] key1;
                    {
                        var __key1_Count3 = reader.Read7BitEncodedInt32();
                        key1 = new int[__key1_Count3][];
                        for (int x3 = 0; x3 < __key1_Count3; x3++)
                        {
                            int[] key3;
                            {
                                var __key3_Count5 = reader.Read7BitEncodedInt32();
                                key3 = new int[__key3_Count5];
                                for (int x5 = 0; x5 < __key3_Count5; x5++)
                                {
                                    int key5;
                                    key5 = reader.Read7BitEncodedInt32();
                                    key3[x5] = key5;
                                }
                            }
                            key1[x3] = key3;
                        }
                    }
                    Array3DValue[x1] = key1;
                }
            }
            {
                MapEnumToInt = new Dictionary<ColorT, int>();
                var __MapEnumToInt_Count1 = reader.Read7BitEncodedInt32();
                for (int x1 = 0; x1 < __MapEnumToInt_Count1; x1++)
                {
                    ColorT key1;
                    {
                        ColorT __enumVal = default;
                        var __enumStr = reader.ReadString();
                        if (!string.IsNullOrEmpty(__enumStr) && !Enum.TryParse(__enumStr, out __enumVal))
                        {
                            throw new ArgumentException();
                        }
                        key1 = __enumVal;
                    }
                    int value1;
                    value1 = reader.Read7BitEncodedInt32();
                    MapEnumToInt.Add(key1, value1);
                }
            }
            {
                var __EnumArray_Count1 = reader.Read7BitEncodedInt32();
                EnumArray = new ColorT[__EnumArray_Count1];
                for (int x1 = 0; x1 < __EnumArray_Count1; x1++)
                {
                    ColorT key1;
                    {
                        ColorT __enumVal = default;
                        var __enumStr = reader.ReadString();
                        if (!string.IsNullOrEmpty(__enumStr) && !Enum.TryParse(__enumStr, out __enumVal))
                        {
                            throw new ArgumentException();
                        }
                        key1 = __enumVal;
                    }
                    EnumArray[x1] = key1;
                }
            }
            {
                var __jsonStr = reader.ReadString();
                CustomJSON = Utility.Json.ToObject<SampleParent>(__jsonStr);
            }
            {
                var __jsonStr = reader.ReadString();
                CustomFieldType = new CustomSample(__jsonStr);
            }
            return true;
        }
    }
    
}
