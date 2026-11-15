using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace HydroLink.Core;

public sealed record PluginManifest(string Id, string Name, string Assembly, string Version = "0.0.0", int ApiVersion = 1,
    string? GitHubRepository = null, string MinimumHydroLinkVersion = "0.17.2", bool RequiresOsc = false, string? EntryType = null, Dictionary<string,string>? OscInputs = null)
{
    public static readonly JsonSerializerOptions Json = new() { PropertyNameCaseInsensitive = true, PropertyNamingPolicy = JsonNamingPolicy.CamelCase, WriteIndented = true };
    public void Validate()
    {
        if (string.IsNullOrEmpty(Id) || !Regex.IsMatch(Id, @"\A[a-z0-9][a-z0-9.-]{0,79}\z") || Id.Contains("..") || string.IsNullOrWhiteSpace(Name))
            throw new InvalidDataException("Ungültige Plugin-Identität.");
        if (ApiVersion != 1 || !System.Version.TryParse(Version, out _) || !System.Version.TryParse(MinimumHydroLinkVersion, out var minimum) || minimum > new System.Version(0,17,6))
            throw new InvalidDataException("Plugin-Version oder HydroLink-API nicht kompatibel.");
        if (string.IsNullOrWhiteSpace(Assembly) || Path.GetFileName(Assembly) != Assembly || Assembly.Contains(':') || !Assembly.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Plugin-DLL muss im Paket-Hauptordner liegen.");
        if (GitHubRepository is not null && !Regex.IsMatch(GitHubRepository, @"\A[A-Za-z0-9_-]+/[A-Za-z0-9_.-]+\z"))
            throw new InvalidDataException("Ungültiges GitHub-Repository.");
    }
    public static PluginManifest Read(string folder)
    {
        var result = JsonSerializer.Deserialize<PluginManifest>(File.ReadAllText(Path.Combine(folder,"plugin.json")), Json) ?? throw new InvalidDataException("Plugin-Manifest fehlt.");
        result.Validate();
        if (!File.Exists(Path.Combine(folder,result.Assembly))) throw new InvalidDataException("Plugin-DLL fehlt.");
        return result;
    }
}
public sealed record PluginEntry(PluginManifest Manifest, string Folder, bool Managed);
public sealed record PluginSelection(string Version, string? Previous);
public sealed class PluginCatalog(string root, Action<string> log)
{
    public static string DefaultRoot => Path.Combine(Settings.Folder, "Plugins");
    public string Root => Path.GetFullPath(root);
    public IReadOnlyList<PluginEntry> Discover(string? legacy = null)
    {
        var result = new Dictionary<string, PluginEntry>(StringComparer.Ordinal);
        if (Directory.Exists(Root)) foreach (var folder in Directory.EnumerateDirectories(Root).Where(p => !Path.GetFileName(p).StartsWith('.')))
        {
            try {
                var selection = ReadSelection(folder);
                if (selection is null) continue;
                var path = VersionFolder(folder, selection.Version);
                var manifest = PluginManifest.Read(path);
                if (manifest.Id != Path.GetFileName(folder) || manifest.Version != selection.Version) throw new InvalidDataException("Plugin-Auswahl passt nicht zum Manifest.");
                result.Add(manifest.Id, new(manifest,path,true));
            } catch(Exception ex) { log("Plugin nicht verfügbar: " + Path.GetFileName(folder) + " · " + ex.Message); }
        }
        if (legacy is not null && Directory.Exists(legacy)) foreach(var file in Directory.EnumerateFiles(legacy,"plugin.json",SearchOption.AllDirectories))
        {
            try { var folder=Path.GetDirectoryName(file)!; var manifest=PluginManifest.Read(folder); result.TryAdd(manifest.Id,new(manifest,folder,false)); }
            catch(Exception ex) { log("Altes Plugin nicht verfügbar: " + ex.Message); }
        }
        return result.Values.OrderBy(e=>e.Manifest.Name).ToArray();
    }
    static PluginSelection? ReadSelection(string folder) => File.Exists(Path.Combine(folder,"active.json")) ? JsonSerializer.Deserialize<PluginSelection>(File.ReadAllText(Path.Combine(folder,"active.json"))) : null;
    static string VersionFolder(string folder,string version)
    {
        if (!System.Version.TryParse(version,out _)) throw new InvalidDataException("Ungültige Version.");
        return Path.Combine(folder,"versions",version);
    }
    static void Select(string folder,PluginSelection selection)
    {
        var path=Path.Combine(folder,"active.json"); var temp=path+".tmp";
        File.WriteAllText(temp,JsonSerializer.Serialize(selection)); File.Move(temp,path,true);
    }
    // Packages are immutable. Loaded DLLs are never overwritten; selection takes effect after restart.
    public PluginManifest Install(string zipPath, string? expectedId = null, string? expectedVersion = null, string? expectedRepository = null)
    {
        Directory.CreateDirectory(Root);
        var stage=Path.Combine(Root,".stage-"+Guid.NewGuid().ToString("N")); Directory.CreateDirectory(stage);
        try {
            using(var zip=ZipFile.OpenRead(zipPath)) {
                if(zip.Entries.Count > 1024) throw new InvalidDataException("Zu viele Paketdateien.");
                long size=0; var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach(var entry in zip.Entries) {
                    size=checked(size+entry.Length);
                    if(size>128L*1024*1024) throw new InvalidDataException("Plugin-Paket zu groß.");
                    var name=entry.FullName.Replace('\\','/');
                    if(name.StartsWith('/') || name.Contains(':') || name.Split('/').Any(x=>x is "." or ".." || x.EndsWith(' ') || x.EndsWith('.')) || ((entry.ExternalAttributes>>16)&0xF000)==0xA000)
                        throw new InvalidDataException("Unsicherer Paketpfad.");
                    var path=Path.GetFullPath(Path.Combine(stage,name));
                    if(!path.StartsWith(stage+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase) || !seen.Add(path)) throw new InvalidDataException("Doppelter oder fremder Paketpfad.");
                    if(name.EndsWith('/')) { Directory.CreateDirectory(path); continue; }
                    Directory.CreateDirectory(Path.GetDirectoryName(path)!); entry.ExtractToFile(path);
                }
            }
            var manifest=PluginManifest.Read(stage);
            if(expectedId is not null && (manifest.Id!=expectedId || manifest.Version!=expectedVersion || manifest.GitHubRepository!=expectedRepository)) throw new InvalidDataException("Update-Identität stimmt nicht überein.");
            var folder=Path.Combine(Root,manifest.Id); var previous=ReadSelection(folder);
            var destination=VersionFolder(folder,manifest.Version);
            if(Directory.Exists(destination)) {
                var files=Directory.GetFiles(stage,"*",SearchOption.AllDirectories);
                if(files.Length!=Directory.GetFiles(destination,"*",SearchOption.AllDirectories).Length || files.Any(file=>{
                    var target=Path.Combine(destination,Path.GetRelativePath(stage,file));
                    if(!File.Exists(target)) return true;
                    using var a=File.OpenRead(file); using var b=File.OpenRead(target);
                    return !SHA256.HashData(a).SequenceEqual(SHA256.HashData(b));
                })) throw new InvalidDataException("Diese Version ist bereits mit anderem Inhalt installiert.");
                if(previous?.Version!=manifest.Version) Select(folder,new(manifest.Version,previous?.Version));
                return manifest;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            Directory.Move(stage,destination);
            try { Select(folder,new(manifest.Version,previous?.Version)); }
            catch { Directory.Move(destination,stage); throw; }
            return manifest;
        } finally { if(Directory.Exists(stage)) Directory.Delete(stage,true); }
    }
    public void Rollback(string id)
    {
        var entry=Discover().Single(p=>p.Manifest.Id==id); var folder=Path.Combine(Root,id);
        var selection=ReadSelection(folder)!;
        if(selection.Previous is null) throw new InvalidOperationException("Keine vorherige Version vorhanden.");
        var previous=PluginManifest.Read(VersionFolder(folder,selection.Previous));
        if(previous.Id!=id || previous.Version!=selection.Previous) throw new InvalidDataException("Sicherung ungültig.");
        Select(folder,new(selection.Previous,selection.Version));
    }
}
public sealed record PluginUpdate(PluginEntry Installed, string Version, Uri Archive, Uri Checksum);
public sealed partial class PluginUpdates(HttpClient http)
{
    public async Task<PluginUpdate?> CheckAsync(PluginEntry entry,CancellationToken token)
    {
        var repo=entry.Manifest.GitHubRepository;
        if(repo is null) return null;
        entry.Manifest.Validate();
        using var request=new HttpRequestMessage(HttpMethod.Get,$"https://api.github.com/repos/{repo}/releases/latest");
        request.Headers.UserAgent.ParseAdd("HydroLink-Plugins/1.0");
        using var response=await http.SendAsync(request,token); response.EnsureSuccessStatusCode();
        using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(token)); var r=json.RootElement;
        if(r.GetProperty("draft").GetBoolean() || r.GetProperty("prerelease").GetBoolean()) return null;
        var version=r.GetProperty("tag_name").GetString()!.TrimStart('v');
        if(!Version.TryParse(version,out var next) || next<=Version.Parse(entry.Manifest.Version)) return null;
        var name=entry.Manifest.Id+"-"+version+".zip";
        Uri Asset(string filename) {
            var asset=r.GetProperty("assets").EnumerateArray().Single(a=>a.GetProperty("name").GetString()==filename);
            var uri=new Uri(asset.GetProperty("browser_download_url").GetString()!);
            if(uri.Scheme!="https" || uri.Host!="github.com" || !uri.AbsolutePath.StartsWith("/"+repo+"/releases/download/",StringComparison.Ordinal)) throw new InvalidDataException("Update stammt nicht aus dem hinterlegten Repository.");
            return uri;
        }
        return new(entry,version,Asset(name),Asset(name+".sha256"));
    }
    public async Task DownloadAsync(PluginUpdate update,string destination,CancellationToken token)
    {
        var checksum=(await http.GetStringAsync(update.Checksum,token)).Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries).FirstOrDefault();
        if(checksum is null || !Regex.IsMatch(checksum,@"\A[0-9a-fA-F]{64}\z")) throw new InvalidDataException("SHA-256-Prüfsumme fehlt.");
        using var response=await http.GetAsync(update.Archive,HttpCompletionOption.ResponseHeadersRead,token); response.EnsureSuccessStatusCode();
        await using(var source=await response.Content.ReadAsStreamAsync(token)) await using(var file=File.Create(destination)) {
            var buffer=new byte[81920]; long total=0; int count;
            while((count=await source.ReadAsync(buffer,token))>0) { total+=count; if(total>64L*1024*1024) throw new InvalidDataException("Download zu groß."); await file.WriteAsync(buffer.AsMemory(0,count),token); }
        }
        await using var downloaded=File.OpenRead(destination);
        if(!Convert.ToHexString(await SHA256.HashDataAsync(downloaded,token)).Equals(checksum,StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Prüfsumme stimmt nicht überein.");
    }
}
