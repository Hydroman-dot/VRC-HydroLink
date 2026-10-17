using System.Text.Json;
namespace VrcHapticsBridge.Core;

public sealed record SpsSource(string Kind, string Prefix, double LengthMeters = .2)
{
    public static SpsSource? FromLiveAddress(string address)
    {
        var plug = address.StartsWith("/avatar/parameters/OGB/Pen/", StringComparison.Ordinal);
        if ((!plug && !address.StartsWith("/avatar/parameters/OGB/Orf/", StringComparison.Ordinal)) || address.Count(c => c == '/') != 6) return null;
        var split = address.LastIndexOf('/');
        if (plug && address[(split + 1)..] is not ("PenSelf" or "PenOthers")) return null;
        if (address[(split + 1)..] is not ("PenSelf" or "PenOthers" or "PenOthersClose" or "PenSelfNewRoot" or "PenSelfNewTip" or "PenOthersNewRoot" or "PenOthersNewTip")) return null;
        var source = new SpsSource(plug ? "Plug" : "Socket", address[..(split + 1)]);
        try { source.Validate(); return source; } catch (ArgumentException) { return null; }
    }
    public override string ToString() => $"{Kind} · {Prefix.Split('/')[^2]}";
    public void Validate()
    {
        var start = Kind == "Plug" ? "/avatar/parameters/OGB/Pen/" : Kind == "Socket" ? "/avatar/parameters/OGB/Orf/" : "";
        if (start.Length == 0 || !Prefix.StartsWith(start, StringComparison.Ordinal) || !Prefix.EndsWith('/') || Prefix.Length <= start.Length + 1
            || Prefix.Length > 512 || Prefix.Any(char.IsControl) || Prefix[start.Length..^1].Contains('/'))
            throw new ArgumentException("Ungültige SPS-Quelle.");
        if (!double.IsFinite(LengthMeters) || LengthMeters is < .02 or > 1) throw new ArgumentException("Socket-Kalibrierlänge: 2–100 cm.");
    }
    // Called with addresses advertised by the live OSCQuery tree, never with contract guesses.
    public static SpsSource[] Discover(IEnumerable<string> addresses)
    {
        return addresses.Where(a => a.StartsWith("/avatar/parameters/OGB/Pen/", StringComparison.Ordinal) ||
            a.StartsWith("/avatar/parameters/OGB/Orf/", StringComparison.Ordinal))
            .Where(a => a.Count(c => c == '/') == 6)
            .GroupBy(a => a[..(a.LastIndexOf('/') + 1)])
            .Where(g => g.Any(a => a.EndsWith("/PenSelf") || a.EndsWith("/PenOthers") || a.EndsWith("/PenSelfNewRoot") || a.EndsWith("/PenOthersNewRoot")))
            .Select(g => new SpsSource(g.Key.Contains("/OGB/Pen/") ? "Plug" : "Socket", g.Key)).OrderBy(s => s.Prefix).ToArray();
    }
}
public sealed record WizardInventory(string Avatar, int Plugs, int Sockets, string[] Candidates)
{
    public static WizardInventory Read(string path)
    {
        var info = new FileInfo(path);
        if (info.Length > 2_000_000) throw new ArgumentException("Wizard-Datei ist zu groß.");
        using var json = JsonDocument.Parse(File.ReadAllText(path), new JsonDocumentOptions { MaxDepth = 32 });
        var root = json.RootElement;
        if (!root.TryGetProperty("schemaVersion", out var version) || version.GetInt32() is < 1 or > 3)
            throw new ArgumentException("Unbekannte Wizard-Vertragsversion.");
        var sources = root.TryGetProperty("sources", out var all) ? all.EnumerateArray().ToArray()
            : root.TryGetProperty("sockets", out var sockets) ? sockets.EnumerateArray().ToArray() : [];
        return new(root.TryGetProperty("avatarName", out var avatar) ? avatar.GetString() ?? "" : "",
            sources.Count(s => s.TryGetProperty("kind", out var kind) && kind.GetString() == "Plug"),
            sources.Count(s => !s.TryGetProperty("kind", out var kind) || kind.GetString() == "Socket"),
            sources.Where(s => s.TryGetProperty("candidatePrefix", out var p) && p.ValueKind == JsonValueKind.String)
                .Select(s => s.GetProperty("candidatePrefix").GetString()!).Where(s => s.Length > 0).Distinct().ToArray());
    }
}
/// <summary>Separates source geometry from device capability routing. No query values actuate hardware.</summary>
public sealed class SpsInput
{
    private readonly Dictionary<string, double> values = new(StringComparer.Ordinal);
    public void Clear() => values.Clear();
    public void Receive(string address, double value)
    {
        if (double.IsFinite(value) && (values.ContainsKey(address) || values.Count < 4096)) values[address] = Math.Clamp(value, 0, 1);
    }
    public double Level(SpsSource source)
    {
        double Get(string suffix) => values.GetValueOrDefault(source.Prefix + suffix);
        double Party(string party)
        {
            if (source.Kind == "Plug") return Get("Pen" + party);
            var rootKey = source.Prefix + "Pen" + party + "NewRoot";
            var tipKey = source.Prefix + "Pen" + party + "NewTip";
            if (values.ContainsKey(rootKey) || values.ContainsKey(tipKey))
            {
                // SPS socket receiver spheres have 1 m range. A saturated tip indicates entry.
                // User-calibrated length avoids guessing an unknown partner's geometry.
                if (!values.TryGetValue(rootKey, out var root) || !values.TryGetValue(tipKey, out var tip)
                    || root <= 0 || tip <= .99) return 0;
                return Math.Clamp(1 - (1 - root) / source.LengthMeters, 0, 1);
            }
            return party == "Others" && values.TryGetValue(source.Prefix + "PenOthersClose", out var close) && close < .5
                ? 0 : Get("Pen" + party);
        }
        return Math.Max(Party("Self"), Party("Others"));
    }
}
