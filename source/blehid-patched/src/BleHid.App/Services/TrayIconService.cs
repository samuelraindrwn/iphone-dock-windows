using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using Wpf.Ui.Controls;
using Forms = System.Windows.Forms;

namespace BleHid.App.Services;

/// <summary>
/// Notification area presence. WPF-UI 4.3 ships no tray control, so this wraps the WinForms one
/// and draws its image from the Fluent glyph rather than carrying an .ico asset.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly Forms.NotifyIcon _icon;
    private System.Drawing.Icon? _current;

    public TrayIconService(Action open, Action exit)
    {
        var menu = new Forms.ContextMenuStrip();
        menu.Items.Add("Open BLE HID", null, (_, _) => open());
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => exit());

        _icon = new Forms.NotifyIcon
        {
            Text = "BLE HID",
            ContextMenuStrip = menu,
            Visible = true
        };
        _icon.DoubleClick += (_, _) => open();

        Redraw();
        SystemEvents.UserPreferenceChanged += OnUserPreferenceChanged;
    }

    public void ShowMessage(string text) =>
        _icon.ShowBalloonTip(3000, "BLE HID", text, Forms.ToolTipIcon.Info);

    private void OnUserPreferenceChanged(object sender, UserPreferenceChangedEventArgs e)
    {
        if (e.Category == UserPreferenceCategory.General) Redraw();
    }

    // The taskbar follows the system theme rather than the app theme, so a fixed colour goes
    // invisible for half of all users.
    private void Redraw()
    {
        var light = Registry.GetValue(
            @"HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Themes\Personalize",
            "SystemUsesLightTheme", 0) is int and not 0;

        var previous = _current;
        _current = Render(light ? Colors.Black : Colors.White);
        _icon.Icon = _current;
        previous?.Dispose();
    }

    private static System.Drawing.Icon Render(Color color)
    {
        var glyph = new SymbolIcon
        {
            Symbol = SymbolRegular.Bluetooth24,
            FontSize = 28,
            Foreground = new SolidColorBrush(color),
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center
        };
        glyph.Measure(new Size(32, 32));
        glyph.Arrange(new Rect(0, 0, 32, 32));
        glyph.UpdateLayout();

        var bitmap = new RenderTargetBitmap(32, 32, 96, 96, PixelFormats.Pbgra32);
        bitmap.Render(glyph);

        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(bitmap));
        using var png = new MemoryStream();
        encoder.Save(png);

        return new System.Drawing.Icon(new MemoryStream(WrapAsIcon(png.ToArray())));
    }

    // Vista and later accept a PNG payload inside the icon container.
    private static byte[] WrapAsIcon(byte[] png)
    {
        using var buffer = new MemoryStream();
        using (var writer = new BinaryWriter(buffer, System.Text.Encoding.UTF8, leaveOpen: true))
        {
            writer.Write((short)0);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write((byte)32);
            writer.Write((byte)32);
            writer.Write((byte)0);
            writer.Write((byte)0);
            writer.Write((short)1);
            writer.Write((short)32);
            writer.Write(png.Length);
            writer.Write(22);
            writer.Write(png);
        }

        return buffer.ToArray();
    }

    public void Dispose()
    {
        SystemEvents.UserPreferenceChanged -= OnUserPreferenceChanged;
        _icon.Visible = false;
        _icon.Dispose();
        _current?.Dispose();
    }
}
