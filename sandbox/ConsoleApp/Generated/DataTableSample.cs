// <auto-generated />
#pragma warning disable CS0105

using System;
using System.IO;
using System.Collections.Generic;
using DataTables;

namespace ConsoleApp
{
    public sealed class DataTableSample : DataRowBase
    {
        /// <summary>整数</summary>
        public int Id { get; private set; }
        /// <summary>字符串</summary>
        public string Name { get; private set; }
        /// <summary>枚举</summary>
        public ColorT Color { get; private set; }
        /// <summary>数组</summary>
        public int[] ArrayValue { get; private set; }
        /// <summary>字典</summary>
        public Dictionary<int, int> MapValue { get; private set; }
        /// <summary>枚举与整形的字典</summary>
        public Dictionary<ColorT, int> MapEnumToInt { get; private set; }
        /// <summary>枚举数组</summary>
        public ColorT[] EnumArray { get; private set; }

        public override bool Deserialize(BinaryReader reader)
        {
            //using (MemoryStream stream = new MemoryStream(raw, offset, length, false))
            {
                //using (BinaryReader reader = new BinaryReader(stream))
                {
                    Id = reader.Read7BitEncodedInt32();
                    Name = reader.ReadString();
                    {
                        ColorT __enumVal = default;
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
                        MapValue = new Dictionary<int, int>();
                        var __MapValue_Count1 = reader.Read7BitEncodedInt32();
                        for (int x1 = 0; x1 < __MapValue_Count1; x1++)
                        {
                            int key1;
                            key1 = reader.Read7BitEncodedInt32();
                            int value1;
                            value1 = reader.Read7BitEncodedInt32();
                            MapValue.Add(key1, value1);
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
                }
            }

            return true;
        }
    }
}