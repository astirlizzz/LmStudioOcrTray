using System.Windows.Forms;
using System.Drawing;
using System.Drawing.Drawing2D;

namespace LmStudioOcrTray;

internal sealed class StatusPopup : Form
{
    private static readonly Font PopupFont = new("Segoe UI", 9.5f, FontStyle.Regular, GraphicsUnit.Point);
    private static readonly Font SymbolFont = new("Segoe UI Symbol", 11f, FontStyle.Bold, GraphicsUnit.Point);
    private readonly System.Windows.Forms.Timer _closeTimer;

    public StatusPopup(string symbol, Color symbolColor, string message, int durationMilliseconds)
    {
        AutoScaleMode = AutoScaleMode.Dpi;
        BackColor = Color.FromArgb(38, 38, 40);
        ForeColor = Color.White;
        FormBorderStyle = FormBorderStyle.None;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.Manual;
        TopMost = true;

        var symbolSize = TextRenderer.MeasureText(symbol, SymbolFont, Size.Empty, TextFormatFlags.NoPadding);
        var textSize = TextRenderer.MeasureText(message, PopupFont, Size.Empty, TextFormatFlags.NoPadding);
        var width = Math.Clamp(14 + symbolSize.Width + 8 + textSize.Width + 16, 120, 240);
        var height = 40;
        ClientSize = new Size(width, height);

        var symbolLabel = new Label
        {
            AutoSize = false,
            BackColor = Color.Transparent,
            Font = SymbolFont,
            ForeColor = symbolColor,
            Location = new Point(12, 0),
            Size = new Size(symbolSize.Width + 4, height),
            Text = symbol,
            TextAlign = ContentAlignment.MiddleCenter,
        };

        var textLabel = new Label
        {
            AutoEllipsis = true,
            AutoSize = false,
            BackColor = Color.Transparent,
            Font = PopupFont,
            ForeColor = Color.White,
            Location = new Point(symbolLabel.Right + 6, 0),
            Size = new Size(width - symbolLabel.Right - 16, height),
            Text = message,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        Controls.Add(symbolLabel);
        Controls.Add(textLabel);

        _closeTimer = new System.Windows.Forms.Timer
        {
            Interval = Math.Max(250, durationMilliseconds),
        };
        _closeTimer.Tick += (_, _) =>
        {
            _closeTimer.Stop();
            Close();
        };

        Shown += (_, _) =>
        {
            PositionNearWorkAreaCorner();
            _closeTimer.Start();
        };
    }

    protected override bool ShowWithoutActivation => true;

    protected override CreateParams CreateParams
    {
        get
        {
            var parameters = base.CreateParams;
            parameters.ExStyle |= Win32.WS_EX_TOOLWINDOW | Win32.WS_EX_TOPMOST | Win32.WS_EX_NOACTIVATE;
            return parameters;
        }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        UpdateRoundedRegion();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _closeTimer.Dispose();
        }

        base.Dispose(disposing);
    }

    private void PositionNearWorkAreaCorner()
    {
        var screen = Screen.PrimaryScreen ?? Screen.FromPoint(Cursor.Position);
        var workArea = screen.WorkingArea;
        Location = new Point(workArea.Right - Width - 12, workArea.Bottom - Height - 12);
    }

    private void UpdateRoundedRegion()
    {
        if (Width <= 0 || Height <= 0)
        {
            return;
        }

        const int radius = 10;
        using var path = new GraphicsPath();
        var diameter = radius * 2;
        path.AddArc(0, 0, diameter, diameter, 180, 90);
        path.AddArc(Width - diameter, 0, diameter, diameter, 270, 90);
        path.AddArc(Width - diameter, Height - diameter, diameter, diameter, 0, 90);
        path.AddArc(0, Height - diameter, diameter, diameter, 90, 90);
        path.CloseFigure();

        var oldRegion = Region;
        Region = new Region(path);
        oldRegion?.Dispose();
    }
}
