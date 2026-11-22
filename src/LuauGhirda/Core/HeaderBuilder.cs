using System.Text;

namespace LuauGhirda.Core;

/// <summary>
/// Builds the banner LuauGhirda writes at the top of every output file and
/// console session — dashes, a mini ASCII computer, and the signature line.
/// </summary>
public static class HeaderBuilder
{
    public const string Version = "V1.0.0";

    /// <summary>Mini computer artwork, one line per row (kept ≤ 26 columns wide).</summary>
    private static readonly string[] Art =
    {
        @"      .------------.      ",
        @"      | .--------. |      ",
        @"      | | >_ luau | |     ",
        @"      | '--------' |      ",
        @"   .--+----------+--.     ",
        @"  [__| LGH-9000 |__]      ",
        @"      '----------'       ",
        @"       \________/        ",
    };

    public static string Build(int width = 72)
    {
        width = Math.Max(60, width);
        var sb = new StringBuilder();

        // Line 1 — dashes flowing into the product name.
        sb.Append('-', width - ("LuauGhirda " + Version).Length - 2);
        sb.Append(" LuauGhirda ");
        sb.Append(Version);
        sb.AppendLine();

        // Line 2..n — artwork row wrapped in dashes so every line is exactly `width` wide.
        foreach (var artLine in Art)
        {
            string trimmed = artLine.TrimEnd();
            int dashes = width - 3 - trimmed.Length;
            if (dashes < 2) dashes = 2;
            sb.Append("-- ");
            sb.Append(trimmed);
            sb.Append('-', dashes);
            sb.AppendLine();
        }

        // Full rule.
        sb.Append('-', width);
        sb.AppendLine();

        // Signature.
        sb.Append("-- decompiled and deobfuscated using LuauGhirda ");
        sb.Append(Version.ToLowerInvariant());
        sb.Append(" ----");
        sb.AppendLine();

        return sb.ToString();
    }
}
