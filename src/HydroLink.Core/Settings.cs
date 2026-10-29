using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace HydroLink.Core;

public sealed class Settings
{
    public List<string> HideInactiveModules { get; set; } = [];
    public bool Fitness { get; set; }
    public bool WindForce { get; set; }
    public bool OscHub { get; set; }
    public bool Haptics { get; set; }
    public string MovementSource { get; set; } = "VRTI";
    public string HeartSource { get; set; } = "VRCOSC";
    public string VrtiUrl { get; set; } = "ws://127.0.0.1:47126";
    public string HeartUrl { get; set; } = "ws://127.0.0.1:36210";
    public string FitOscUrl { get; set; } = "ws://127.0.0.1:6547/";
    public string PulsoidToken { get; set; } = "";
    public string HypeRateKey { get; set; } = "";
    public string HypeRateId { get; set; } = "";
    public string WindHost { get; set; } = "";
    public int WindPort { get; set; } = 4210;
    public string WindOscAddress { get; set; } = "/avatar/parameters/WindForce";
    public string IntifaceUrl { get; set; } = "ws://127.0.0.1:12345";
    public HomeAssistantOptions HomeAssistant { get; set; } = new();
    public bool Companion { get; set; }
    public string CompanionUrl { get; set; } = "http://127.0.0.1:38491";
    public string CompanionCode { get; set; } = "";
    public bool ManualOsc { get; set; }
    public string ManualOscHost { get; set; } = "127.0.0.1";
    public int ManualOscPort { get; set; } = 9000;
    public bool SendHeartToOsc { get; set; }
    public string HeartOscAddress { get; set; } = "/avatar/parameters/HeartRate";
    public List<string> EnabledPlugins { get; set; } = [];
    public List<DashboardCard> Dashboard { get; set; } = DashboardCard.Defaults();
    public List<LaunchProgram> Programs { get; set; } = LaunchProgram.Defaults();
    public bool StartModulesWithApp { get; set; }
    public string WindMovementSource { get; set; } = "None";
    public string WindHeartSource { get; set; } = "None";
    public List<WindRule> WindRules { get; set; } = WindRule.Defaults();
    public int WindRiseMs { get; set; } = 120;
    public bool WindMultiPwm { get; set; }
    public string WindHeartMode { get; set; } = "SOURCE";
    public FitnessOptions FitnessOptions { get; set; } = new();
    public VrcHapticsBridge.Core.BridgeSettings HapticsOptions { get; set; } = new();
    public List<OscRoute> OscRoutes { get; set; } = [];
    public static string Folder => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "VRC HydroLink");
    public static string FilePath => Path.Combine(Folder, "settings.json");
    public static Settings Load(string? path = null, Action<string>? warning = null)
    {
        path ??= FilePath;
        Settings Read(string file) => JsonSerializer.Deserialize<Settings>(File.ReadAllText(file)) ?? throw new JsonException("Leere Einstellungen");
        Settings settings;
        try { settings = File.Exists(path) ? Read(path) : File.Exists(path + ".bak") ? Read(path + ".bak") : new(); }
        catch (Exception ex) when (ex is JsonException or IOException)
        {
            if (!File.Exists(path + ".bak")) throw;
            settings = Read(path + ".bak");
            warning?.Invoke("Einstellungen beschädigt oder nicht lesbar; letzte Sicherung geladen. Originaldatei bleibt erhalten.");
        }
        settings.PulsoidToken = Unprotect(settings.PulsoidToken);
        settings.HypeRateKey = Unprotect(settings.HypeRateKey);
        settings.CompanionCode = Unprotect(settings.CompanionCode);
        settings.HomeAssistant.Password = Unprotect(settings.HomeAssistant.Password);
        return settings;
    }
    private static string Protect(string value)
    {
        if (value.Length == 0) return value;
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Credential storage requires Windows");
        return "dpapi:" + Convert.ToBase64String(ProtectedData.Protect(Encoding.UTF8.GetBytes(value), null, DataProtectionScope.CurrentUser));
    }
    private static string Unprotect(string value)
    {
        if (!value.StartsWith("dpapi:")) return value;
        if (!OperatingSystem.IsWindows()) throw new PlatformNotSupportedException("Credential storage requires Windows");
        return Encoding.UTF8.GetString(ProtectedData.Unprotect(Convert.FromBase64String(value[6..]), null, DataProtectionScope.CurrentUser));
    }
    private static readonly object saveGate = new();
    public void Save(string? path = null)
    {
        path ??= FilePath;
        lock (saveGate)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
            var copy = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(this))!;
            copy.PulsoidToken = Protect(PulsoidToken); copy.HypeRateKey = Protect(HypeRateKey); copy.CompanionCode = Protect(CompanionCode); copy.HomeAssistant.Password = Protect(HomeAssistant.Password);
            var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                using (var file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    JsonSerializer.Serialize(file, copy, new JsonSerializerOptions { WriteIndented = true });
                    file.Flush(true);
                }
                if (File.Exists(path))
                {
                    bool valid;
                    try { valid = JsonSerializer.Deserialize<Settings>(File.ReadAllText(path)) != null; }
                    catch (JsonException) { valid = false; }
                    // Never replace the last good backup with damaged JSON.
                    File.Replace(temporary, path, valid ? path + ".bak" : null);
                }
                else File.Move(temporary, path);
            }
            finally { if (File.Exists(temporary)) File.Delete(temporary); }
        }
    }
}
