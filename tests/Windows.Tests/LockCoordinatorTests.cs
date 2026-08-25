using WinLock.Service.Locking;
using WinLock.Service.Logging;
using Xunit;

namespace Windows.Tests;

public sealed class RecordingLogger : ISecurityEventLogger
{
    public List<(SecurityEventType Type, string Message, object? Data)> Events { get; } = new();

    public void Log(SecurityEventType type, string message, object? data = null) =>
        Events.Add((type, message, data));
}

public sealed class LockCoordinatorTests
{
    [Fact]
    public async Task Success_LogsRequestedAndSuccess()
    {
        var logger = new RecordingLogger();
        var coordinator = new LockCoordinator(new FakeLockService(), logger);

        await coordinator.LockAsync("phone-1", CancellationToken.None);

        Assert.Equal(new[]
        {
            SecurityEventType.LockRequested,
            SecurityEventType.LockSuccess,
        }, logger.Events.Select(e => e.Type).ToArray());
    }

    [Fact]
    public async Task Failure_LogsRequestedAndFailed_AndThrows()
    {
        var logger = new RecordingLogger();
        var coordinator = new LockCoordinator(new ThrowingLockService(), logger);

        await Assert.ThrowsAsync<LockFailedException>(() =>
            coordinator.LockAsync(null, CancellationToken.None));

        Assert.Equal(new[]
        {
            SecurityEventType.LockRequested,
            SecurityEventType.LockFailed,
        }, logger.Events.Select(e => e.Type).ToArray());
    }

    [Fact]
    public async Task Cancellation_Rethrows_AndLogsFailure()
    {
        var logger = new RecordingLogger();
        var coordinator = new LockCoordinator(new ThrowingLockService(), logger);

        await Assert.ThrowsAsync<LockFailedException>(() =>
            coordinator.LockAsync(null, new CancellationToken(canceled: true)));

        Assert.Contains(SecurityEventType.LockFailed, logger.Events.Select(e => e.Type));
    }

    private sealed class FakeLockService : IWindowsLockService
    {
        public bool CanLock => true;
        public Task LockAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class ThrowingLockService : IWindowsLockService
    {
        public bool CanLock => true;
        public Task LockAsync(CancellationToken cancellationToken)
            => Task.FromException(new LockFailedException("simulated failure"));
    }
}