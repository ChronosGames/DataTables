// <auto-generated />
#pragma warning disable CS0105
using System;
using System.IO;
using System.Collections.Generic;
using DataTables;

public sealed partial class DTMatrixSample : DataMatrixBase<short, long, bool>
{
    protected override bool DefaultValue => true;

    protected override bool Deserialize(BinaryReader reader)
    {
        var _key1 = reader.ReadInt16();
        var _key2 = reader.Read7BitEncodedInt64();
        var _value = reader.ReadBoolean();
        AddDataSet(_key1, _key2, _value);
        return true;
    }
}
