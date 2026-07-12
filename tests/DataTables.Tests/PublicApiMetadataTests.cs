using System;
using System.ComponentModel;
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
    public void CompatibilityMembers_ShouldBeObsoleteAndHiddenFromIntelliSense()
    {
        var members = new MemberInfo[]
        {
            FindProperty(typeof(DataTableManager), "IsMemoryManagementEnabled"),
            FindMethod(typeof(DataTableManager), "UseCustomSource", 1),
            FindMethod(typeof(DataTableManager), "EnableMemoryManagement", 1),
            FindMethod(typeof(DataTableManager), "DisableMemoryManagement", 0),
            FindMethod(typeof(DataTableManager), "LoadAsync", 1, isGeneric: true),
            FindMethod(typeof(DataTableManager), "HasDataTable", 0, isGeneric: true),
            FindMethod(typeof(DataTableManager), "GetOrCreateDataTableAsync", 0, isGeneric: true),
            FindMethod(typeof(DataTableManager), "GetOrCreateDataTableAsync", 2, isGeneric: true),
            FindMethod(typeof(DataTableManager), "CreateDataTableAsync", 2, isGeneric: true),
            FindMethod(typeof(DataTableManager), "CreateDataTable", 1, isGeneric: true),
            FindMethod(typeof(DataTableManager), "CreateDataTable", 2, isGeneric: true),
            FindMethod(typeof(DataTableManager), "GetDataTable", 0, isGeneric: true),
            FindProperty(typeof(DataTableContext), "IsMemoryManagementEnabled"),
            FindMethod(typeof(DataTableContext), "EnableMemoryManagement", 1),
            FindMethod(typeof(DataTableContext), "DisableMemoryManagement", 0),
            FindMethod(typeof(DataTableContext), "LoadAsync", 1, isGeneric: true),
            FindMethod(typeof(DataTableContext), "GetOrCreateDataTableAsync", 2, isGeneric: true),
            FindMethod(typeof(DataTableContext), "CreateDataTableAsync", 2, isGeneric: true),
            FindMethod(typeof(DataTableContext), "HasDataTable", 1, isGeneric: true),
            FindMethod(typeof(DataTableContext), "GetDataTable", 1, isGeneric: true),
            FindMethod(typeof(DataTableContext), "CreateDataTable", 1, isGeneric: true),
            FindMethod(typeof(DataTableContext), "CreateDataTable", 2, isGeneric: true),
            FindMethod(typeof(TableRegistration), "LoadAsync", 1),
            FindLegacyRegistrationConstructor(),
            FindField(typeof(CacheStats), "MemoryUsage"),
            FindField(typeof(CacheStats), "MemoryUsageRate")
        };

        foreach (var member in members) AssertCompatibilityMember(member);

        var assembly = typeof(DataTableManager).Assembly;
        AssertCompatibilityMember(assembly.GetType("DataTables.IDataTableManager", throwOnError: true)!);
        AssertCompatibilityMember(assembly.GetType("DataTables.DataTableStatus", throwOnError: true)!);
    }

    [Fact]
    public void RecommendedMembers_ShouldNotBeObsolete()
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

    private static FieldInfo FindField(Type type, string name)
        => type.GetField(name, BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Field {type.FullName}.{name} was not found.");

    private static ConstructorInfo FindLegacyRegistrationConstructor()
        => typeof(TableRegistration).GetConstructor(new[]
        {
            typeof(Type),
            typeof(string),
            typeof(Priority),
            typeof(Func<CancellationToken, ValueTask<DataTableBase?>>)
        }) ?? throw new InvalidOperationException("Legacy TableRegistration constructor was not found.");

    private static ConstructorInfo FindContextAwareRegistrationConstructor()
        => typeof(TableRegistration).GetConstructor(new[]
        {
            typeof(Type),
            typeof(string),
            typeof(Priority),
            typeof(Func<DataTableContext, CancellationToken, ValueTask<DataTableBase?>>)
        }) ?? throw new InvalidOperationException("Context-aware TableRegistration constructor was not found.");

    private static void AssertCompatibilityMember(MemberInfo member)
    {
        member.GetCustomAttribute<ObsoleteAttribute>().Should().NotBeNull(
            $"{member.DeclaringType?.FullName ?? member.Name}.{member.Name} is a compatibility API");
        var editorBrowsable = member.GetCustomAttribute<EditorBrowsableAttribute>();
        editorBrowsable.Should().NotBeNull(
            $"{member.DeclaringType?.FullName ?? member.Name}.{member.Name} is a compatibility API");
        editorBrowsable!.State.Should().Be(EditorBrowsableState.Never,
            $"{member.DeclaringType?.FullName ?? member.Name}.{member.Name} should be hidden from IntelliSense");
    }
}
