using System.Diagnostics;
using System.IO;
using System.Text;

namespace LuauGhirda.Core;

public sealed record ProcessResult(int ExitCode, string OutputPath)
{
    public bool Success => ExitCode == 0;
}

/// <summary>
/// Runs external engines as child processes and streams their stdout/stderr
/// line by line to callbacks (marshalled by the caller onto the UI thread).
/// Supports cooperative cancellation and a hard runtime timeout.
/// </summary>
public static class ProcessRunner
{
    /// <summary>Locate an executable by trying absolute candidates first, then PATH.</summary>
    public static string? FindTool(params string[] candidates)
    {
        foreach (var c in candidates)
            if (File.Exists(c)) return Path.GetFullPath(c);

        var pathEnv = Environment.GetEnvironmentVariable("PATH") ?? "";
        var ext = OperatingSystem.IsWindows() ? new[] { ".exe", ".cmd", ".bat" } : new[] { "" };
        foreach (var dir in pathEnv.Split(';', ':', StringSplitOptions.RemoveEmptyEntries))
        {
            foreach (var e in ext)
            {
                var p = Path.Combine(dir.Trim(), candidates[0] + e);
                if (File.Exists(p)) return p;
            }
        }
        return null;
    }

    /// <summary>
    /// Run a process. <paramref name="onOut"/>/<paramref name="onErr"/> fire per line as it arrives.
    /// Returns once the process exits or is cancelled/timed out.
    /// </summary>
    public static async Task<ProcessResult> RunAsync(
        string exe,
        string arguments,
        string? workingDirectory,
        Action<string>? onOut = null,
        Action<string>? onErr = null,
        CancellationToken ct = default,
        int timeoutSeconds = 0)
    {
        var psi = new ProcessStartInfo
        {
            FileName = exe,
            Arguments = arguments,
            UseShellExecute = false,
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };
        if (!string.IsNullOrEmpty(workingDirectory))
            psi.WorkingDirectory = workingDirectory;

        using var proc = new Process { StartInfo = psi };
        try
        {
            proc.Start();
        }
        catch (Exception ex)
        {
            onErr?.Invoke($"[runner] failed to start '{exe}': {ex.Message}");
            return new ProcessResult(-127, "");
        }

        using var timeoutCts = timeoutSeconds > 0
            ? CancellationTokenSource.CreateLinkedTokenSource(ct)
            : null;
        if (timeoutCts is not null)
            timeoutCts.CancelAfter(TimeSpan.FromSeconds(timeoutSeconds));

        var token = timeoutCts?.Token ?? ct;

        var outTask = PumpAsync(proc.StandardOutput, onOut, token);
        var errTask = PumpAsync(proc.StandardError, onErr, token);

        try
        {
            await proc.WaitForExitAsync(token).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            TryKill(proc);
            onErr?.Invoke("[runner] terminated (cancelled or timeout)");
            return new ProcessResult(-126, "");
        }

        try { await Task.WhenAll(outTask, errTask).ConfigureAwait(false); } catch { /* streams may abort on kill */ }

        return new ProcessResult(proc.ExitCode, "");
    }

    private static async Task PumpAsync(StreamReader reader, Action<string>? onLine, CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(ct).ConfigureAwait(false);
            if (line is null) break;
            onLine?.Invoke(line);
        }
    }

    private static void TryKill(Process proc)
    {
        try
        {
            if (!proc.HasExited)
            {
                proc.Kill(entireProcessTree: true);
            }
        }
        catch { /* already gone */ }
    }
}
