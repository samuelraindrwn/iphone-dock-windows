using BleHid.Core;
using Xunit;

namespace BleHid.Core.Tests;

/// <summary>
/// Regression cover for the handover race: the CLI and the app hand the radio to each other
/// through a named mutex and a named event, and both got this wrong at first.
/// </summary>
public class SingleInstanceTests
{
    // The real names are global to the machine, so the tests use their own to avoid stopping a
    // peripheral the developer is actually running.
    private static string TestEvent() => $@"Local\BleHid.Test.Stop.{Guid.NewGuid():N}";
    private static string TestMutex() => $@"Local\BleHid.Test.Peripheral.{Guid.NewGuid():N}";

    [Fact]
    public void Instance_names_are_local_scoped_and_distinct()
    {
        Assert.StartsWith(@"Local\", SingleInstance.MutexName);
        Assert.StartsWith(@"Local\", SingleInstance.StopEventName);
        Assert.NotEqual(SingleInstance.MutexName, SingleInstance.StopEventName);
    }

    [Fact]
    public void Second_owner_cannot_take_the_mutex_while_the_first_holds_it()
    {
        var name = TestMutex();
        using var first = new Mutex(true, name, out var firstOwns);
        using var second = new Mutex(true, name, out var secondOwns);

        Assert.True(firstOwns);
        Assert.False(secondOwns);
    }

    [Fact]
    public void Mutex_becomes_available_once_the_owner_releases_it()
    {
        var name = TestMutex();
        var first = new Mutex(true, name, out var firstOwns);
        Assert.True(firstOwns);
        first.Dispose();

        using var second = new Mutex(true, name, out var secondOwns);
        Assert.True(secondOwns);
    }

    /// <summary>
    /// The bug: a named event that already exists ignores the initialState argument, so a new
    /// owner inherits the signal that retired the previous one and exits immediately.
    /// </summary>
    [Fact]
    public void Creating_an_existing_named_event_inherits_its_signalled_state()
    {
        var name = TestEvent();
        using var owner = new EventWaitHandle(false, EventResetMode.ManualReset, name);
        owner.Set();

        using var newcomer = new EventWaitHandle(false, EventResetMode.ManualReset, name);

        Assert.True(newcomer.WaitOne(0));
    }

    /// <summary>The fix: reset immediately after creating, before registering any wait.</summary>
    [Fact]
    public void Resetting_after_creation_clears_an_inherited_stop_request()
    {
        var name = TestEvent();
        using var owner = new EventWaitHandle(false, EventResetMode.ManualReset, name);
        owner.Set();

        using var newcomer = new EventWaitHandle(false, EventResetMode.ManualReset, name);
        newcomer.Reset();

        Assert.False(newcomer.WaitOne(0));
    }

    [Fact]
    public void A_reset_stop_event_still_fires_on_a_later_request()
    {
        var name = TestEvent();
        using var owner = new EventWaitHandle(false, EventResetMode.ManualReset, name);
        owner.Reset();

        using var signalled = new ManualResetEventSlim();
        var registration = ThreadPool.RegisterWaitForSingleObject(
            owner, (_, _) => signalled.Set(), null, Timeout.Infinite, executeOnlyOnce: true);

        try
        {
            Assert.False(signalled.Wait(100));

            using (var requester = EventWaitHandle.OpenExisting(name)) requester.Set();

            Assert.True(signalled.Wait(TimeSpan.FromSeconds(5)));
        }
        finally
        {
            registration.Unregister(null);
        }
    }

    [Fact]
    public void Opening_a_stop_event_that_does_not_exist_is_reported_not_thrown_blindly()
    {
        Assert.Throws<WaitHandleCannotBeOpenedException>(
            () => EventWaitHandle.OpenExisting(TestEvent()));
    }

    /// <summary>Both front ends must agree, or two processes fight over the radio.</summary>
    [Fact]
    public void Cli_and_app_derive_the_same_names_from_core()
    {
        Assert.Equal(@"Local\BleHid.Peripheral", SingleInstance.MutexName);
        Assert.Equal(@"Local\BleHid.Stop", SingleInstance.StopEventName);
    }
}
