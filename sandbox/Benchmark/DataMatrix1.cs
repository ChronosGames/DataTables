using System;
using System.IO;
using DataTables;

namespace Benchmark;

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
    public DataMatrix1(string name) : base(name)
    {
        base.InitDataSet((int)Key1Enum.MAX * (int)Key2Enum.MAX);

        for (int i = 0; i < (int)Key1Enum.MAX; i++)
        {
            for (int j = 0; j < (int)Key2Enum.MAX; j++)
            {
                base.AddDataSet(i * (int)Key2Enum.MAX + j, (Key1Enum)i, (Key2Enum)j, (ValueEnum)Random.Shared.Next((int)ValueEnum.MAX));
            }
        }
    }

    protected override bool Deserialize(int index, BinaryReader reader)
    {
        throw new NotImplementedException();
    }
}
