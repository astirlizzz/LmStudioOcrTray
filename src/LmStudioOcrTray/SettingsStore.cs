using System.Text.Json;
using LmStudioOcrTray.Core;

namespace LmStudioOcrTray;

internal sealed class SettingsStore
{
    private readonly string _directory;
    private readonly string _path;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    };

    public SettingsStore()
    {
        _directory = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "LmStudioOcrTray");
        _path = Path.Combine(_directory, "settings.json");
    }

    public AppSettings Load()
    {
        if (!File.Exists(_path))
        {
            return AppSettings.CreateDefault();
        }

        try
        {
            var json = File.ReadAllText(_path);
            var settings = JsonSerializer.Deserialize<AppSettings>(json, _jsonOptions) ?? AppSettings.CreateDefault();
            settings.Normalize();

            if (!string.IsNullOrWhiteSpace(settings.ProtectedApiKey))
            {
                try
                {
                    settings.ApiKey = DpapiProtector.Unprotect(settings.ProtectedApiKey);
                }
                catch (Exception ex)
                {
                    AppLogger.Log(ex, "Unable to decrypt saved API key");
                    settings.ApiKey = string.Empty;
                }
            }

            return settings;
        }
        catch (Exception ex)
        {
            AppLogger.Log(ex, "Unable to load settings; using defaults");
            return AppSettings.CreateDefault();
        }
    }

    public void Save(AppSettings settings)
    {
        Directory.CreateDirectory(_directory);

        var persisted = settings.Clone();
        persisted.ProtectedApiKey = DpapiProtector.Protect(settings.ApiKey);
        persisted.ApiKey = string.Empty;

        var json = JsonSerializer.Serialize(persisted, _jsonOptions);
        var tempPath = _path + ".tmp";
        File.WriteAllText(tempPath, json);
        File.Move(tempPath, _path, overwrite: true);
    }
}
