using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests;

public sealed class PublicApiMetadataTests
{
    [Fact]
    public void RemovedCompatibilityMembers_ShouldNotExist()
    {
        var removedMembers = new (Type Type, string Name)[]
        {
            (typeof(DataTableManager), "IsMemoryManagementEnabled"),
            (typeof(DataTableManager), "UseCustomSource"),
            (typeof(DataTableManager), "EnableMemoryManagement"),
            (typeof(DataTableManager), "DisableMemoryManagement"),
            (typeof(DataTableManager), "HasDataTable"),
            (typeof(DataTableManager), "GetOrCreateDataTableAsync"),
            (typeof(DataTableManager), "CreateDataTableAsync"),
            (typeof(DataTableManager), "CreateDataTable"),
            (typeof(DataTableManager), "GetDataTable"),
            (typeof(DataTableManager), "GetDataTableInternal"),
            (typeof(DataTableContext), "IsMemoryManagementEnabled"),
            (typeof(DataTableContext), "EnableMemoryManagement"),
            (typeof(DataTableContext), "DisableMemoryManagement"),
            (typeof(DataTableContext), "HasDataTable"),
            (typeof(DataTableContext), "GetDataTable"),
            (typeof(DataTableContext), "CreateDataTable"),
            (typeof(CacheStats), "MemoryUsage"),
            (typeof(CacheStats), "MemoryUsageRate"),
        };

        foreach (var (type, name) in removedMembers)
        {
            type.GetMember(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
                .Should().BeEmpty($"{type.FullName}.{name} should be removed in the breaking API cleanup");
        }

        var assembly = typeof(DataTableManager).Assembly;
        assembly.GetType("DataTables.IDataTableManager").Should().BeNull();
        assembly.GetType("DataTables.DataTableStatus").Should().BeNull();
    }

    [Fact]
    public void RecommendedMembers_ShouldRemainPublicAndNotObsolete()
    {
        var members = new MemberInfo[]
        {
            FindProperty(typeof(DataTableManager), "IsEstimatedMemoryBudgetEnabled"),
            FindMethod(typeof(DataTableManager), "UseDataSource", 1),
            FindMethod(typeof(DataTableManager), "EnableEstimatedMemoryBudget", 2),
            FindMethod(typeof(DataTableManager), "DisableEstimatedMemoryBudget", 0),
            FindMethod(typeof(DataTableManager), "LoadAsync", 2, isGeneric: true),
            FindMethod(typeof(DataTableManager), "GetCached", 1, isGeneric: true),
            FindMethod(typeof(DataTableManager), "IsLoaded", 1, isGeneric: true),
            FindProperty(typeof(DataTableContext), "IsEstimatedMemoryBudgetEnabled"),
            FindMethod(typeof(DataTableContext), "UseDataSource", 1),
            FindMethod(typeof(DataTableContext), "EnableEstimatedMemoryBudget", 2),
            FindMethod(typeof(DataTableContext), "DisableEstimatedMemoryBudget", 0),
            FindMethod(typeof(DataTableContext), "LoadAsync", 2, isGeneric: true),
            FindMethod(typeof(DataTableContext), "GetCached", 1, isGeneric: true),
            FindMethod(typeof(DataTableContext), "IsLoaded", 1, isGeneric: true),
            FindMethod(typeof(IDataTableContext), "LoadAsync", 2, isGeneric: true),
            FindMethod(typeof(IDataTableContext), "GetCached", 1, isGeneric: true),
            FindMethod(typeof(IDataTableContext), "IsLoaded", 1, isGeneric: true),
            FindMethod(typeof(TableRegistration), "LoadAsync", 2),
            FindContextAwareRegistrationConstructor(),
            FindProperty(typeof(CacheStats), "EstimatedMemoryUsageBytes"),
            FindProperty(typeof(CacheStats), "EstimatedBudgetUsageRate"),
            typeof(IDataTableContext)
        };

        foreach (var member in members)
        {
            member.GetCustomAttribute<ObsoleteAttribute>().Should().BeNull(
                $"{member.DeclaringType?.FullName ?? member.Name}.{member.Name} is a recommended API");
        }
    }

    private static MethodInfo FindMethod(Type type, string name, int parameterCount, bool? isGeneric = null)
    {
        return type.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            .Single(method => method.Name == name
                && method.GetParameters().Length == parameterCount
                && (isGeneric == null || method.IsGenericMethodDefinition == isGeneric));
    }

    private static PropertyInfo FindProperty(Type type, string name)
        => type.GetProperty(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Property {type.FullName}.{name} was not found.");

    private static ConstructorInfo FindContextAwareRegistrationConstructor()
        => typeof(TableRegistration).GetConstructor(new[]
        {
            typeof(Type),
            typeof(string),
            typeof(Priority),
            typeof(Func<DataTableContext, CancellationToken, ValueTask<DataTableBase?>>)
        }) ?? throw new InvalidOperationException("Context-aware TableRegistration constructor was not found.");
}
