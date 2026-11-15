using System.Diagnostics;

namespace LmStudioOcrTray.Core;

public sealed class LmStudioDiscovery
{
    private static readonly Uri FallbackUri = new("http://127.0.0.1:1234");

    public async Task<Uri?> ResolveServerUriAsync(string? serverSetting, CancellationToken cancellationToken)
    {
        if (!IsAuto(serverSetting))
        {
            return TryNormalizeServerUri(serverSetting, out var explicitUri) ? explicitUri : null;
        }

        var detectedPort = await TryGetServerPortFromCliAsync(cancellationToken).ConfigureAwait(false);
        return detectedPort is > 0
            ? new Uri($"http://127.0.0.1:{detectedPort.Value}")
            : FallbackUri;
    }

    public static bool IsAuto(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            || value.Trim().Equals("Auto", StringComparison.OrdinalIgnoreCase);
    }

    public static bool TryNormalizeServerUri(string? value, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var candidate = value.Trim();
        if (!candidate.Contains("://", StringComparison.Ordinal))
        {
            candidate = "http://" + candidate;
        }

        if (!Uri.TryCreate(candidate, UriKind.Absolute, out var parsed)
            || (parsed.Scheme != Uri.UriSchemeHttp && parsed.Scheme != Uri.UriSchemeHttps)
            || string.IsNullOrWhiteSpace(parsed.Host))
        {
            return false;
        }

        uri = new Uri(parsed.GetLeftPart(UriPartial.Authority));
        return true;
    }

    private static async Task<int?> TryGetServerPortFromCliAsync(CancellationToken cancellationToken)
    {
        // The lms CLI can wake LM Studio on some setups. Only invoke it on Windows
        // when the desktop application is already running; otherwise the default-port
        // HTTP fallback is tried and fails silently if the server is offline.
        if (OperatingSystem.IsWindows() && !IsLmStudioDesktopRunning())
        {
            return null;
        }

        foreach (var executable in GetLmsExecutableCandidates())
        {
            var port = await TryRunStatusAsync(executable, cancellationToken).ConfigureAwait(false);
            if (port.HasValue)
            {
                return port;
            }
        }

        return null;
    }

    private static IEnumerable<string> GetLmsExecutableCandidates()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (!string.IsNullOrWhiteSpace(home))
        {
            var bundled = Path.Combine(home, ".lmstudio", "bin", OperatingSystem.IsWindows() ? "lms.exe" : "lms");
            if (File.Exists(bundled))
            {
                yield return bundled;
            }
        }

        yield return OperatingSystem.IsWindows() ? "lms.exe" : "lms";
    }

    private static async Task<int?> TryRunStatusAsync(string executable, CancellationToken cancellationToken)
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = executable,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                },
            };
            process.StartInfo.ArgumentList.Add("server");
            process.StartInfo.ArgumentList.Add("status");
            process.StartInfo.ArgumentList.Add("--json");
            process.StartInfo.ArgumentList.Add("--quiet");

            if (!process.Start())
            {
                return null;
            }

            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(1.5));

            try
            {
                await process.WaitForExitAsync(timeoutCts.Token).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                TryKill(process);
                return null;
            }

            if (process.ExitCode != 0)
            {
                return null;
            }

            var output = await process.StandardOutput.ReadToEndAsync().ConfigureAwait(false);
            return LmStudioProtocol.TryParseServerStatus(output, out var port) ? port : null;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch
        {
            return null;
        }
    }

    private static bool IsLmStudioDesktopRunning()
    {
        try
        {
            foreach (var process in Process.GetProcesses())
            {
                using (process)
                {
                    var name = process.ProcessName;
                    if (name.Contains("LM Studio", StringComparison.OrdinalIgnoreCase)
                        || name.Equals("LMStudio", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }
                }
            }
        }
        catch
        {
            // If process enumeration is unavailable, skip CLI auto-detection rather than
            // risk starting LM Studio as a side effect.
        }

        return false;
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // Best effort only.
        }
    }
}
