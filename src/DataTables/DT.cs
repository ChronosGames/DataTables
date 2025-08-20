// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace DataTables;

public static class DT<T> where T : DataTableBase
{
    public static T Instance => DataTableManager.GetCached<T>() ?? throw new InvalidOperationException($"{typeof(T).Name} not loaded");

    public static T? InstanceOrNull => DataTableManager.GetCached<T>();
    public static bool IsLoaded => DataTableManager.HasDataTable<T>();
}
