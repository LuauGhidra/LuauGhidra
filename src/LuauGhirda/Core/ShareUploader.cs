using System.IO;
using System.Net.Http;
using System.Text;
using System.Text.Json;

namespace LuauGhirda.Core;

/// <summary>
/// Share a deobfuscated script: POST it as a public scrap to Rubiš
/// (rubis.app) and hand back the shareable view link.
/// POST https://api.rubis.app/v2/scrap?public=true&amp;title=…  (body: text/plain)
/// → { "success": true, "view": "https://rubis.app/view/?scrap=…", "raw": … }
/// </summary>
public static class ShareUploader
{
    private static readonly HttpClient Http = new() { Timeout = TimeSpan.FromSeconds(90) };

    public static async Task<(bool Ok, string LinkOrError)> UploadAsync(string filePath, CancellationToken ct = default)
    {
        try
        {
            if (!File.Exists(filePath)) return (false, "output file not found");
            var text = await File.ReadAllTextAsync(filePath, ct).ConfigureAwait(false);
            if (text.Length > 3 * 1024 * 1024)
                return (false, "script exceeds the free 3 MB scrap limit — share the file instead");

            var title = Uri.EscapeDataString("LuauGhirda output — " + Path.GetFileName(filePath));
            using var content = new StringContent(text, Encoding.UTF8, "text/plain");
            using var resp = await Http.PostAsync(
                $"https://api.rubis.app/v2/scrap?public=true&title={title}", content, ct).ConfigureAwait(false);
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);

            if (!resp.IsSuccessStatusCode)
                return (false, $"http {(int)resp.StatusCode} — {Truncate(body, 140)}");

            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.False)
                return (false, "rubis rejected the upload");
            string link = root.TryGetProperty("view", out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() ?? ""
                : "";
            return string.IsNullOrWhiteSpace(link) ? (false, "no link in response") : (true, link);
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            return (false, ex.Message);
        }
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "…";
}
