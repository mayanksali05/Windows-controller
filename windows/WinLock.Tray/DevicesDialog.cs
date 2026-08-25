using WinLock.Protocol.Models;

namespace WinLock.Tray;

/// <summary>Dialog that lists paired devices and allows unpairing.</summary>
public sealed class DevicesDialog : Form
{
    private readonly ListView _list;
    private readonly Func<string, Task<bool>> _unpairAsync;

    public DevicesDialog(IReadOnlyList<AuthorizedDeviceDto> devices, Func<string, Task<bool>> unpairAsync)
    {
        _unpairAsync = unpairAsync;
        Text = "Paired devices";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(480, 300);

        _list = new ListView
        {
            View = View.Details,
            FullRowSelect = true,
            GridLines = true,
            Location = new Point(15, 15),
            Size = new Size(450, 210),
        };
        _list.Columns.Add("Device ID", 170);
        _list.Columns.Add("Name", 130);
        _list.Columns.Add("Paired (UTC)", 130);

        foreach (var device in devices)
        {
            _list.Items.Add(new ListViewItem(new[] { device.DeviceId, device.Name, device.PairedAt }));
        }

        var unpair = new Button { Text = "Unpair selected", Location = new Point(15, 240), Size = new Size(130, 30) };
        unpair.Click += async (_, _) => await UnpairSelectedAsync();

        var close = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Location = new Point(335, 240),
            Size = new Size(130, 30),
        };

        Controls.Add(_list);
        Controls.Add(unpair);
        Controls.Add(close);
    }

    private async Task UnpairSelectedAsync()
    {
        if (_list.SelectedItems.Count == 0)
        {
            return;
        }

        var deviceId = _list.SelectedItems[0].Text;
        if (await _unpairAsync(deviceId))
        {
            _list.Items.Remove(_list.SelectedItems[0]);
        }
    }
}