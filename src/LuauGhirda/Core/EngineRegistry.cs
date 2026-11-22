namespace LuauGhirda.Core;

public enum EngineRuntime
{
    Node,
    Python,
    Cloud,
}

public enum EngineFamily
{
    Luraph,
    MoonSec,
    Prometheus,
    LuaObfuscator,
    LuaExpert,
}

/// <summary>One bundled deobfuscation engine.</summary>
public sealed record EngineDef(
    string Id,
    string DisplayName,
    string Folder,
    EngineRuntime Runtime,
    string Family,
    string[] Markers,
    string Credit,
    bool NeedsNpmInstall)
{
    public override string ToString() => DisplayName;
}

/// <summary>
/// The six engines shipped under engines/ (five local + the lua.expert cloud
/// decompiler) plus the marker heuristics used by "Auto-detect" to route a
/// script to the right one.
/// </summary>
public static class EngineRegistry
{
    public static readonly IReadOnlyList<EngineDef> All = new[]
    {

        new EngineDef("luraph151", "Luraph v15.1", "Luraph-v15.1-Deobfuscator",
            EngineRuntime.Node, "Luraph",
            new[] { "LPH_NO_VIRTUALIZE", "LPH_ENCSTR", "LPH_ENCFUNC", "LPH_CRASH", "LPH_JIT", "LPH_" },
            "", false),

        new EngineDef("caomod", "Luraph 14.x and 15 deobfuscator", "CaoMod-Luraph-Deobfuscator",
            EngineRuntime.Node, "Luraph",
            new[] { "LPH_", "lura.ph" },
            "", false),

        new EngineDef("moonsec", "MoonSec V3", "MoonSec-Deobfuscator",
            EngineRuntime.Node, "MoonSec",
            new[] { "MoonSec", "moonsec", "Moonsec" },
            "", true),

        new EngineDef("prometheus", "Prometheus", "Prometheus-Deobfuscator-Fixed",
            EngineRuntime.Node, "Prometheus",
            new[] { "prometheus", "wearedevs" },
            "", false),

        new EngineDef("luaobf", "luaobfuscator.com", "LuaObfuscator-Deobfuscator",
            EngineRuntime.Python, "LuaObfuscator",
            new[] { "luaobfuscator", "OBFUSCATEV1", "CHAOTIC" },
            "", false),

        new EngineDef("luaexpert", "lua.expert (cloud decompiler)", "",
            EngineRuntime.Cloud, "LuaExpert",
            new[] { "\u001bLuau", "LuauVM" },
            "", false),
    };

    public static EngineDef? ById(string id) => All.FirstOrDefault(e => e.Id == id);

    /// <summary>
    /// Heuristic classifier: scans the raw script for known obfuscator markers and
    /// returns engines ranked by likelihood. Compiled Luau bytecode routes straight
    /// to the lua.expert cloud decompiler; unmatched source falls back through the
    /// local engines in a sensible global order.
    /// </summary>
    public static IReadOnlyList<EngineDef> Rank(string script)
    {
        var scores = new Dictionary<string, double>();
        foreach (var e in All) scores[e.Id] = 0;

        foreach (var e in All)
        {
            foreach (var marker in e.Markers)
            {
                if (script.Contains(marker, StringComparison.OrdinalIgnoreCase))
                    scores[e.Id] += 10;
            }
        }

        // Compiled Luau bytecode (starts with ESC 'Luau') belongs to lua.expert.
        if (script.StartsWith('\u001b') && script.Contains("Luau", StringComparison.Ordinal))
            scores["luaexpert"] += 50;

        // Luraph VM shape: heavy dispatcher tables with LPH constants.
        if (script.Contains("LPH_", StringComparison.Ordinal)) scores["luraph152"] += 6;
        if (script.Contains("LPH_", StringComparison.Ordinal)) scores["luraph151"] += 5;
        if (script.Contains("LPH_", StringComparison.Ordinal)) scores["caomod"] += 4;

        // MoonSec V3 leaves function-table dispatchers and dump-like header strings.
        if (script.Contains('\u001b') && script.Contains("MoonSec", StringComparison.OrdinalIgnoreCase))
            scores["moonsec"] += 8;

        // Prometheus (wearedevs fork): massive \escape string packing in one or few lines.
        if (CountChar(script, '\\') > script.Length * 0.08)
            scores["prometheus"] += 5;

        // luaobfuscator.com presets embed preset tags.
        if (script.Contains("luaobfuscator.com", StringComparison.OrdinalIgnoreCase))
            scores["luaobf"] += 10;

        var ranked = All.OrderByDescending(e => scores[e.Id]).ToList();
        return ranked;
    }

    private static long CountChar(string s, char c)
    {
        long n = 0;
        foreach (var ch in s) if (ch == c) n++;
        return n;
    }
}
