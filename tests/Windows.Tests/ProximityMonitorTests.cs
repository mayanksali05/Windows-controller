using WinLock.Cryptography;
using WinLock.Service.Bluetooth;
using WinLock.Service.Configuration;
using WinLock.Service.Logging;
using Xunit;

namespace Windows.Tests;

public sealed class ProximityMonitorTests
{
    private static string TempDir() =>
        Path.Combine(Path.GetTempPath(), "winlock-tests", Guid.NewGuid().ToString("N"));

    [Fact]
    public async Task Disabled_StaysUnknown_AndDoesNotStartScanner()
    {
        var scanner = new TrackingScanner();
        var logger = new RecordingLogger();
        using var monitor = new ProximityMonitor(
            scanner,
            new AuthorizedDeviceStore(new DpapiSecureStorage(TempDir())),
            logger,
            new SecurityOptions { ProximityEnabled = false });

        await monitor.StartAsync(CancellationToken.None);
        await Task.Delay(50);

        Assert.False(scanner.Started);
        Assert.Equal(ProximityState.Unknown, monitor.CurrentState.State);

        await monitor.StopAsync(CancellationToken.None);
    }

    [Fact]
    public async Task SnapshotChange_LogsOncePerState()
    {
        var scanner = new TrackingScanner();
        var logger = new RecordingLogger();
        using var monitor = new ProximityMonitor(
            scanner,
            new AuthorizedDeviceStore(new DpapiSecureStorage(TempDir())),
            logger,
            new SecurityOptions { ProximityEnabled = true });

        await monitor.StartAsync(CancellationToken.None);
        await Task.Delay(50);
        Assert.True(scanner.Started);

        scanner.Push(new ProximitySnapshot(ProximityState.Nearby, "phone-1", -55, DateTimeOffset.UtcNow));
        scanner.Push(new ProximitySnapshot(ProximityState.Nearby, "phone-1", -60, DateTimeOffset.UtcNow));
        scanner.Push(new ProximitySnapshot(ProximityState.Away, "phone-1", -85, DateTimeOffset.UtcNow));

        var transitionEvents = logger.Events
            .Where(e => e.Type == SecurityEventType.ProximityChanged)
            .Where(e => e.Message.StartsWith("Proximity changed to", StringComparison.Ordinal))
            .ToList();
        Assert.Equal(2, transitionEvents.Count); // Unknown -> Nearby -> Away

        await monitor.StopAsync(CancellationToken.None);
    }

    private sealed class TrackingScanner : IBluetoothProximityScanner
    {
        public bool Started { get; private set; }
        public ProximitySnapshot Current { get; private set; } = ProximitySnapshot.Unknown();
        public event EventHandler<ProximitySnapshot>? SnapshotChanged;

        public void SetMonitoredDevices(IReadOnlyCollection<string> deviceIds) { }
        public void UpdateStale() { }
        public Task StartAsync(CancellationToken cancellationToken) { Started = true; return Task.CompletedTask; }
        public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

        public void Push(ProximitySnapshot snapshot)
        {
            Current = snapshot;
            SnapshotChanged?.Invoke(this, snapshot);
        }
    }
}