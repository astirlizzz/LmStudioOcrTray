using System.Windows.Forms;
using System.Drawing;
using System.Runtime.InteropServices;

namespace LmStudioOcrTray;

internal sealed class ClipboardMonitor : IDisposable
{
    private readonly object _imageGate = new();
    private readonly ClipboardWindow _window;
    private readonly System.Windows.Forms.Timer _retryTimer;
    private Bitmap? _latestImage;
    private int _attemptsRemaining;
    private bool _disposed;
    private bool _listening;

    public ClipboardMonitor()
    {
        _window = new ClipboardWindow(OnClipboardChanged);
        _listening = Win32.AddClipboardFormatListener(_window.Handle);
        if (!_listening)
        {
            AppLogger.Log("AddClipboardFormatListener failed; clipboard image monitoring is unavailable.");
        }

        _retryTimer = new System.Windows.Forms.Timer { Interval = 50 };
        _retryTimer.Tick += (_, _) => TryCaptureClipboardImage();
    }

    public Bitmap? GetLatestImageClone()
    {
        lock (_imageGate)
        {
            return _latestImage is null ? null : new Bitmap(_latestImage);
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _retryTimer.Stop();
        _retryTimer.Dispose();
        if (_listening)
        {
            Win32.RemoveClipboardFormatListener(_window.Handle);
        }
        _window.Dispose();

        lock (_imageGate)
        {
            _latestImage?.Dispose();
            _latestImage = null;
        }
    }

    private void OnClipboardChanged()
    {
        _attemptsRemaining = 5;
        TryCaptureClipboardImage();
    }

    private void TryCaptureClipboardImage()
    {
        if (_disposed)
        {
            return;
        }

        try
        {
            if (!Clipboard.ContainsImage())
            {
                _retryTimer.Stop();
                return;
            }

            using var image = Clipboard.GetImage();
            if (image is null)
            {
                _retryTimer.Stop();
                return;
            }

            var ownedCopy = new Bitmap(image);
            Bitmap? oldImage;
            lock (_imageGate)
            {
                oldImage = _latestImage;
                _latestImage = ownedCopy;
            }

            oldImage?.Dispose();
            _retryTimer.Stop();
        }
        catch (ExternalException ex)
        {
            RetryOrLog(ex);
        }
        catch (InvalidOperationException ex)
        {
            RetryOrLog(ex);
        }
    }

    private void RetryOrLog(Exception exception)
    {
        _attemptsRemaining--;
        if (_attemptsRemaining > 0)
        {
            _retryTimer.Stop();
            _retryTimer.Start();
            return;
        }

        _retryTimer.Stop();
        AppLogger.Log(exception, "Unable to read image from clipboard after retries");
    }

    private sealed class ClipboardWindow : NativeWindow, IDisposable
    {
        private readonly Action _changed;

        public ClipboardWindow(Action changed)
        {
            _changed = changed;
            CreateHandle(new CreateParams());
        }

        public void Dispose()
        {
            DestroyHandle();
        }

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == Win32.WM_CLIPBOARDUPDATE)
            {
                _changed();
            }

            base.WndProc(ref m);
        }
    }
}
