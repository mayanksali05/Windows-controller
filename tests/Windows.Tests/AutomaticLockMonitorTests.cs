using Microsoft.Extensions.Logging.Abstractions;
using WinLock.Cryptography;
using WinLock.Service.Bluetooth;
using WinLock.Service.Configuration;
using WinLock.Service.Locking;
using Xunit;

namespace Windows.Tests;

public sealed class AutomaticLockMonitorTests
{
    private sealed class CountingLockService : IWindowsLockService
    {
        public int LockCount;
        public bool CanLock { get; init; } = true;
        public Task LockAsync(CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref LockCount);
            return Task.CompletedTask;
        }
    }

    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), "winlock-tests", Guid.NewGuid().ToString("N"));

    private static AuthorizedDeviceStore CreateStoreWithDevice(AuthorizedDevice device)
    {
        var store = new AuthorizedDeviceStore(new DpapiSecureStorage(TempDir()));
        store.TryAdd(device);
        return store;
    }

    private static AutomaticLockMonitor CreateMonitor(
        FakeProximityScanner scanner,
        SecurityOptions security,
        IWindowsLockService lockService,
        Func<DateTimeOffset>? utcNow = null,
        Func<bool?>? isLockedState = null)
    {
        var devices = CreateStoreWithDevice(new AuthorizedDevice { DeviceId = "phone-1", PublicKeyBase64Url = "AAAA" });
        var proximity = new ProximityMonitor(
            scanner,
            devices,
            new RecordingLogger(),
            new SecurityOptions { ProximityEnabled = true });
        return new AutomaticLockMonitor(
            proximity,
            new LockCoordinator(lockService, new RecordingLogger()),
            lockService,
            devices,
            security,
            NullLogger<AutomaticLockMonitor>.Instance,
            utcNow,
            TimeSpan.FromMilliseconds(10),
            isLockedState);
    }

    [Fact]
    public void Disabled_DoesNotArm()
    {
        var scanner = new FakeProximityScanner();
        var monitor = CreateMonitor(scanner, new SecurityOptions { AutomaticLockEnabled = false }, new CountingLockService());

        scanner.SetState(new ProximitySnapshot(ProximityState.Away, "phone-1", -90, DateTimeOffset.UtcNow));
        Assert.False(monitor.EvaluateLocked());
        Assert.Null(monitor.ArmedSince);
    }

    [Fact]
    public void NoPairedDevices_NeverArms()
    {
        var scanner = new FakeProximityScanner();
        var emptyStore = new AuthorizedDeviceStore(new DpapiSecureStorage(TempDir()));
        var proximity = new ProximityMonitor(scanner, emptyStore, new RecordingLogger(), new SecurityOptions { ProximityEnabled = true });
        var monitor = new AutomaticLockMonitor(
            proximity,
            new LockCoordinator(new CountingLockService(), new RecordingLogger()),
            new CountingLockService(),
            emptyStore,
            new SecurityOptions { AutomaticLockEnabled = true, AutoLockAwayDurationSeconds = 1 },
            NullLogger<AutomaticLockMonitor>.Instance,
            utcNow: () => DateTimeOffset.UtcNow,
            pollInterval: TimeSpan.FromMilliseconds(10),
            isLockedState: () => false);

        scanner.SetState(new ProximitySnapshot(ProximityState.Away, null, null, DateTimeOffset.UtcNow));
        Assert.False(monitor.EvaluateLocked());
        Assert.Null(monitor.ArmedSince);
    }

    [Fact]
    public void Nearby_DoesNotArm()
    {
        var scanner = new FakeProximityScanner();
        var monitor = CreateMonitor(scanner, new SecurityOptions { AutomaticLockEnabled = true }, new CountingLockService());

        scanner.SetState(new ProximitySnapshot(ProximityState.Nearby, "phone-1", -50, DateTimeOffset.UtcNow));
        Assert.False(monitor.EvaluateLocked());
        Assert.Null(monitor.ArmedSince);
    }

    [Fact]
    public void Away_WithinGrace_ArmsWithoutLocking()
    {
        var scanner = new FakeProximityScanner();
        var monitor = CreateMonitor(
            scanner,
            new SecurityOptions { AutomaticLockEnabled = true, AutoLockAwayDurationSeconds = 60 },
            new CountingLockService());

        scanner.SetState(new ProximitySnapshot(ProximityState.Away, "phone-1", -90, DateTimeOffset.UtcNow));
        Assert.False(monitor.EvaluateLocked());
        Assert.NotNull(monitor.ArmedSince);
    }

    [Fact]
    public void Away_ReturnsNearby_Cancels()
    {
        var scanner = new FakeProximityScanner();
        var monitor = CreateMonitor(
            scanner,
            new SecurityOptions { AutomaticLockEnabled = true, AutoLockAwayDurationSeconds = 60 },
            new CountingLockService());

        scanner.SetState(new ProximitySnapshot(ProximityState.Away, "phone-1", -90, DateTimeOffset.UtcNow));
        monitor.EvaluateLocked();
        Assert.NotNull(monitor.ArmedSince);

        scanner.SetState(new ProximitySnapshot(ProximityState.Nearby, "phone-1", -50, DateTimeOffset.UtcNow));
        Assert.False(monitor.EvaluateLocked());
        Assert.Null(monitor.ArmedSince);
    }

    [Fact]
    public void Away_BeyondGrace_Locks()
    {
        var clock = new MutableClock();
        var scanner = new FakeProximityScanner();
        var monitor = CreateMonitor(
            scanner,
            new SecurityOptions { AutomaticLockEnabled = true, AutoLockAwayDurationSeconds = 60 },
            new CountingLockService(),
            utcNow: () => clock.Now,
            isLockedState: () => false);

        scanner.SetState(new ProximitySnapshot(ProximityState.Away, "phone-1", -90, clock.Now));
        Assert.False(monitor.EvaluateLocked());
        Assert.NotNull(monitor.ArmedSince);

        clock.Now = clock.Now.AddSeconds(61);
        Assert.True(monitor.EvaluateLocked());

        // Lock at most once per absence episode.
        Assert.False(monitor.EvaluateLocked());
    }

    [Fact]
    public void AlreadyLocked_DoesNotLock()
    {
        var clock = new MutableClock();
        var scanner = new FakeProximityScanner();
        var monitor = CreateMonitor(
            scanner,
            new SecurityOptions { AutomaticLockEnabled = true, AutoLockAwayDurationSeconds = 60 },
            new CountingLockService(),
            utcNow: () => clock.Now,
            isLockedState: () => true);

        scanner.SetState(new ProximitySnapshot(ProximityState.Away, "phone-1", -90, clock.Now));
        monitor.EvaluateLocked();
        clock.Now = clock.Now.AddSeconds(61);

        Assert.False(monitor.EvaluateLocked());
    }

    [Fact]
    public void NonInteractiveSession_DoesNotLock()
    {
        var clock = new MutableClock();
        var scanner = new FakeProximityScanner();
        var monitor = CreateMonitor(
            scanner,
            new SecurityOptions { AutomaticLockEnabled = true, AutoLockAwayDurationSeconds = 60 },
            new CountingLockService { CanLock = false },
            utcNow: () => clock.Now,
            isLockedState: () => false);

        scanner.SetState(new ProximitySnapshot(ProximityState.Away, "phone-1", -90, clock.Now));
        monitor.EvaluateLocked();
        clock.Now = clock.Now.AddSeconds(61);

        Assert.False(monitor.EvaluateLocked());
    }

    [Fact]
    public async Task ExecuteAsync_LocksOnce_WhenAbsent()
    {
        var clock = new MutableClock();
        var scanner = new FakeProximityScanner();
        var lockService = new CountingLockService();
        var monitor = CreateMonitor(
            scanner,
            new SecurityOptions { AutomaticLockEnabled = true, AutoLockAwayDurationSeconds = 1 },
            lockService,
            utcNow: () => clock.Now,
            isLockedState: () => false);

        scanner.SetState(new ProximitySnapshot(ProximityState.Away, "phone-1", -90, clock.Now));
        await monitor.StartAsync(CancellationToken.None);

        clock.Now = clock.Now.AddSeconds(5);
        await Task.Delay(200);

        Assert.Equal(1, lockService.LockCount);

        // Still absent: no repeated locks.
        await Task.Delay(100);
        Assert.Equal(1, lockService.LockCount);

        await monitor.StopAsync(CancellationToken.None);
    }

    private sealed class MutableClock
    {
        public DateTimeOffset Now { get; set; } = DateTimeOffset.UtcNow;
    }
}