using System.Windows.Forms;
using LmStudioOcrTray.Core;

namespace LmStudioOcrTray;

internal sealed class HotkeyCaptureBox : TextBox
{
    public HotkeyCaptureBox(HotkeySettings initialValue)
    {
        ReadOnly = true;
        ShortcutsEnabled = false;
        Value = initialValue.Clone();
        Text = HotkeyFormatter.Format(Value);
    }

    public HotkeySettings Value { get; private set; }

    public void SetValue(HotkeySettings value)
    {
        Value = value.Clone();
        Text = HotkeyFormatter.Format(Value);
    }

    protected override bool IsInputKey(Keys keyData) => true;

    protected override void OnKeyDown(KeyEventArgs e)
    {
        e.SuppressKeyPress = true;
        e.Handled = true;

        if (IsModifierKey(e.KeyCode))
        {
            return;
        }

        var win = Win32.IsKeyDown(Win32.VK_LWIN) || Win32.IsKeyDown(Win32.VK_RWIN);
        var candidate = new HotkeySettings
        {
            Win = win,
            Ctrl = e.Control,
            Alt = e.Alt,
            Shift = e.Shift,
            VirtualKey = (int)e.KeyCode,
        };

        if (!candidate.HasAnyModifier || candidate.VirtualKey == 0)
        {
            return;
        }

        Value = candidate;
        Text = HotkeyFormatter.Format(candidate);
        SelectAll();
    }

    private static bool IsModifierKey(Keys key)
    {
        return key is Keys.LWin
            or Keys.RWin
            or Keys.ShiftKey
            or Keys.LShiftKey
            or Keys.RShiftKey
            or Keys.ControlKey
            or Keys.LControlKey
            or Keys.RControlKey
            or Keys.Menu
            or Keys.LMenu
            or Keys.RMenu;
    }
}
