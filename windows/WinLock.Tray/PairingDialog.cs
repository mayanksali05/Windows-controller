namespace WinLock.Tray;

/// <summary>Dialog that shows the pairing QR code plus the raw payload text.</summary>
public sealed class PairingDialog : Form
{
    public PairingDialog(string payloadText, Bitmap qrImage)
    {
        Text = "Pair a new device";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterScreen;
        ClientSize = new Size(420, 540);

        var picture = new PictureBox
        {
            Image = qrImage,
            SizeMode = PictureBoxSizeMode.Zoom,
            Location = new Point(25, 20),
            Size = new Size(370, 370),
        };

        var caption = new Label
        {
            Text = "Scan this QR code from the iPhone app (or enter the payload manually).",
            Location = new Point(25, 395),
            Size = new Size(370, 30),
        };

        var payload = new TextBox
        {
            Multiline = true,
            ReadOnly = true,
            ScrollBars = ScrollBars.Vertical,
            Location = new Point(25, 430),
            Size = new Size(370, 70),
            Text = payloadText,
        };

        var close = new Button
        {
            Text = "Close",
            DialogResult = DialogResult.OK,
            Location = new Point(310, 505),
        };

        Controls.Add(picture);
        Controls.Add(caption);
        Controls.Add(payload);
        Controls.Add(close);
    }
}