using System.Text;

namespace LmStudioOcrTray;

internal static class AppLogger
{
    private const long MaxLogBytes = 1024 * 1024;
    private static readonly object Gate = new();

    public static void Log(string message)
    {
        try
        {
            lock (Gate)
            {
                var directory = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "LmStudioOcrTray");
                Directory.CreateDirectory(directory);
                var path = Path.Combine(directory, "app.log");

                if (File.Exists(path) && new FileInfo(path).Length > MaxLogBytes)
                {
                    File.Delete(path);
                }

                var line = $"{DateTimeOffset.Now:O}  {message}{Environment.NewLine}";
                File.AppendAllText(path, line, Encoding.UTF8);
            }
        }
        catch
        {
            // Logging must never terminate the tray process.
        }
    }

    public static void Log(Exception exception, string context)
    {
        Log($"{context}: {exception}");
    }
}
