using System.Text.Json;
using System.Security.Cryptography;
using System.Text;

namespace HydroLink.Core;

public sealed class Settings
{
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
    public static Settings Load()
    {
        var settings = File.Exists(FilePath) ? JsonSerializer.Deserialize<Settings>(File.ReadAllText(FilePath)) ?? new() : new();
        settings.PulsoidToken = Unprotect(settings.PulsoidToken);
        settings.HypeRateKey = Unprotect(settings.HypeRateKey);
        settings.CompanionCode = Unprotect(settings.CompanionCode);
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
    public void Save()
    {
        Directory.CreateDirectory(Folder);
        var copy = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(this))!;
        copy.PulsoidToken = Protect(PulsoidToken); copy.HypeRateKey = Protect(HypeRateKey); copy.CompanionCode = Protect(CompanionCode);
        File.WriteAllText(FilePath + ".tmp", JsonSerializer.Serialize(copy, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(FilePath + ".tmp", FilePath, true);
    }
}
