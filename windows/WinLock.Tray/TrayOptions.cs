namespace WinLock.Tray;

/// <summary>Configuration read from the tray app's own appsettings.json.</summary>
public sealed class TrayOptions
{
    public int Port { get; set; } = 8765;
    public bool UseHttps { get; set; } = true;
    public int PollIntervalSeconds { get; set; } = 5;
    public string LogsDirectory { get; set; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinLock", "logs");
}