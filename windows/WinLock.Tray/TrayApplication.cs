using System.Diagnostics;
using System.Text.Json;
using WinLock.Cryptography;
using WinLock.Protocol.Models;

namespace WinLock.Tray;

/// <summary>System-tray UI for the WinLock service (status, lock, logs, exit).</summary>
public sealed class TrayApplication : IDisposable
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ToolStripMenuItem _statusItem;
    private readonly ServiceClient _client;
    private readonly TrayOptions _options;
    private readonly CancellationTokenSource _cts = new();
    private readonly System.Windows.Forms.Timer _timer;
    private string _statusText = "Starting...";

    public TrayApplication(TrayOptions options)
    {
        _options = options;

        var storage = new DpapiSecureStorage(Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WinLock", "storage"));
        var identity = new DeviceIdentityService(storage, new Ed25519SigningService());
        _client = new ServiceClient(options, identity);

        _statusItem = new ToolStripMenuItem("Status: unknown") { Enabled = false };

        var menu = new ContextMenuStrip();
        menu.Items.Add(_statusItem);
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Pair new device", null, async (_, _) => await PairNewDeviceAsync());
        menu.Items.Add("Paired devices", null, async (_, _) => await ShowDevicesAsync());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Lock now", null, async (_, _) => await LockNowAsync());
        menu.Items.Add("Open security logs", null, (_, _) => OpenLogs());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => Exit());
        _menu = menu;

        _notifyIcon = new NotifyIcon
        {
            Icon = SystemIcons.Shield,
            Text = "WinLock",
            Visible = true,
            ContextMenuStrip = menu,
        };

        _timer = new System.Windows.Forms.Timer { Interval = Math.Max(1000, options.PollIntervalSeconds * 1000) };
        _timer.Tick += async (_, _) => await PollAsync();
        _timer.Start();
    }

    private readonly ContextMenuStrip _menu;

    private async Task PollAsync()
    {
        var status = await _client.GetStatusAsync(_cts.Token);
        if (status is null)
        {
            SetStatus("Service unreachable");
            return;
        }

        var locked = status.IsLocked switch
        {
            true => "Yes",
            false => "No",
            null => "Unknown",
        };
        var battery = status.BatteryPercent is int b ? $"{b}%" : "n/a";
        SetStatus($"Locked: {locked} | Battery: {battery} | {status.ServiceVersion}");
    }

    private void SetStatus(string text)
    {
        _statusText = text;
        _notifyIcon.Text = $"WinLock — {text}";
        _statusItem.Text = $"Status: {text}";
    }

    private async Task LockNowAsync()
    {
        var (ok, message) = await _client.LockAsync(_cts.Token);
        _notifyIcon.ShowBalloonTip(
            3000, "WinLock", ok ? "Laptop locked" : $"Lock failed: {message}", ToolTipIcon.Info);
        await PollAsync();
    }

    private async Task PairNewDeviceAsync()
    {
        var payload = await _client.CreatePairingSessionAsync(_cts.Token);
        if (payload is null)
        {
            _notifyIcon.ShowBalloonTip(3000, "WinLock", "Could not create a pairing session", ToolTipIcon.Warning);
            return;
        }

        var payloadText = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web));
        using var dialog = new PairingDialog(payloadText, QrCodeRenderer.Render(payloadText));
        dialog.ShowDialog();
    }

    private async Task ShowDevicesAsync()
    {
        var devices = await _client.ListDevicesAsync(_cts.Token);
        using var dialog = new DevicesDialog(devices, deviceId => _client.UnpairAsync(deviceId, _cts.Token).ContinueWith(t => t.Result.Success));
        dialog.ShowDialog();
        await PollAsync();
    }

    private void OpenLogs()
    {
        if (Directory.Exists(_options.LogsDirectory))
        {
            Process.Start("explorer.exe", _options.LogsDirectory);
        }
    }

    private void Exit()
    {
        _timer.Stop();
        _notifyIcon.Visible = false;
        _cts.Cancel();
        Application.Exit();
    }

    public void Dispose()
    {
        _timer.Dispose();
        _notifyIcon.Dispose();
        _menu.Dispose();
        _client.Dispose();
        _cts.Dispose();
    }
}