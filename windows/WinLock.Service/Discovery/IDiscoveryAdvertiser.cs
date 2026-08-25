namespace WinLock.Service.Discovery;

/// <summary>Advertises the WinLock service on the LAN for Bonjour/mDNS discovery.</summary>
public interface IDiscoveryAdvertiser
{
    Task StartAsync(CancellationToken cancellationToken);
    Task StopAsync(CancellationToken cancellationToken);
}