using System.Windows.Forms;
using System.Drawing;
using LmStudioOcrTray.Core;

namespace LmStudioOcrTray;

internal sealed class SettingsForm : Form
{
    private readonly AppSettings _initialSettings;
    private readonly LmStudioDiscovery _discovery;
    private readonly LmStudioClient _client;
    private readonly Func<AppSettings, string?> _applySettings;

    private HotkeyCaptureBox _hotkeyBox = null!;
    private CheckBox _startWithWindowsBox = null!;
    private NumericUpDown _timeoutBox = null!;
    private TextBox _serverBox = null!;
    private TextBox _apiKeyBox = null!;
    private ComboBox _modelBox = null!;
    private Button _refreshModelsButton = null!;
    private Label _connectionStatusLabel = null!;
    private TextBox _promptBox = null!;

    public SettingsForm(
        AppSettings settings,
        LmStudioDiscovery discovery,
        LmStudioClient client,
        Func<AppSettings, string?> applySettings)
    {
        _initialSettings = settings.Clone();
        _discovery = discovery;
        _client = client;
        _applySettings = applySettings;

        Text = "LM Studio OCR Tray Settings";
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        ShowInTaskbar = false;
        AutoScaleMode = AutoScaleMode.Dpi;
        ClientSize = new Size(610, 620);

        BuildUi();
        LoadValues();
    }

    private void BuildUi()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 4,
            Padding = new Padding(12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        Controls.Add(root);

        root.Controls.Add(BuildGeneralGroup(), 0, 0);
        root.Controls.Add(BuildLmStudioGroup(), 0, 1);
        root.Controls.Add(BuildOcrGroup(), 0, 2);
        root.Controls.Add(BuildButtons(), 0, 3);
    }

    private Control BuildGeneralGroup()
    {
        var group = new GroupBox
        {
            Text = "General",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 10),
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 3,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _hotkeyBox = new HotkeyCaptureBox(HotkeySettings.CreateDefault())
        {
            Dock = DockStyle.Fill,
            Margin = new Padding(3, 3, 3, 6),
        };
        _startWithWindowsBox = new CheckBox
        {
            Text = "Start with Windows",
            AutoSize = true,
            Margin = new Padding(3, 5, 3, 6),
        };
        _timeoutBox = new NumericUpDown
        {
            Minimum = 5,
            Maximum = 600,
            Increment = 5,
            Width = 90,
            Margin = new Padding(3, 3, 3, 6),
        };

        table.Controls.Add(MakeLabel("OCR hotkey:"), 0, 0);
        table.Controls.Add(_hotkeyBox, 1, 0);
        table.SetColumnSpan(_hotkeyBox, 2);
        table.Controls.Add(new Label { AutoSize = true }, 0, 1);
        table.Controls.Add(_startWithWindowsBox, 1, 1);
        table.SetColumnSpan(_startWithWindowsBox, 2);
        table.Controls.Add(MakeLabel("Request timeout:"), 0, 2);
        table.Controls.Add(_timeoutBox, 1, 2);
        table.Controls.Add(new Label
        {
            Text = "sec",
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 6, 3, 3),
        }, 2, 2);

        group.Controls.Add(table);
        return group;
    }

    private Control BuildLmStudioGroup()
    {
        var group = new GroupBox
        {
            Text = "LM Studio",
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 10),
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 3,
            RowCount = 5,
        };
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 145));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));

        _serverBox = new TextBox { Dock = DockStyle.Fill, Margin = new Padding(3, 3, 3, 6) };
        _apiKeyBox = new TextBox
        {
            Dock = DockStyle.Fill,
            UseSystemPasswordChar = true,
            Margin = new Padding(3, 3, 3, 6),
        };
        _modelBox = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            Margin = new Padding(3, 3, 3, 6),
        };
        _refreshModelsButton = new Button
        {
            Text = "Refresh models",
            AutoSize = true,
            Margin = new Padding(6, 2, 3, 6),
        };
        _refreshModelsButton.Click += RefreshModelsClicked;
        _connectionStatusLabel = new Label
        {
            Text = "Not checked",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 4, 3, 6),
        };

        table.Controls.Add(MakeLabel("Server:"), 0, 0);
        table.Controls.Add(_serverBox, 1, 0);
        table.SetColumnSpan(_serverBox, 2);
        table.Controls.Add(MakeLabel("API key:"), 0, 1);
        table.Controls.Add(_apiKeyBox, 1, 1);
        table.SetColumnSpan(_apiKeyBox, 2);
        table.Controls.Add(MakeLabel("Vision model:"), 0, 2);
        table.Controls.Add(_modelBox, 1, 2);
        table.Controls.Add(_refreshModelsButton, 2, 2);
        table.Controls.Add(MakeLabel("Connection:"), 0, 3);
        table.Controls.Add(_connectionStatusLabel, 1, 3);
        table.SetColumnSpan(_connectionStatusLabel, 2);

        var hint = new Label
        {
            Text = "Auto discovers the local LM Studio server and uses the first loaded vision model.",
            AutoSize = true,
            ForeColor = SystemColors.GrayText,
            Margin = new Padding(3, 2, 3, 2),
        };
        table.Controls.Add(hint, 0, 4);
        table.SetColumnSpan(hint, 3);

        group.Controls.Add(table);
        return group;
    }

    private Control BuildOcrGroup()
    {
        var group = new GroupBox
        {
            Text = "OCR",
            Dock = DockStyle.Fill,
            Padding = new Padding(10),
            Margin = new Padding(0, 0, 0, 10),
        };

        var table = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
        };
        table.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        table.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        _promptBox = new TextBox
        {
            Dock = DockStyle.Fill,
            Multiline = true,
            ScrollBars = ScrollBars.Vertical,
            AcceptsReturn = true,
        };
        var resetButton = new Button
        {
            Text = "Reset default",
            AutoSize = true,
            Anchor = AnchorStyles.Right,
            Margin = new Padding(3, 8, 0, 0),
        };
        resetButton.Click += (_, _) => _promptBox.Text = AppDefaults.OcrPrompt;

        table.Controls.Add(_promptBox, 0, 0);
        table.Controls.Add(resetButton, 0, 1);
        group.Controls.Add(table);
        return group;
    }

    private Control BuildButtons()
    {
        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.RightToLeft,
            AutoSize = true,
            WrapContents = false,
            Margin = new Padding(0),
        };

        var saveButton = new Button { Text = "Save", AutoSize = true };
        saveButton.Click += SaveClicked;
        var cancelButton = new Button
        {
            Text = "Cancel",
            AutoSize = true,
            DialogResult = DialogResult.Cancel,
        };

        AcceptButton = saveButton;
        CancelButton = cancelButton;
        panel.Controls.Add(saveButton);
        panel.Controls.Add(cancelButton);
        return panel;
    }

    private void LoadValues()
    {
        _hotkeyBox.SetValue(_initialSettings.Hotkey);
        _startWithWindowsBox.Checked = _initialSettings.StartWithWindows;
        _timeoutBox.Value = Math.Clamp(_initialSettings.TimeoutSeconds, (int)_timeoutBox.Minimum, (int)_timeoutBox.Maximum);
        _serverBox.Text = _initialSettings.Server;
        _apiKeyBox.Text = _initialSettings.ApiKey;
        _promptBox.Text = _initialSettings.Prompt;
        PopulateModelChoices(Array.Empty<VisionModel>(), _initialSettings.VisionModel);
    }

    private async void RefreshModelsClicked(object? sender, EventArgs e)
    {
        _refreshModelsButton.Enabled = false;
        _connectionStatusLabel.ForeColor = SystemColors.GrayText;
        _connectionStatusLabel.Text = "Checking…";

        try
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            var server = await _discovery.ResolveServerUriAsync(_serverBox.Text, timeout.Token);
            if (server is null)
            {
                SetConnectionError("Invalid server URL");
                return;
            }

            var models = await _client.ListLoadedVisionModelsAsync(server, _apiKeyBox.Text, timeout.Token);
            var selected = (_modelBox.SelectedItem as ModelChoice)?.Key ?? _initialSettings.VisionModel;
            PopulateModelChoices(models, selected);

            _connectionStatusLabel.ForeColor = Color.ForestGreen;
            _connectionStatusLabel.Text = models.Count == 0
                ? "Connected — no loaded vision model"
                : $"Connected — {models.Count} loaded vision model{(models.Count == 1 ? string.Empty : "s")}";
        }
        catch (Exception ex)
        {
            AppLogger.Log(ex, "Model refresh failed");
            SetConnectionError("Offline / unavailable");
        }
        finally
        {
            _refreshModelsButton.Enabled = true;
        }
    }

    private void SaveClicked(object? sender, EventArgs e)
    {
        if (!_hotkeyBox.Value.HasAnyModifier || _hotkeyBox.Value.VirtualKey == 0)
        {
            MessageBox.Show(this, "Choose a hotkey with at least one modifier.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var serverText = _serverBox.Text.Trim();
        if (!LmStudioDiscovery.IsAuto(serverText)
            && !LmStudioDiscovery.TryNormalizeServerUri(serverText, out _))
        {
            MessageBox.Show(this, "Server must be Auto or a valid HTTP/HTTPS address.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (string.IsNullOrWhiteSpace(_promptBox.Text))
        {
            MessageBox.Show(this, "OCR prompt cannot be empty.", Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        var candidate = _initialSettings.Clone();
        candidate.Hotkey = _hotkeyBox.Value.Clone();
        candidate.StartWithWindows = _startWithWindowsBox.Checked;
        candidate.TimeoutSeconds = Decimal.ToInt32(_timeoutBox.Value);
        candidate.Server = LmStudioDiscovery.IsAuto(serverText) ? "Auto" : serverText;
        candidate.ApiKey = _apiKeyBox.Text;
        candidate.VisionModel = (_modelBox.SelectedItem as ModelChoice)?.Key ?? "Auto";
        candidate.Prompt = _promptBox.Text;

        var error = _applySettings(candidate);
        if (!string.IsNullOrWhiteSpace(error))
        {
            MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        DialogResult = DialogResult.OK;
        Close();
    }

    private void PopulateModelChoices(IReadOnlyList<VisionModel> models, string? selectedKey)
    {
        selectedKey = string.IsNullOrWhiteSpace(selectedKey) ? "Auto" : selectedKey;

        _modelBox.BeginUpdate();
        try
        {
            _modelBox.Items.Clear();
            _modelBox.Items.Add(new ModelChoice("Auto", "Auto"));

            foreach (var model in models)
            {
                _modelBox.Items.Add(new ModelChoice(model.Key, $"{model.DisplayName}  [{model.Key}]"));
            }

            if (!selectedKey.Equals("Auto", StringComparison.OrdinalIgnoreCase)
                && !models.Any(model => model.Key.Equals(selectedKey, StringComparison.OrdinalIgnoreCase)))
            {
                _modelBox.Items.Add(new ModelChoice(selectedKey, $"{selectedKey}  (not loaded)"));
            }

            var selection = _modelBox.Items
                .OfType<ModelChoice>()
                .FirstOrDefault(item => item.Key.Equals(selectedKey, StringComparison.OrdinalIgnoreCase));
            _modelBox.SelectedItem = selection ?? _modelBox.Items[0];
        }
        finally
        {
            _modelBox.EndUpdate();
        }
    }

    private void SetConnectionError(string text)
    {
        _connectionStatusLabel.ForeColor = Color.Firebrick;
        _connectionStatusLabel.Text = text;
    }

    private static Label MakeLabel(string text)
    {
        return new Label
        {
            Text = text,
            AutoSize = true,
            Anchor = AnchorStyles.Left,
            Margin = new Padding(3, 6, 3, 3),
        };
    }

    private sealed record ModelChoice(string Key, string Label)
    {
        public override string ToString() => Label;
    }
}
