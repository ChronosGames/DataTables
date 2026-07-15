using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Xunit;

namespace DataTables.Tests;

public sealed class PreheatResultTests
{
    [Fact]
    public async Task Preheat_ShouldBoundConcurrencyAndReturnStableRegistrationOrder()
    {
        using var context = new global::DataTables.DataTableContext();
        var active = 0;
        var maximum = 0;
        var reachedLimit = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var release = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registrations = Enumerable.Range(0, 8)
            .Reverse()
            .Select(index => new global::DataTables.TableRegistration(
                typeof(ControlledTable),
                index.ToString("D2"),
                global::DataTables.Priority.Normal,
                async (_, _) =>
                {
                    var current = Interlocked.Increment(ref active);
                    UpdateMaximum(ref maximum, current);
                    if (current == 3) reachedLimit.TrySetResult(true);
                    await release.Task;
                    Interlocked.Decrement(ref active);
                    return new ControlledTable(index.ToString("D2"));
                }))
            .ToArray();
        context.RegisterTables(registrations);

        var pending = context.PreheatAsync(
            global::DataTables.Priority.Normal,
            new global::DataTables.PreheatOptions(maxConcurrency: 3)).AsTask();
        await reachedLimit.Task.WaitAsync(TimeSpan.FromSeconds(5));
        maximum.Should().Be(3);
        release.TrySetResult(true);
        var result = await pending;

        maximum.Should().Be(3);
        result.StopReason.Should().Be(global::DataTables.PreheatStopReason.None);
        result.Tables.Select(table => table.Name).Should().Equal("00", "01", "02", "03", "04", "05", "06", "07");
        result.Tables.Should().OnlyContain(table => table.Status == global::DataTables.PreheatTableStatus.Loaded);
        result.Stats.TableCount.Should().Be(8);
        result.Stats.LoadedCount.Should().Be(8);
        result.Stats.NotStartedCount.Should().Be(0);
    }

    [Fact]
    public async Task FailFast_ShouldStopTakingNewTablesButAllowInflightWorkToFinish()
    {
        using var context = new global::DataTables.DataTableContext();
        var started = new ConcurrentBag<string>();
        var inflightStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var failureReturned = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var releaseInflight = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registrations = Enumerable.Range(0, 4)
            .Select(index => index.ToString("D2"))
            .Select(name => new global::DataTables.TableRegistration(
                typeof(ControlledTable),
                name,
                global::DataTables.Priority.Critical,
                async (_, _) =>
                {
                    started.Add(name);
                    if (name == "00")
                    {
                        await inflightStarted.Task;
                        failureReturned.TrySetResult(true);
                        throw new InvalidOperationException("controlled failure");
                    }

                    if (name == "01")
                    {
                        inflightStarted.TrySetResult(true);
                        await releaseInflight.Task;
                    }

                    return new ControlledTable(name);
                }))
            .ToArray();
        context.RegisterTables(registrations);

        var pending = context.PreheatAsync(
            global::DataTables.Priority.Critical,
            new global::DataTables.PreheatOptions(maxConcurrency: 2, failFast: true)).AsTask();
        await failureReturned.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await Task.Delay(50);
        pending.IsCompleted.Should().BeFalse("the in-flight loader must be allowed to finish");
        releaseInflight.TrySetResult(true);
        var result = await pending;

        result.StopReason.Should().Be(global::DataTables.PreheatStopReason.FailFast);
        result.Tables.Select(table => table.Status).Should().Equal(
            global::DataTables.PreheatTableStatus.Failed,
            global::DataTables.PreheatTableStatus.Loaded,
            global::DataTables.PreheatTableStatus.NotStarted,
            global::DataTables.PreheatTableStatus.NotStarted);
        result.Tables[0].Exception.Should().BeOfType<InvalidOperationException>();
        started.Should().BeEquivalentTo("00", "01");
        result.Stats.FailureCount.Should().Be(1);
        result.Stats.LoadedCount.Should().Be(1);
        result.Stats.NotStartedCount.Should().Be(2);
    }

    [Fact]
    public async Task RuntimeCallerCancellation_ShouldReturnCanceledAndNotStartedPartialResults()
    {
        using var context = new global::DataTables.DataTableContext();
        var active = 0;
        var twoStarted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
        var registrations = Enumerable.Range(0, 4)
            .Select(index => new global::DataTables.TableRegistration(
                typeof(ControlledTable),
                index.ToString("D2"),
                global::DataTables.Priority.Normal,
                async (_, cancellationToken) =>
                {
                    if (Interlocked.Increment(ref active) == 2) twoStarted.TrySetResult(true);
                    await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
                    return new ControlledTable(index.ToString("D2"));
                }))
            .ToArray();
        context.RegisterTables(registrations);
        using var cancellation = new CancellationTokenSource();

        var pending = context.PreheatAsync(
            global::DataTables.Priority.Normal,
            new global::DataTables.PreheatOptions(maxConcurrency: 2),
            cancellation.Token).AsTask();
        await twoStarted.Task.WaitAsync(TimeSpan.FromSeconds(5));
        await cancellation.CancelAsync();
        var result = await pending;

        result.StopReason.Should().Be(global::DataTables.PreheatStopReason.Canceled);
        result.Tables.Select(table => table.Status).Should().Equal(
            global::DataTables.PreheatTableStatus.Canceled,
            global::DataTables.PreheatTableStatus.Canceled,
            global::DataTables.PreheatTableStatus.NotStarted,
            global::DataTables.PreheatTableStatus.NotStarted);
        result.Tables.Take(2).Should().OnlyContain(table => table.Exception is OperationCanceledException);
        result.Stats.CanceledCount.Should().Be(2);
        result.Stats.NotStartedCount.Should().Be(2);
        result.Stats.FailureCount.Should().Be(0);
    }

    [Fact]
    public async Task NullAndNonCallerCancellation_ShouldBeOrdinaryFailuresWithTableIdentity()
    {
        using var context = new global::DataTables.DataTableContext();
        context.RegisterTables(
        [
            new global::DataTables.TableRegistration(
                typeof(ControlledTable),
                "00-null",
                global::DataTables.Priority.Normal,
                (_, _) => ValueTask.FromResult<global::DataTables.DataTableBase?>(null)),
            new global::DataTables.TableRegistration(
                typeof(ControlledTable),
                "01-own-cancellation",
                global::DataTables.Priority.Normal,
                (_, _) => throw new OperationCanceledException("loader-owned cancellation")),
        ]);

        var result = await context.PreheatAsync(
            global::DataTables.Priority.Normal,
            new global::DataTables.PreheatOptions(maxConcurrency: 1));

        result.StopReason.Should().Be(global::DataTables.PreheatStopReason.None);
        result.Tables.Should().OnlyContain(table => table.Status == global::DataTables.PreheatTableStatus.Failed);
        result.Tables[0].Exception.Should().BeOfType<InvalidOperationException>()
            .Which.Message.Should().Contain(typeof(ControlledTable).FullName).And.Contain("00-null");
        result.Tables[1].Exception.Should().BeOfType<OperationCanceledException>();
        result.Stats.FailureCount.Should().Be(2);
        result.Stats.CanceledCount.Should().Be(0);
    }

    [Fact]
    public async Task PreCanceledCall_ShouldThrowDirectlyAndOptionsShouldRejectInvalidConcurrency()
    {
        Action invalidOptions = () => _ = new global::DataTables.PreheatOptions(0);
        invalidOptions.Should().Throw<ArgumentOutOfRangeException>();

        using var context = new global::DataTables.DataTableContext();
        context.RegisterTables(
        [
            new global::DataTables.TableRegistration(
                typeof(ControlledTable),
                string.Empty,
                global::DataTables.Priority.Critical,
                (_, _) => ValueTask.FromResult<global::DataTables.DataTableBase?>(new ControlledTable(string.Empty))),
        ]);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();

        var action = async () => await context.PreheatAsync(
            global::DataTables.Priority.Critical,
            new global::DataTables.PreheatOptions(),
            cancellation.Token);

        await action.Should().ThrowAsync<OperationCanceledException>();
    }

    private static void UpdateMaximum(ref int maximum, int current)
    {
        int observed;
        do
        {
            observed = Volatile.Read(ref maximum);
            if (observed >= current) return;
        }
        while (Interlocked.CompareExchange(ref maximum, current, observed) != observed);
    }

    private sealed class ControlledTable : global::DataTables.DataTableBase
    {
        public ControlledTable(string name) : base(name)
        {
        }

        public override Type Type => typeof(ControlledTable);
        public override int Count => 0;
        public override bool ParseDataRow(int index, BinaryReader reader) => false;
    }
}
