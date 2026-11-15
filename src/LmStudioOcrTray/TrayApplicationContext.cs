using System.Windows.Forms;
using System.Drawing;
using LmStudioOcrTray.Core;

namespace LmStudioOcrTray;

internal sealed class TrayApplicationContext : ApplicationContext
{
    private readonly SettingsStore _settingsStore;
    private readonly ClipboardMonitor _clipboardMonitor;
    private readonly GlobalHotkey _globalHotkey;
    private readonly LmStudioDiscovery _discovery;
    private readonly LmStudioClient _lmStudioClient;
    private readonly StatusPopupService _popupService;
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _trayMenu;
    private readonly ToolStripMenuItem _enabledMenuItem;

    private AppSettings _settings;
    private bool _runtimeEnabled;
    private int _ocrInProgress;
    private bool _disposed;

    public TrayApplicationContext()
    {
        _settingsStore = new SettingsStore();
        _settings = _settingsStore.Load();
        _discovery = new LmStudioDiscovery();
        _lmStudioClient = new LmStudioClient();
        _popupService = new StatusPopupService();
        _clipboardMonitor = new ClipboardMonitor();
        _globalHotkey = new GlobalHotkey();
        _globalHotkey.Pressed += GlobalHotkeyPressed;

        _enabledMenuItem = new ToolStripMenuItem("Enabled")
        {
            CheckOnClick = false,
        };
        _enabledMenuItem.Click += (_, _) => ToggleEnabled();

        var settingsMenuItem = new ToolStripMenuItem("Settings…");
        settingsMenuItem.Click += (_, _) => OpenSettings();
        var exitMenuItem = new ToolStripMenuItem("Exit");
        exitMenuItem.Click += (_, _) => ExitThread();

        _trayMenu = new ContextMenuStrip();
        _trayMenu.Items.Add(_enabledMenuItem);
        _trayMenu.Items.Add(settingsMenuItem);
        _trayMenu.Items.Add(new ToolStripSeparator());
        _trayMenu.Items.Add(exitMenuItem);

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadTrayIcon(),
            Text = "LM Studio OCR Tray",
            ContextMenuStrip = _trayMenu,
            Visible = true,
        };
        _notifyIcon.DoubleClick += (_, _) => OpenSettings();

        ApplyStartupRegistrationBestEffort(_settings.StartWithWindows);
        InitializeRuntimeEnabledState();
    }

    protected override void ExitThreadCore()
    {
        DisposeResources();
        base.ExitThreadCore();
    }

    private static Icon LoadTrayIcon()
    {
        try
        {
            return Icon.ExtractAssociatedIcon(Application.ExecutablePath) ?? SystemIcons.Application;
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    private void InitializeRuntimeEnabledState()
    {
        if (_settings.Enabled && _globalHotkey.TryRegister(_settings.Hotkey))
        {
            _runtimeEnabled = true;
        }
        else
        {
            _runtimeEnabled = false;
            if (_settings.Enabled)
            {
                AppLogger.Log($"Hotkey unavailable at startup: {HotkeyFormatter.Format(_settings.Hotkey)}");
            }
        }

        _enabledMenuItem.Checked = _runtimeEnabled;
    }

    private void ToggleEnabled()
    {
        if (_runtimeEnabled)
        {
            _globalHotkey.Unregister();
            _runtimeEnabled = false;
            _settings.Enabled = false;
            _enabledMenuItem.Checked = false;
            SaveSettingsBestEffort();
            return;
        }

        if (!_globalHotkey.TryRegister(_settings.Hotkey))
        {
            _popupService.ShowHotkeyUnavailable();
            return;
        }

        _runtimeEnabled = true;
        _settings.Enabled = true;
        _enabledMenuItem.Checked = true;
        SaveSettingsBestEffort();
    }

    private void OpenSettings()
    {
        using var form = new SettingsForm(_settings, _discovery, _lmStudioClient, ApplySettings);
        form.ShowDialog();
    }

    private string? ApplySettings(AppSettings candidate)
    {
        candidate.Normalize();
        if (!candidate.Hotkey.HasAnyModifier || candidate.Hotkey.VirtualKey == 0)
        {
            return "Choose a hotkey with at least one modifier.";
        }

        var oldSettings = _settings.Clone();
        var oldRuntimeEnabled = _runtimeEnabled;

        if (oldRuntimeEnabled)
        {
            _globalHotkey.Unregister();
            _runtimeEnabled = false;
        }

        // Test the candidate even when the app is currently disabled so Settings
        // can reject a shortcut that Windows or another application already owns.
        if (!_globalHotkey.TryRegister(candidate.Hotkey))
        {
            RestoreHotkey(oldRuntimeEnabled, oldSettings.Hotkey);
            return $"Hotkey '{HotkeyFormatter.Format(candidate.Hotkey)}' is already in use.";
        }

        var candidateRegistered = candidate.Enabled;
        if (!candidateRegistered)
        {
            _globalHotkey.Unregister();
        }

        try
        {
            StartupRegistration.SetEnabled(candidate.StartWithWindows);
            _settingsStore.Save(candidate);
        }
        catch (Exception ex)
        {
            AppLogger.Log(ex, "Unable to apply settings");
            if (candidateRegistered)
            {
                _globalHotkey.Unregister();
            }

            RestoreHotkey(oldRuntimeEnabled, oldSettings.Hotkey);
            ApplyStartupRegistrationBestEffort(oldSettings.StartWithWindows);
            return "Unable to save settings. See the local app log for details.";
        }

        _settings = candidate.Clone();
        _runtimeEnabled = candidateRegistered;
        _enabledMenuItem.Checked = _runtimeEnabled;
        return null;
    }

    private void RestoreHotkey(bool shouldBeEnabled, HotkeySettings hotkey)
    {
        _runtimeEnabled = false;
        if (shouldBeEnabled && _globalHotkey.TryRegister(hotkey))
        {
            _runtimeEnabled = true;
        }

        _enabledMenuItem.Checked = _runtimeEnabled;
    }

    private async void GlobalHotkeyPressed(object? sender, EventArgs e)
    {
        if (!_runtimeEnabled)
        {
            return;
        }

        if (Interlocked.CompareExchange(ref _ocrInProgress, 1, 0) != 0)
        {
            _popupService.ShowAlreadyRunning();
            return;
        }

        Bitmap? image = null;
        var requestStarted = false;
        var settings = _settings.Clone();

        try
        {
            image = _clipboardMonitor.GetLatestImageClone();
            if (image is null)
            {
                _popupService.ShowNoImage();
                return;
            }

            Uri? server;
            IReadOnlyList<VisionModel> models;
            try
            {
                using var preflightTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(4));
                server = await _discovery.ResolveServerUriAsync(settings.Server, preflightTimeout.Token);
                if (server is null)
                {
                    return;
                }

                models = await _lmStudioClient.ListLoadedVisionModelsAsync(server, settings.ApiKey, preflightTimeout.Token);
            }
            catch (Exception ex)
            {
                AppLogger.Log(ex, "LM Studio preflight unavailable");
                return;
            }

            var model = VisionModelSelector.Select(models, settings.VisionModel);
            if (model is null)
            {
                _popupService.ShowFailed();
                return;
            }

            requestStarted = true;
            _popupService.ShowOcrStarted();

            var imageDataUrl = ImageEncoder.ToPngDataUrl(image);
            using var requestTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds));
            var result = await _lmStudioClient.ChatOcrAsync(
                server,
                settings.ApiKey,
                model,
                settings.Prompt,
                imageDataUrl,
                requestTimeout.Token);

            if (string.IsNullOrWhiteSpace(result))
            {
                throw new InvalidOperationException("LM Studio returned no OCR text.");
            }

            if (!await ClipboardWriter.TrySetTextAsync(result))
            {
                throw new InvalidOperationException("Unable to write OCR text to the clipboard.");
            }

            _popupService.ShowCopied();
        }
        catch (Exception ex)
        {
            AppLogger.Log(ex, "OCR operation failed");
            if (requestStarted)
            {
                _popupService.ShowFailed();
            }
        }
        finally
        {
            image?.Dispose();
            Interlocked.Exchange(ref _ocrInProgress, 0);
        }
    }

    private void SaveSettingsBestEffort()
    {
        try
        {
            _settingsStore.Save(_settings);
        }
        catch (Exception ex)
        {
            AppLogger.Log(ex, "Unable to save settings");
        }
    }

    private static void ApplyStartupRegistrationBestEffort(bool enabled)
    {
        try
        {
            StartupRegistration.SetEnabled(enabled);
        }
        catch (Exception ex)
        {
            AppLogger.Log(ex, "Unable to update startup registration");
        }
    }

    private void DisposeResources()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _trayMenu.Dispose();
        _globalHotkey.Pressed -= GlobalHotkeyPressed;
        _globalHotkey.Dispose();
        _clipboardMonitor.Dispose();
        _popupService.Dispose();
        _lmStudioClient.Dispose();
    }
}
