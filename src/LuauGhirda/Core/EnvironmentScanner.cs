using System.Diagnostics;
using System.IO;

namespace LuauGhirda.Core;

/// <summary>
/// Detects the external runtimes the engines need (Node.js, Python 3, Java) and
/// whether engine-local npm dependencies are installed. Results are cached until
/// the next <see cref="ScanAsync"/>.
/// </summary>
public static class EnvironmentScanner
{
    public sealed record ToolInfo(string Name, bool Found, string? Version, string? Path, string Hint)
    {
        /// <summary>Official download page, surfaced by the UI when the tool is missing.</summary>
        public string? Url { get; init; }
    }

    public static IReadOnlyList<ToolInfo> LastScan { get; private set; } = Array.Empty<ToolInfo>();

    private static string? _python;

    /// <summary>Cached python launcher ("python" on Windows, "python3" elsewhere) or null when missing.</summary>
    public static string? PythonCommand() => _python;

    public static bool NodeAvailable => LastScan.Any(t => t.Name == "Node.js" && t.Found);
    public static bool PythonAvailable => LastScan.Any(t => t.Name == "Python 3" && t.Found);

    public static async Task<IReadOnlyList<ToolInfo>> ScanAsync(CancellationToken ct = default)
    {
        var node = await ProbeAsync("Node.js", "node", "--version",
            "Node.js 18+ drives the Luraph / CaoMod / MoonSec / Prometheus engines",
            "https://nodejs.org/en/download", ct).ConfigureAwait(false);
        ToolInfo python;
        var py3 = await ProbeAsync("Python 3", "python3", "--version",
            "Python 3.10+ drives the luaobfuscator.com engine",
            "https://www.python.org/downloads/", ct).ConfigureAwait(false);
        if (py3.Found)
        {
            python = py3;
            _python = "python3";
        }
        else
        {
            var winPy = await ProbeAsync("Python 3", "python", "--version", py3.Hint,
                "https://www.python.org/downloads/", ct).ConfigureAwait(false);
            python = winPy;
            _python = winPy.Found ? "python" : null;
        }
        var luau = ProbeLuau();

        LastScan = new[] { node, python, luau };
        return LastScan;
    }

    /// <summary>Run `node_modules` presence check for engines that declare npm dependencies.</summary>
    public static bool EngineDepsInstalled(EngineDef engine)
    {
        if (!engine.NeedsNpmInstall) return true;
        return Directory.Exists(Path.Combine(Paths.EngineDir(engine.Folder), "node_modules"));
    }

    /// <summary>Run `npm install` inside an engine folder (streams progress).</summary>
    public static async Task<bool> InstallEngineDepsAsync(EngineDef engine, Action<LogKind, string> onLog, CancellationToken ct = default)
    {
        var npm = ProcessRunner.FindTool("npm");
        if (npm is null)
        {
            onLog(LogKind.Error, "npm not found — install Node.js first");
            return false;
        }

        onLog(LogKind.Info, $"npm install → {engine.Folder}");
        var result = await ProcessRunner.RunAsync(npm, "install --no-audit --no-fund --loglevel=error",
            Paths.EngineDir(engine.Folder),
            line => onLog(LogKind.Raw, "  " + line),
            line => onLog(LogKind.Warn, "  " + line),
            ct, timeoutSeconds: 300).ConfigureAwait(false);

        return result.Success;
    }

    private static async Task<ToolInfo> ProbeAsync(string name, string exe, string args, string hint, string? url, CancellationToken ct)
    {
        var path = ProcessRunner.FindTool(exe);
        if (path is null) return new ToolInfo(name, false, null, null, hint) { Url = url };

        var result = await ProcessRunner.RunAsync(path, args, null, null, null, ct, 10).ConfigureAwait(false);
        var version = result.ExitCode == 0 ? PeekVersion(exe, args, path) : null;
        return new ToolInfo(name, true, version ?? "installed", path, hint) { Url = url };
    }

    private static string? PeekVersion(string exe, string args, string path)
    {
        try
        {
            var psi = new ProcessStartInfo(path, args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
            };
            using var p = Process.Start(psi);
            if (p is null) return null;
            var so = p.StandardOutput.ReadToEnd();
            var se = p.StandardError.ReadToEnd();
            p.WaitForExit(5000);
            var line = (so + se).Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                                .FirstOrDefault(l => l.Length < 80);
            return string.IsNullOrWhiteSpace(line) ? null : line;
        }
        catch { return null; }
    }

    private static ToolInfo ProbeLuau()
    {
        var p = Paths.LuauRuntime();
        return new ToolInfo("Luau runtime", p is not null,
            p is null ? null : "bundled",
            p, "Ships with LuauGhirda under engines/*/bin — used for post-clean runs");
    }
}
