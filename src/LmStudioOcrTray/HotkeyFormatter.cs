using System.Windows.Forms;
using LmStudioOcrTray.Core;

namespace LmStudioOcrTray;

internal static class HotkeyFormatter
{
    public static string Format(HotkeySettings settings)
    {
        var parts = new List<string>(5);
        if (settings.Win) parts.Add("Win");
        if (settings.Ctrl) parts.Add("Ctrl");
        if (settings.Alt) parts.Add("Alt");
        if (settings.Shift) parts.Add("Shift");
        parts.Add(FormatKey(settings.VirtualKey));
        return string.Join(" + ", parts);
    }

    private static string FormatKey(int virtualKey)
    {
        if (virtualKey is >= 0x41 and <= 0x5A)
        {
            return ((char)virtualKey).ToString();
        }

        if (virtualKey is >= 0x30 and <= 0x39)
        {
            return ((char)virtualKey).ToString();
        }

        var key = (Keys)virtualKey;
        return key switch
        {
            Keys.Oemcomma => ",",
            Keys.OemPeriod => ".",
            Keys.OemMinus => "-",
            Keys.Oemplus => "+",
            _ => key.ToString(),
        };
    }
}
