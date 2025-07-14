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
        // 测试Hook机制
        DataTableManager.HookDataTableLoaded<DTDataTableSample>(table =>
        {
            Console.WriteLine($"DTDataTableSample已加载，包含 {table.Count} 行数据");
        });
        
        DataTableManager.HookGlobalDataTableLoaded(table =>
        {
            Console.WriteLine($"数据表 {table.GetType().Name} 已加载");
        });
        
        // 使用新的静态API
        DataTableManager.SetDataTableHelper(new DefaultDataTableHelper("DataTables"));
        DataTableManagerExtension.Preload(() => Console.WriteLine("数据表全部加载完毕"));

        Debug.Assert(DataTableManager.HasDataTable<DTDataTableSample>(), "加载配置表失败");

        // 使用新的静态API直接获取数据
        var row1 = DTDataTableSample.GetDataRowById(1);
        Debug.Assert(row1 != null, "加载配置表失败1");
        
        var row3 = DTDataTableSample.GetDataRowById(3);
        Debug.Assert(row3!.ArrayStringValue.Length == 2 && row3.ArrayStringValue[0] == "a", "加载配置表失败2");
        
        var dtSample = DataTableManager.GetDataTable<DTDataTableSample>();
        Debug.Assert(dtSample!.GetAllDataRows()[dtSample.Count - 1].CustomFieldType.Raw == "aaa");
        
        DataTableManager.DestroyDataTable(dtSample);

        Debug.Assert(DataTableManager.GetDataTable<DTMatrixSample>() != null, "加载MatrixSample失败");
        Debug.Assert(DataTableManager.GetDataTable<DTMatrixSample>()!.Get(2, 1) == false, "加载MatrixSample失败");
        Debug.Assert(DataTableManager.GetDataTable<DTMatrixSample>()!.Get(5, 3) == true, "加载MatrixSample失败");
    }
}
