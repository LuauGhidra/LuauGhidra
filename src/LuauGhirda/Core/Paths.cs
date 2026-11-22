using System.IO;

namespace LuauGhirda.Core;

/// <summary>
/// Filesystem anchors. The app ships the deobfuscation engines and the Luau
/// runtime in an `engines/` folder placed next to the executable (or above it,
/// which covers `dotnet run` from bin/Debug/net8.0-windows).
/// </summary>
public static class Paths
{
    private static string? _enginesRoot;

    public static string EnginesRoot
    {
        get
        {
            if (_enginesRoot is not null) return _enginesRoot;

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            for (int i = 0; i < 6 && dir is not null; i++)
            {
                var candidate = Path.Combine(dir.FullName, "engines");
                if (Directory.Exists(candidate))
                {
                    _enginesRoot = candidate;
                    return candidate;
                }
                dir = dir.Parent;
            }
            // Default next to the exe even if missing — runner reports a clear error.
            _enginesRoot = Path.Combine(AppContext.BaseDirectory, "engines");
            return _enginesRoot;
        }
    }

    /// <summary>Full path inside the engines root, e.g. EngineDir("Luraph-v15.1-Deobfuscator").</summary>
    public static string EngineDir(string folder) => Path.Combine(EnginesRoot, folder);

    public static bool EngineExists(string folder) => Directory.Exists(EngineDir(folder));

    public static string EngineFile(string folder, params string[] parts)
    {
        var p = Path.Combine(new[] { EngineDir(folder) }.Concat(parts).ToArray());
        return p;
    }

    /// <summary>The bundled per-OS Luau CLI (ships inside the engine folders).</summary>
    public static string? LuauRuntime()
    {
        string exe = OperatingSystem.IsWindows() ? "luau.exe" : "luau";
        foreach (var folder in new[]
                 {
                     "Luraph-v15.2-Deobfuscator/bin",
                     "Luraph-v15.1-Deobfuscator/bin",
                     "CaoMod-Luraph-Deobfuscator/bin",
                 })
        {
            var p = EngineFile(folder, exe);
            if (File.Exists(p)) return p;
        }
        return null;
    }

    /// <summary>
    /// Outputs folder next to the executable: &lt;exe dir&gt;\outputs. Every
    /// deobfuscated file lands here unless a custom path is typed.
    /// </summary>
    public static string OutputsDir => Path.Combine(AppContext.BaseDirectory, "outputs");

    public static string EnsureOutputs()
    {
        try { Directory.CreateDirectory(OutputsDir); } catch { /* read-only dir — user picks a path */ }
        return OutputsDir;
    }
}
