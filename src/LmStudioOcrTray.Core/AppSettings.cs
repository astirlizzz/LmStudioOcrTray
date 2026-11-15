using System.Text.Json.Serialization;

namespace LmStudioOcrTray.Core;

public static class AppDefaults
{
    public const string OcrPrompt = "Transcribe all visible text from the image. Return only the transcription. Preserve line breaks, layout, lists, tables, and code formatting as closely as possible. Do not explain, summarize, or add commentary. Do not reason extensively; perform the transcription quickly and directly.";
}

public sealed class AppSettings
{
    public bool Enabled { get; set; } = true;
    public HotkeySettings Hotkey { get; set; } = HotkeySettings.CreateDefault();
    public bool StartWithWindows { get; set; } = true;
    public int TimeoutSeconds { get; set; } = 60;
    public string Server { get; set; } = "Auto";
    public string VisionModel { get; set; } = "Auto";
    public string Prompt { get; set; } = AppDefaults.OcrPrompt;
    public string ProtectedApiKey { get; set; } = string.Empty;

    [JsonIgnore]
    public string ApiKey { get; set; } = string.Empty;

    public static AppSettings CreateDefault() => new();

    public AppSettings Clone()
    {
        return new AppSettings
        {
            Enabled = Enabled,
            Hotkey = Hotkey.Clone(),
            StartWithWindows = StartWithWindows,
            TimeoutSeconds = TimeoutSeconds,
            Server = Server,
            VisionModel = VisionModel,
            Prompt = Prompt,
            ProtectedApiKey = ProtectedApiKey,
            ApiKey = ApiKey,
        };
    }

    public void Normalize()
    {
        Hotkey ??= HotkeySettings.CreateDefault();
        TimeoutSeconds = Math.Clamp(TimeoutSeconds, 5, 600);
        Server = string.IsNullOrWhiteSpace(Server) ? "Auto" : Server.Trim();
        VisionModel = string.IsNullOrWhiteSpace(VisionModel) ? "Auto" : VisionModel.Trim();
        Prompt = string.IsNullOrWhiteSpace(Prompt) ? AppDefaults.OcrPrompt : Prompt;
        ProtectedApiKey ??= string.Empty;
        ApiKey ??= string.Empty;
    }
}

public sealed class HotkeySettings : IEquatable<HotkeySettings>
{
    public bool Win { get; set; }
    public bool Shift { get; set; }
    public bool Ctrl { get; set; }
    public bool Alt { get; set; }
    public int VirtualKey { get; set; }

    public static HotkeySettings CreateDefault()
    {
        return new HotkeySettings
        {
            Win = true,
            Shift = true,
            VirtualKey = 0x5A, // Z
        };
    }

    public HotkeySettings Clone()
    {
        return new HotkeySettings
        {
            Win = Win,
            Shift = Shift,
            Ctrl = Ctrl,
            Alt = Alt,
            VirtualKey = VirtualKey,
        };
    }

    public bool HasAnyModifier => Win || Shift || Ctrl || Alt;

    public bool Equals(HotkeySettings? other)
    {
        return other is not null
            && Win == other.Win
            && Shift == other.Shift
            && Ctrl == other.Ctrl
            && Alt == other.Alt
            && VirtualKey == other.VirtualKey;
    }

    public override bool Equals(object? obj) => Equals(obj as HotkeySettings);

    public override int GetHashCode() => HashCode.Combine(Win, Shift, Ctrl, Alt, VirtualKey);
}
