using System.Text.Json;
using System.Text.RegularExpressions;
namespace HydroLink.Core;
public sealed partial class PluginUpdates
{
    public static string Repository(string input)
    {
        var value=input.Trim().TrimEnd('/');
        if (value.StartsWith("https://",StringComparison.OrdinalIgnoreCase)) {
            if(!Uri.TryCreate(value,UriKind.Absolute,out var uri) || uri.Host!="github.com" || !uri.IsDefaultPort || uri.UserInfo.Length>0 || uri.Query.Length>0 || uri.Fragment.Length>0)
                throw new InvalidDataException("Bitte einen GitHub-Repository-Link ohne Zusatzparameter eingeben.");
            value=uri.AbsolutePath.Trim('/');
        }
        if(value.EndsWith(".git",StringComparison.Ordinal)) value=value[..^4];
        if(!Regex.IsMatch(value,@"\A[A-Za-z0-9_-]+/[A-Za-z0-9_-][A-Za-z0-9_.-]*\z") || value.Contains(".."))
            throw new InvalidDataException("Erwartet: https://github.com/Anbieter/Repository oder Anbieter/Repository.");
        return value;
    }
    public async Task<PluginUpdate> FindRepositoryAsync(string input,CancellationToken token)
    {
        var repo=Repository(input);
        using var request=new HttpRequestMessage(HttpMethod.Get,$"https://api.github.com/repos/{repo}/releases/latest");
        request.Headers.UserAgent.ParseAdd("HydroLink-Plugins/1.0");
        using var response=await http.SendAsync(request,token); response.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token)); var release=json.RootElement;
        if(release.GetProperty("draft").GetBoolean() || release.GetProperty("prerelease").GetBoolean()) throw new InvalidDataException("Kein stabiles Plugin-Release vorhanden.");
        var version=release.GetProperty("tag_name").GetString()!.TrimStart('v');
        if(!Version.TryParse(version,out _)) throw new InvalidDataException("Release hat keine unterstützte Versionsnummer.");
        var assets=release.GetProperty("assets").EnumerateArray().ToArray();
        var suffix="-"+version+".zip";
        var candidates=assets.Where(a=>a.GetProperty("name").GetString() is { } name && name.EndsWith(suffix,StringComparison.Ordinal) && assets.Any(c=>c.GetProperty("name").GetString()==name+".sha256")).ToArray();
        if(candidates.Length!=1) throw new InvalidDataException("Das Release muss genau ein HydroLink-Plugin-Paket mit passender SHA-256-Datei enthalten.");
        var filename=candidates[0].GetProperty("name").GetString()!;
        var id=filename[..^suffix.Length];
        var manifest=new PluginManifest(id,id,"plugin.dll","0.0.0",GitHubRepository:repo); manifest.Validate();
        Uri Asset(JsonElement asset) {
            var uri=new Uri(asset.GetProperty("browser_download_url").GetString()!);
            if(uri.Scheme!="https" || uri.Host!="github.com" || !uri.IsDefaultPort || uri.UserInfo.Length>0 || !uri.AbsolutePath.StartsWith("/"+repo+"/releases/download/",StringComparison.Ordinal)) throw new InvalidDataException("Paket stammt nicht aus dem gewählten Repository.");
            return uri;
        }
        return new(new(manifest,"",false),version,Asset(candidates[0]),Asset(assets.Single(a=>a.GetProperty("name").GetString()==filename+".sha256")));
    }
}
