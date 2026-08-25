using QRCoder;

namespace WinLock.Tray;

/// <summary>Renders the pairing payload as a scannable QR bitmap.</summary>
public static class QrCodeRenderer
{
    public static Bitmap Render(string payload)
    {
        using var generator = new QRCodeGenerator();
        using var data = generator.CreateQrCode(payload, QRCodeGenerator.ECCLevel.M);
        using var qr = new QRCode(data);
        return qr.GetGraphic(8);
    }
}