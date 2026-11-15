using System.Windows.Forms;
using LmStudioOcrTray.Core;

namespace LmStudioOcrTray;

internal sealed class GlobalHotkey : IDisposable
{
    private const int HotkeyId = 0x51A7;
    private readonly HotkeyWindow _window;
    private bool _registered;
    private HotkeySettings? _current;

    public GlobalHotkey()
    {
        _window = new HotkeyWindow(() => Pressed?.Invoke(this, EventArgs.Empty));
    }

    public event EventHandler? Pressed;

    public HotkeySettings? Current => _current?.Clone();

    public bool TryRegister(HotkeySettings settings)
    {
        if (_registered && _current?.Equals(settings) == true)
        {
            return true;
        }

        Unregister();

        var modifiers = Win32.MOD_NOREPEAT;
        if (settings.Win) modifiers |= Win32.MOD_WIN;
        if (settings.Shift) modifiers |= Win32.MOD_SHIFT;
        if (settings.Ctrl) modifiers |= Win32.MOD_CONTROL;
        if (settings.Alt) modifiers |= Win32.MOD_ALT;

        if (!Win32.RegisterHotKey(_window.Handle, HotkeyId, modifiers, unchecked((uint)settings.VirtualKey)))
        {
            return false;
        }

        _registered = true;
        _current = settings.Clone();
        return true;
    }

    public void Unregister()
    {
        if (!_registered)
        {
            return;
        }

        Win32.UnregisterHotKey(_window.Handle, HotkeyId);
        _registered = false;
        _current = null;
    }

    public void Dispose()
    {
        Unregister();
        _window.Dispose();
    }

    private sealed class HotkeyWindow : NativeWindow, IDisposable
    {
        private readonly Action _pressed;

        public HotkeyWindow(Action pressed)
        {
            _pressed = pressed;
            CreateHandle(new CreateParams());
        }

        public void Dispose()
        {
            DestroyHandle();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Win32.WM_HOTKEY && m.WParam.ToInt32() == HotkeyId)
            {
                _pressed();
            }

            base.WndProc(ref m);
        }
    }
}
