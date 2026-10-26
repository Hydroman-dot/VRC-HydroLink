using System.Text.Json;
using System.Text.RegularExpressions;
namespace HydroLink.Core;
public static class ReleaseUpdate
{
    public static string? NewerVersion(JsonElement release, Version current)
    {
        if (release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) return null;
        var tag = release.GetProperty("tag_name").GetString() ?? "";
        if (!Regex.IsMatch(tag, @"\Av[0-9]+\.[0-9]+\.[0-9]+\z") || !Version.TryParse(tag[1..], out var version)) return null;
        var installed = new Version(current.Major, current.Minor, Math.Max(0, current.Build));
        if (version <= installed) return null;
        var name = "VRC-HydroLink-" + tag[1..] + "-win-x64.zip";
        foreach (var required in new[] { name, name + ".sha256" })
        {
            var assets = release.GetProperty("assets").EnumerateArray().Where(a => a.GetProperty("name").GetString() == required).ToArray();
            if (assets.Length != 1 || !Uri.TryCreate(assets[0].GetProperty("browser_download_url").GetString(), UriKind.Absolute, out var uri) || uri.Scheme != "https" || uri.Host != "github.com" || !uri.AbsolutePath.StartsWith("/Hydroman-dot/VRC-HydroLink/releases/download/", StringComparison.Ordinal)) return null;
        }
        return tag[1..];
    }
}
