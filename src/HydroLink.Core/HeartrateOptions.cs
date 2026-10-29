namespace HydroLink.Core;
public sealed class HeartrateOptions
{
    public ulong BluetoothAddress { get; set; }
    public string DeviceName { get; set; } = "Coospo H9Z";
    public bool VrcOscParameters { get; set; }
    public bool SmoothValue { get; set; } = true;
    public int ValueSmoothingMs { get; set; } = 1000;
    public bool SmoothAverage { get; set; } = true;
    public int AverageSmoothingMs { get; set; } = 1000;
    public int AveragePeriodMs { get; set; } = 10000;
    public int LowerBpm { get; set; }
    public int UpperBpm { get; set; } = 240;
    public bool PulseBeat { get; set; }
    public Dictionary<string, string> Parameters { get; set; } = Names.ToDictionary(n => n, n => "/avatar/parameters/VRCOSC/Heartrate/" + n);
    public static readonly string[] Names = ["Connected", "Value", "Normalised", "Average", "Beat", "Enabled", "Units", "Tens", "Hundreds"];
    public string Address(string name) => Parameters.GetValueOrDefault(name) ?? "/avatar/parameters/VRCOSC/Heartrate/" + name;
    public void Validate()
    {
        if (LowerBpm < 0 || UpperBpm <= LowerBpm || UpperBpm > 1000) throw new ArgumentException("Pulsgrenzen müssen aufsteigend zwischen 0 und 1000 liegen.");
        if (AveragePeriodMs is < 1000 or > 300000 || ValueSmoothingMs is < 0 or > 30000 || AverageSmoothingMs is < 0 or > 30000) throw new ArgumentException("Mittelwert: 1000–300000 ms; Glättung: 0–30000 ms.");
        foreach (var name in Names)
        {
            var address = Address(name);
            if (!address.StartsWith("/avatar/parameters/") || address.EndsWith('/') || address.Any(char.IsWhiteSpace) || address.IndexOfAny(['*','?','[',']','{','}',',','\0']) >= 0) throw new ArgumentException("Ungültige Puls-Parameteradresse: " + name);
        }
        if (Names.Select(Address).Distinct().Count() != Names.Length) throw new ArgumentException("Puls-Parameteradressen müssen eindeutig sein.");
    }
}
