using System.Windows.Forms;
using System.Runtime.InteropServices;

namespace LmStudioOcrTray;

internal static class ClipboardWriter
{
    public static async Task<bool> TrySetTextAsync(string text, CancellationToken cancellationToken = default)
    {
        const int attempts = 5;

        for (var attempt = 1; attempt <= attempts; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                Clipboard.SetText(text, TextDataFormat.UnicodeText);
                return true;
            }
            catch (ExternalException) when (attempt < attempts)
            {
                await Task.Delay(50, cancellationToken);
            }
            catch (InvalidOperationException) when (attempt < attempts)
            {
                await Task.Delay(50, cancellationToken);
            }
        }

        return false;
    }
}
