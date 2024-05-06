using System;
using System.IO;
using DataTables;

namespace Benchmark;

public class DataMatrix2 : DataMatrixBase<Key1Enum, Key2Enum, ValueEnum>
{
    public DataMatrix2(string name) : base(name)
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
