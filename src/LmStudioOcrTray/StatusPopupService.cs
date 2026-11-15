using System.Drawing;

namespace LmStudioOcrTray;

internal sealed class StatusPopupService : IDisposable
{
    private StatusPopup? _current;

    public void ShowOcrStarted() => Show("◌", Color.Gainsboro, "OCR…", 1700);

    public void ShowCopied() => Show("✓", Color.LimeGreen, "Copied", 1700);

    public void ShowFailed() => Show("✕", Color.IndianRed, "OCR Failed!", 2000);

    public void ShowAlreadyRunning() => Show("●", Color.LightSkyBlue, "OCR already running", 1500);

    public void ShowNoImage() => Show("✕", Color.Orange, "No image captured", 2000);

    public void ShowHotkeyUnavailable() => Show("✕", Color.Orange, "Hotkey unavailable", 2000);

    public void Dispose()
    {
        _current?.Close();
        _current?.Dispose();
        _current = null;
    }

    private void Show(string symbol, Color symbolColor, string message, int durationMilliseconds)
    {
        _current?.Close();
        _current?.Dispose();

        var popup = new StatusPopup(symbol, symbolColor, message, durationMilliseconds);
        _current = popup;
        popup.FormClosed += (_, _) =>
        {
            if (ReferenceEquals(_current, popup))
            {
                _current = null;
            }
        };
        popup.Show();
    }
}
