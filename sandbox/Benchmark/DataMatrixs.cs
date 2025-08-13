using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using DataTables;

namespace Benchmark;

public class Key1Comparer : IEqualityComparer<Key1Enum>
{
    public bool Equals(Key1Enum x, Key1Enum y)
    {
        return (int)x == (int)y;
    }

    public int GetHashCode([DisallowNull] Key1Enum obj)
    {
        return (int)obj;
    }
}

public enum Key1Enum : int
{
    Key01 = 0,
    Key02,
    Key03,
    Key04,
    Key05,
    Key06,
    Key07,
    Key08,
    Key09,
    Key10,
    Key11,
    MAX,
}

public class Key2Comparer : IEqualityComparer<Key2Enum>
{
    public bool Equals(Key2Enum x, Key2Enum y)
    {
        return (int)x == (int)y;
    }

    public int GetHashCode([DisallowNull] Key2Enum obj)
    {
        return (int)obj;
    }
}

public enum Key2Enum : int
{
    Key21 = 0,
    Key22,
    Key23,
    Key24,
    Key25,
    Key26,
    Key27,
    Key28,
    Key29,
    MAX,
}

public enum ValueEnum : int
{
    Value1 = 0,
    Value2,
    Value3,
    MAX,
}

public class DataMatrix1 : DataMatrixBase1<Key1Enum, Key2Enum, ValueEnum>
{
    public DataMatrix1(string name) : base(name, (int)Key1Enum.MAX * (int)Key2Enum.MAX)
    {
        for (int i = 0; i < (int)Key1Enum.MAX; i++)
        {
            for (int j = 0; j < (int)Key2Enum.MAX; j++)
            {
                base.SetDataRow(i * (int)Key2Enum.MAX + j, (Key1Enum)i, (Key2Enum)j, (ValueEnum)Random.Shared.Next((int)ValueEnum.MAX));
            }
        }
    }

    public override bool ParseDataRow(int index, BinaryReader reader)
    {
        throw new NotSupportedException();
    }
}

public class DataMatrix2 : DataMatrixBase<Key1Enum, Key2Enum, ValueEnum>
{
    public DataMatrix2(string name) : base(name, (int)Key1Enum.MAX * (int)Key2Enum.MAX)
    {
        for (int i = 0; i < (int)Key1Enum.MAX; i++)
        {
            for (int j = 0; j < (int)Key2Enum.MAX; j++)
            {
                SetDataRow(i * (int)Key2Enum.MAX + j, (Key1Enum)i, (Key2Enum)j, (ValueEnum)Random.Shared.Next((int)ValueEnum.MAX));
            }
        }
    }

    public override void OnLoadCompleted()
    {
        m_Key1Comparer = new Key1Comparer();
        m_Key2Comparer = new Key2Comparer();
    }
}

public class DataMatrix3 : DataMatrixBase1<Key1Enum, Key2Enum, int[]>
{
    public DataMatrix3(string name) : base(name, (int)Key1Enum.MAX * (int)Key2Enum.MAX)
    {
        for (int i = 0; i < (int)Key1Enum.MAX; i++)
        {
            for (int j = 0; j < (int)Key2Enum.MAX; j++)
            {
                SetDataRow(i * (int)Key2Enum.MAX + j, (Key1Enum)i, (Key2Enum)j, new int[] { Random.Shared.Next(1000, 10000) });
            }
        }
    }

    public override bool ParseDataRow(int index, BinaryReader reader) => throw new NotSupportedException();
}

public class DataMatrix4 : DataMatrixBase<Key1Enum, Key2Enum, int[]>
{
    public DataMatrix4(string name) : base(name, (int)Key1Enum.MAX * (int)Key2Enum.MAX)
    {
        for (int i = 0; i < (int)Key1Enum.MAX; i++)
        {
            for (int j = 0; j < (int)Key2Enum.MAX; j++)
            {
                SetDataRow(i * (int)Key2Enum.MAX + j, (Key1Enum)i, (Key2Enum)j, new int[] { Random.Shared.Next(1000, 10000) });
            }
        }
    }
}

public class DataMatrix6 : DataMatrixBase<string, Key2Enum, int[]>
{
    public DataMatrix6(string name) : base(name, (int)Key1Enum.MAX * (int)Key2Enum.MAX)
    {
        for (int i = 0; i < (int)Key1Enum.MAX; i++)
        {
            for (int j = 0; j < (int)Key2Enum.MAX; j++)
            {
                SetDataRow(i * (int)Key2Enum.MAX + j, Convert.ToString(i), (Key2Enum)j, new int[] { Random.Shared.Next(1000, 10000) });
            }
        }
    }
}
