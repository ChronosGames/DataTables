using System;
using System.Diagnostics;
using DataTables;

namespace ConsoleApp;

public enum ColorT
{
    Red,
    Green,
    Blue
}

internal static class Program
{
    private static void Main(string[] args)
    {
        var manager = new DataTableManager();
        manager.SetDataTableHelper(new DefaultDataTableHelper("DataTables"));
        manager.Preload(() => Console.WriteLine("数据表全部加载完毕"));

        Debug.Assert(manager.HasDataTable<DTDataTableSample>(), "加载配置表失败");

        var dtSample = manager.GetDataTable<DTDataTableSample>();
        Debug.Assert(dtSample!.GetAllDataRows()[dtSample.Count - 1].CustomFieldType.Raw == "aaa");
        Debug.Assert(dtSample.GetDataRowById(1) != null, "加载配置表失败1");
        Debug.Assert(dtSample.GetDataRowById(3)!.ArrayStringValue.Length == 2 && dtSample.GetDataRowById(3)!.ArrayStringValue[0] == "a", "加载配置表失败2");
        manager.DestroyDataTable(dtSample);

        Debug.Assert(manager.GetDataTable<DTMatrixSample>() != null, "加载MatrixSample失败");
        Debug.Assert(manager.GetDataTable<DTMatrixSample>()!.Get(2, 1) == false, "加载MatrixSample失败");
        Debug.Assert(manager.GetDataTable<DTMatrixSample>()!.Get(5, 3) == true, "加载MatrixSample失败");
    }
}
