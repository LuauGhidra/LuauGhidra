using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Diagnostics;

namespace LuauGhirda.Core;

public sealed record DeobfOptions(
    bool Devirtualize = true,
    bool PostClean = true,
    bool ReadableNames = false,
    bool AutoDetect = true,
    string? ForcedEngineId = null);

public sealed record DeobfReport(
    bool Success,
    string EngineUsed,
    string? DetectedFamily,
    string OutputPath,
    TimeSpan Elapsed,
    int Attempts);

/// <summary>
/// Orchestrates the bundled engines: picks one (manual or auto-detected), invokes
/// local engines as child processes and cloud engines over HTTPS, streams every
/// log line, optionally applies the Luraph post-clean pass, and falls back through
/// candidate engines in Auto mode.
/// </summary>
public static class EngineRunner
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(120) };

    /// <summary>
    /// Run deobfuscation. <paramref name="onLog"/> receives already-formatted lines.
    /// </summary>
    public static async Task<DeobfReport> RunAsync(
        string inputPath,
        string outputPath,
        DeobfOptions options,
        Action<LogKind, string> onLog,
        CancellationToken ct = default)
    {
        var sw = Stopwatch.StartNew();
        var script = await File.ReadAllTextAsync(inputPath, ct).ConfigureAwait(false);

        // ---- pick candidates -----------------------------------------------
        List<EngineDef> candidates;
        string? detected = null;
        if (!options.AutoDetect && options.ForcedEngineId is { } fid)
        {
            var forced = EngineRegistry.ById(fid);
            candidates = forced is null ? new List<EngineDef>() : new List<EngineDef> { forced };
        }
        else
        {
            candidates = EngineRegistry.Rank(script).ToList();
            detected = candidates.Count > 0 ? candidates[0].Family : null;
        }

        if (candidates.Count == 0)
        {
            onLog(LogKind.Error, "no engine selected");
            return new DeobfReport(false, "—", detected, outputPath, sw.Elapsed, 0);
        }

        // ---- environment ----------------------------------------------------
        var node = ProcessRunner.FindTool("node");
        var python = EnvironmentScanner.PythonCommand();

        // ---- try in order ---------------------------------------------------
        int attempts = 0;
        foreach (var engine in candidates)
        {
            attempts++;
            ct.ThrowIfCancellationRequested();

            onLog(LogKind.Info, $"─ engine [{attempts}] {engine.DisplayName}");

            bool outputReady = false;

            if (engine.Runtime == EngineRuntime.Cloud)
            {
                outputReady = await RunCloudAsync(inputPath, outputPath, onLog, ct).ConfigureAwait(false);
            }
            else
            {
                if (!Paths.EngineExists(engine.Folder))
                {
                    onLog(LogKind.Warn, $"engine folder missing: {engine.Folder}");
                    continue;
                }

                string? exe;
                string entry;
                if (engine.Runtime == EngineRuntime.Node)
                {
                    if (node is null)
                    {
                        onLog(LogKind.Error, "Node.js not found on PATH — needed for " + engine.DisplayName);
                        continue;
                    }
                    exe = node;
                    entry = engine.Id == "moonsec" ? "bin/ms.js"
                          : engine.Id == "prometheus" ? "bin/pdeobf.js"
                          : "deob.js";
                }
                else
                {
                    if (python is null)
                    {
                        onLog(LogKind.Error, "Python 3 not found on PATH — needed for " + engine.DisplayName);
                        continue;
                    }
                    exe = python;
                    entry = "cli.py";
                }

                var workDir = Paths.EngineDir(engine.Folder);
                if (!File.Exists(Path.Combine(workDir, entry)))
                {
                    onLog(LogKind.Warn, $"entry point missing: {entry} — skipping");
                    continue;
                }

                // engines that need node_modules get them automatically on first use
                if (engine.NeedsNpmInstall && !EnvironmentScanner.EngineDepsInstalled(engine))
                {
                    onLog(LogKind.Info, "  first run — installing engine dependencies…");
                    await EnvironmentScanner.InstallEngineDepsAsync(engine,
                        (k, l) => onLog(k == LogKind.Raw ? LogKind.Raw : k, l), ct).ConfigureAwait(false);
                    if (!EnvironmentScanner.EngineDepsInstalled(engine))
                    {
                        onLog(LogKind.Warn, "  dependency install failed — skipping this engine");
                        continue;
                    }
                }

                string args = engine.Id switch
                {
                    // NOTE: --detect on deob.js is a detect-only mode that exits without
                    // producing output — never pass it when we actually want a result.
                    "caomod" or "luraph151" or "luraph152" => $"\"{entry}\" \"{inputPath}\" -o \"{outputPath}\""
                        + (options.Devirtualize ? "" : " --no-devirt"),
                    "moonsec" => $"bin/ms.js \"{inputPath}\" -o \"{outputPath}\"",
                    "prometheus" => $"bin/pdeobf.js \"{inputPath}\" -o \"{outputPath}\"",
                    "luaobf" => $"cli.py \"{inputPath}\" \"{outputPath}\"" + (options.ReadableNames ? " --names descriptive" : ""),
                    _ => "",
                };

                var result = await ProcessRunner.RunAsync(exe, args, workDir,
                    line => onLog(LogKind.Raw, "  " + line),
                    line => onLog(LogKind.Warn, "  " + line),
                    ct, timeoutSeconds: 600).ConfigureAwait(false);

                // luaobf cli.py exits 3 for "success with warnings".
                bool engineOk = result.Success || (engine.Id == "luaobf" && result.ExitCode == 3);
                outputReady = engineOk && File.Exists(outputPath) && new FileInfo(outputPath).Length > 0;
            }

            if (outputReady)
            {
                // Luraph engines: optional extra tidy pass with the bundled luau-ast.
                if (options.PostClean && engine.Family == "Luraph")
                {
                    var cleaned = await PostCleanAsync(Paths.EngineDir(engine.Folder), outputPath, onLog, ct).ConfigureAwait(false);
                    if (cleaned) outputPath = WithSuffix(outputPath, "_final");
                }

                await StampHeaderAsync(outputPath, onLog, ct).ConfigureAwait(false);

                sw.Stop();
                onLog(LogKind.Ok, $"output ready: {outputPath}");
                onLog(LogKind.Info, $"  size {new FileInfo(outputPath).Length:N0} bytes, {sw.Elapsed.TotalSeconds:0.0}s");
                return new DeobfReport(true, engine.DisplayName, detected, outputPath, sw.Elapsed, attempts);
            }

            onLog(LogKind.Warn, $"engine {engine.DisplayName} returned no output — trying next candidate…");
        }

        sw.Stop();
        onLog(LogKind.Error, "all candidate engines failed. Check the runtimes shown in the sidebar (node/python).");
        return new DeobfReport(false, "—", detected, outputPath, sw.Elapsed, attempts);
    }

    /// <summary>
    /// lua.expert cloud decompiler: POST the base64 script to
    /// https://api.lua.expert/v1/decompile, write the returned plain-text Luau out.
    /// </summary>
    private static async Task<bool> RunCloudAsync(string inputPath, string outputPath,
        Action<LogKind, string> onLog, CancellationToken ct)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(inputPath, ct).ConfigureAwait(false);
            onLog(LogKind.Info, $"  uploading {bytes.Length:N0} bytes to api.lua.expert…");

            var payload = JsonSerializer.Serialize(new Dictionary<string, string>
            {
                ["script"] = Convert.ToBase64String(bytes),
            });
            using var content = new StringContent(payload, Encoding.UTF8, "application/json");
            using var resp = await Http.PostAsync("https://api.lua.expert/v1/decompile", content, ct).ConfigureAwait(false);
            var text = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
            {
                onLog(LogKind.Warn, $"  http {(int)resp.StatusCode}: {Truncate(text.Trim(), 160)}");
                return false;
            }

            var trimmed = text.TrimStart();
            if (trimmed.StartsWith('{') && (trimmed.Contains("\"error\"") || trimmed.Contains("\"message\"")))
            {
                onLog(LogKind.Warn, "  api error: " + Truncate(text.Trim(), 200));
                return false;
            }
            if (string.IsNullOrWhiteSpace(text))
            {
                onLog(LogKind.Warn, "  empty response from lua.expert");
                return false;
            }

            await File.WriteAllTextAsync(outputPath, text, ct).ConfigureAwait(false);
            onLog(LogKind.Raw, "  decompiled luau received from lua.expert");
            return true;
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            onLog(LogKind.Warn, "  lua.expert failed: " + ex.Message);
            return false;
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";

    /// <summary>
    /// Prepend the LuauGhirda ASCII banner to the output file. Byte-safe (works
    /// even if an engine ever emits binary), and idempotent — files already
    /// carrying the banner are left untouched.
    /// </summary>
    private static async Task StampHeaderAsync(string outputPath, Action<LogKind, string> onLog, CancellationToken ct)
    {
        try
        {
            var bytes = await File.ReadAllBytesAsync(outputPath, ct).ConfigureAwait(false);
            var probe = Encoding.ASCII.GetString(bytes, 0, Math.Min(bytes.Length, 400));
            if (probe.Contains("decompiled and deobfuscated using LuauGhirda", StringComparison.Ordinal))
                return; // already stamped (e.g. re-running on a previous output)

            var header = Encoding.UTF8.GetBytes(HeaderBuilder.Build());
            using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
            await fs.WriteAsync(header, ct).ConfigureAwait(false);
            await fs.WriteAsync(bytes, ct).ConfigureAwait(false);
            onLog(LogKind.Info, "  LuauGhirda banner stamped into the output header");
        }
        catch (Exception ex)
        {
            onLog(LogKind.Warn, "  banner stamp skipped: " + ex.Message);
        }
    }

    private static async Task<bool> PostCleanAsync(string engineWorkDir, string outputPath, Action<LogKind, string> onLog, CancellationToken ct)
    {
        var python = EnvironmentScanner.PythonCommand();
        if (python is null) return false;

        string ast = Path.Combine(engineWorkDir, "bin", OperatingSystem.IsWindows() ? "luau-ast.exe" : "luau-ast");
        if (!File.Exists(ast)) return false;

        var final = WithSuffix(outputPath, "_final");
        onLog(LogKind.Info, "─ post-clean (luaclean.py via luau-ast)");

        var result = await ProcessRunner.RunAsync(python,
            $"luaclean.py \"{ast}\" \"{outputPath}\" \"{final}\" --once",
            engineWorkDir,
            line => onLog(LogKind.Raw, "  " + line),
            line => onLog(LogKind.Warn, "  " + line),
            ct, timeoutSeconds: 120).ConfigureAwait(false);

        if (result.Success && File.Exists(final) && new FileInfo(final).Length > 0)
            return true;

        onLog(LogKind.Warn, "post-clean skipped (non-fatal)");
        return false;
    }

    private static string WithSuffix(string path, string suffix)
    {
        var dir = Path.GetDirectoryName(path) ?? ".";
        var name = Path.GetFileNameWithoutExtension(path) + suffix;
        var ext = Path.GetExtension(path);
        return Path.Combine(dir, name + ext);
    }
}

public enum LogKind
{
    Header,
    Info,
    Ok,
    Warn,
    Error,
    Raw,
}
