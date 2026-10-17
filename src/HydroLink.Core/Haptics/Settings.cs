using System.Text.Json;

namespace VrcHapticsBridge.Core;

public sealed record BridgeSettings
{
    public string ServerUrl { get; init; } = "ws://127.0.0.1:12345";
    public int OscPort { get; init; } = 9001;
    public bool UseOscQuery { get; init; } = true;
    public string VibrateAddress { get; init; } = "/avatar/parameters/MaxVibrate";
    public string PumpAddress { get; init; } = "/avatar/parameters/MaxPump";
    public int IdleTimeoutSeconds { get; init; } = 0;

    public void Validate()
    {
        if (!Uri.TryCreate(ServerUrl, UriKind.Absolute, out var uri) ||
            (uri.Scheme != "ws" && uri.Scheme != "wss") || !string.IsNullOrEmpty(uri.UserInfo))
            throw new ArgumentException("Eine gültige ws:// oder wss:// Intiface-Adresse ohne Zugangsdaten eingeben.");
        if (OscPort is < 1024 or > 65535) throw new ArgumentException("OSC-Port muss zwischen 1024 und 65535 liegen.");
        foreach (var address in new[] { VibrateAddress, PumpAddress })
            if (!address.StartsWith('/') || address.Length < 2 || address.Any(char.IsWhiteSpace) || address.IndexOfAny(['*', '?', '[', ']', '{', '}', ',', '\0']) >= 0)
                throw new ArgumentException("OSC-Adressen müssen mit / beginnen und dürfen keine Leerzeichen oder Platzhalter enthalten.");
        if (VibrateAddress == PumpAddress) throw new ArgumentException("Vibration und Pump benötigen unterschiedliche OSC-Adressen.");
        if (IdleTimeoutSeconds is < 0 or > 600) throw new ArgumentException("Inaktivitätslimit: 0 (aus) bis 600 Sekunden.");
    }

    public static BridgeSettings Load(string path)
    {
        if (!File.Exists(path)) return new();
        var settings = JsonSerializer.Deserialize<BridgeSettings>(File.ReadAllText(path)) ?? new();
        settings.Validate();
        return settings;
    }

    public void Save(string path)
    {
        Validate();
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path + ".tmp", JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        File.Move(path + ".tmp", path, true);
    }
}
