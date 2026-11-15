using System.Text.Json;
namespace HydroLink.Core;
public static class PluginOscInputs
{
    public static IReadOnlyDictionary<string,string> Resolve(IEnumerable<PluginEntry> plugins,IReadOnlyCollection<string> enabled,string settingsFolder,Action<string> log)
    {
        var result=new Dictionary<string,string>(StringComparer.Ordinal);
        foreach(var plugin in plugins.Where(p=>enabled.Contains(p.Manifest.Id))) {
            try {
                var declared=plugin.Manifest.OscInputs ?? new Dictionary<string,string>();
                foreach(var pair in declared) Add(pair.Key,pair.Value);
                // Compatibility for the first ESB release, which predates manifest input declarations.
                if(plugin.Manifest.Id=="hydro.haptic.esb" && declared.Count==0) {
                    var names=new Dictionary<string,string> { ["Left"]="HydroHaptic/Left",["Right"]="HydroHaptic/Right",["Intensity"]="HydroHaptic/Intensity",["Enabled"]="HydroHaptic/Enabled",["Heartbeat"]="HydroHaptic/Heartbeat" };
                    var file=Path.Combine(settingsFolder,"hydro-haptic-parameters.json");
                    if(File.Exists(file)) {
                        using var doc=JsonDocument.Parse(File.ReadAllText(file));
                        foreach(var key in names.Keys.ToArray()) if(doc.RootElement.TryGetProperty(key,out var value)) names[key]=value.GetString() ?? "";
                    }
                    foreach(var pair in names) Add("/avatar/parameters/"+pair.Value,pair.Key is "Enabled" or "Heartbeat"?"T":"f");
                }
            } catch(Exception ex) when(ex is IOException or JsonException or InvalidOperationException or ArgumentException) { log("Plugin-OSC-Anmeldung: "+plugin.Manifest.Name+" · "+ex.Message); }
        }
        return result;
        void Add(string address,string type) {
            if(!address.StartsWith("/avatar/parameters/",StringComparison.Ordinal) || address.Length>256 || address.Any(char.IsWhiteSpace) || address.Contains("//") || type is not ("f" or "i" or "T" or "F" or "s")) throw new ArgumentException("Ungültiger OSC-Eingang im Plugin.");
            if(result.TryGetValue(address,out var old) && old!=type) throw new ArgumentException("Widersprüchliche OSC-Typen: "+address);
            result[address]=type;
        }
    }
}
