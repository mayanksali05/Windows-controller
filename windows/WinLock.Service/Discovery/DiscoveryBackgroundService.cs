namespace WinLock.Service.Discovery;

/// <summary>
/// Starts the mDNS advertiser with the service host and logs failures instead
/// of crashing the service (mDNS may be unavailable on some Windows setups;
/// discovery is a convenience, not a security boundary).
/// </summary>
public sealed class DiscoveryBackgroundService : BackgroundService
{
    private readonly IDiscoveryAdvertiser _advertiser;
    private readonly ILogger<DiscoveryBackgroundService> _logger;

    public DiscoveryBackgroundService(IDiscoveryAdvertiser advertiser, ILogger<DiscoveryBackgroundService> logger)
    {
        _advertiser = advertiser;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await _advertiser.StartAsync(stoppingToken);
            _logger.LogInformation("mDNS advertisement started (_mywinlock._tcp).");

            // Keep the hosted service alive for the responder's receive loop;
            // StopAsync disposes the responder.
            await Task.Delay(Timeout.InfiniteTimeSpan, stoppingToken);
        }
        catch (OperationCanceledException)
        {
            // Normal shutdown.
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "mDNS advertisement failed; discovery unavailable.");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        try
        {
            await _advertiser.StopAsync(cancellationToken);
        }
        finally
        {
            await base.StopAsync(cancellationToken);
        }
    }
}