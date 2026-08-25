using System.Text.Json;

namespace WinLock.Tray;

internal static class Program
{
    [STAThread]
    private static void Main()
    {
        ApplicationConfiguration.Initialize();

        var options = LoadOptions();

        try
        {
            using var tray = new TrayApplication(options);
            Application.Run();
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                $"WinLock tray failed to start: {ex.Message}",
                "WinLock", MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }

    private static TrayOptions LoadOptions()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
        if (!File.Exists(path))
        {
            return new TrayOptions();
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<TrayOptions>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
            }) ?? new TrayOptions();
        }
        catch (Exception)
        {
            return new TrayOptions();
        }
    }
}