// <auto-generated />
#pragma warning disable CS0105
using System;
using System.Collections.Generic;
using DataTables;

#nullable enable

namespace ConsoleApp
{
public static class DataTableManagerExtension
{
    public static readonly Dictionary<string, string[]> Tables = new Dictionary<string, string[]>
    {
        { "ConsoleApp.DTDataTableSample", Array.Empty<string>() },
        { "ConsoleApp.DTDataTableSplitSample", new string[] {"x001", "x002"} },
        { "ConsoleApp.DTMatrixSample", Array.Empty<string>() },
    };

    /// <summary>
    /// 预加载所有数据表。
    /// </summary>
    /// <param name="manager"></param>
    /// <param name="onCompleted">全部数据表预加载完成时回调。</param>
    /// <param name="onProgress">单步加载完成时回调。</param>
    public static void Preload(this DataTableManager manager, Action? onCompleted = default, Action<float>? onProgress = default)
    {
        const int total = 4;
        int done = 0;

        void next()
        {
            done++;
            onProgress?.Invoke((float)done / total);
            if (done == total)
            {
                onCompleted?.Invoke();
            }
        };

        manager.CreateDataTable<ConsoleApp.DTDataTableSample>(next);
        manager.CreateDataTable<ConsoleApp.DTDataTableSplitSample>("x001", next);
        manager.CreateDataTable<ConsoleApp.DTDataTableSplitSample>("x002", next);
        manager.CreateDataTable<ConsoleApp.DTMatrixSample>(next);
    }
}
}
