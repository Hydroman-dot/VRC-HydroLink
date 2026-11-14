using System.Text.Json;
namespace HydroLink.Core;

/// <summary>Recover legacy plugins when the preceding application used an older updater.</summary>
public static class PluginCarryover
{
    public static bool Recover(string applicationFolder, string updateRoot, Action<string> log)
    {
        var app=Path.TrimEndingDirectorySeparator(Path.GetFullPath(applicationFolder));
        var root=Path.TrimEndingDirectorySeparator(Path.GetFullPath(updateRoot));
        var stateFile=Path.Combine(root,"state.json");
        AssertPlainPath(stateFile);
        if(!File.Exists(stateFile)) return false;
        using var document=JsonDocument.Parse(File.ReadAllText(stateFile));
        var state=document.RootElement;
        var current=state.GetProperty("current").GetString();
        var previous=state.GetProperty("previous").GetString();
        if(!IsVersion(current) || !IsVersion(previous) || current==previous) return false;
        if(!app.Equals(Path.Combine(root,current!),StringComparison.OrdinalIgnoreCase)) return false;
        var source=Path.Combine(root,previous!,"plugins"); var target=Path.Combine(app,"plugins");
        AssertPlainPath(source); AssertPlainPath(target);
        if(!Directory.Exists(source) || Directory.Exists(target)) return false;
        var stage=Path.Combine(app,".plugins-recovery-"+Guid.NewGuid().ToString("N"));
        try {
            CopyDirectory(source,stage);
            Directory.Move(stage,target);
            log("Vorhandene Plugins samt lokalen Einstellungen aus der vorherigen App-Version übernommen.");
            return true;
        } finally { if(Directory.Exists(stage)) Directory.Delete(stage,true); }
    }
    private static bool IsVersion(string? value) => value is not null && System.Text.RegularExpressions.Regex.IsMatch(value,@"\A[0-9]+\.[0-9]+\.[0-9]+\z") && Version.TryParse(value,out _);
    private static void AssertPlainPath(string path)
    {
        for(var current=Path.GetFullPath(path); !string.IsNullOrEmpty(current); current=Path.GetDirectoryName(current))
            if((File.Exists(current) || Directory.Exists(current)) && (File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)
                throw new IOException("Verknüpfte Plugin-Pfade werden nicht automatisch übernommen.");
    }
    private static void CopyDirectory(string source,string target)
    {
        AssertPlainPath(source); AssertPlainPath(target); Directory.CreateDirectory(target);
        foreach(var entry in new DirectoryInfo(source).EnumerateFileSystemInfos()) {
            if((entry.Attributes&FileAttributes.ReparsePoint)!=0) throw new IOException("Verknüpfte Plugin-Dateien werden nicht automatisch übernommen.");
            var destination=Path.Combine(target,entry.Name);
            if(entry is DirectoryInfo) CopyDirectory(entry.FullName,destination);
            else File.Copy(entry.FullName,destination,false);
        }
    }
}
