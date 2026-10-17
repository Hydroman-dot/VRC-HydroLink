using System.Text.Json;
using HydroLink.Sdk;
namespace HydroLink.Core;

public sealed record OscRoute
{
    public string Id { get; init; } = Guid.NewGuid().ToString("N");
    public bool Enabled { get; init; }
    public string Avatar { get; init; } = "*";
    public string Source { get; init; } = "/avatar/parameters/";
    public string Target { get; init; } = "haptics/input/vibrate";
    public string Type { get; init; } = "f";
    public double Scale { get; init; } = 1;
    public double Offset { get; init; }
    public double Min { get; init; } = 0;
    public double Max { get; init; } = 1;
    public bool Invert { get; init; }
    [System.Text.Json.Serialization.JsonIgnore]
    public string Label => $"{(Enabled ? "AN" : "AUS")} · {Avatar} · {Source} → {Target}";
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Source) || string.IsNullOrWhiteSpace(Avatar)) throw new ArgumentException("Quelle und Profil fehlen.");
        if (Source.StartsWith("osc/sent", StringComparison.Ordinal)) throw new ArgumentException("Gesendete OSC-Werte dienen nur der Anzeige, nicht als Rückkopplungsquelle.");
        if (Source.StartsWith('/') && !Source.StartsWith("/avatar/parameters/")) throw new ArgumentException("Avatar-Eingang muss unter /avatar/parameters/ liegen.");
        if (!(Target.StartsWith("/avatar/parameters/") || Target is "haptics/input/vibrate" or "haptics/input/pump" or "windforce/input/mapped")) throw new ArgumentException("Ungültiges Ausgabeziel.");
        if (Type is not ("f" or "i" or "T" or "s")) throw new ArgumentException("Ausgabetyp muss Float, Int, Bool oder String sein.");
        if (Type == "s" && !Target.StartsWith('/')) throw new ArgumentException("Text kann nur an Avatar-Parameter ausgegeben werden.");
        foreach (var address in new[] { Source, Target }.Where(a => a.StartsWith('/')))
            if (address.EndsWith('/') || address.IndexOfAny(['*', '?', '[', ']', '{', '}', ',', '\0']) >= 0) throw new ArgumentException("Vollständige OSC-Adresse ohne Platzhalter eingeben.");
        if (!double.IsFinite(Scale) || !double.IsFinite(Offset) || !double.IsFinite(Min) || !double.IsFinite(Max) || Min > Max || Math.Abs(Min) > 1e9 || Math.Abs(Max) > 1e9) throw new ArgumentException("Ungültige Skalierung oder Grenzen.");
        if (Source.Any(char.IsWhiteSpace) || Target.Any(char.IsWhiteSpace) || Source == Target) throw new ArgumentException("Quelle/Ziel dürfen weder Leerzeichen enthalten noch identisch sein.");
    }
    public double Transform(double n) => Math.Clamp((Invert ? 1 - n : n) * Scale + Offset, Min, Max);
}

public sealed class OscRouting : IDisposable
{
    private readonly IDataBus bus;
    private readonly IDisposable subscription;
    private OscRoute[] routes = [];
    private readonly object gate = new();
    public string Avatar { get; private set; } = "";
    public OscRouting(IDataBus bus, IEnumerable<OscRoute> initial)
    { this.bus = bus; Replace(initial); subscription = bus.Subscribe("", Receive); }
    public void Replace(IEnumerable<OscRoute> values)
    {
        var copy = values.Take(257).ToArray(); ValidateRoutes(copy);
        lock (gate) routes = copy;
        bus.Publish("routing/reset", true, "routing");
    }
    public static void ValidateRoutes(IReadOnlyCollection<OscRoute> routes)
    {
        if (routes.Count > 256) throw new ArgumentException("Maximal 256 Zuordnungen.");
        foreach (var route in routes) route.Validate();
        if (routes.Select(r => r.Id).Distinct().Count() != routes.Count) throw new ArgumentException("Doppelte Zuordnungs-ID.");
        var enabled = routes.Where(r => r.Enabled).ToArray();
        // Do not allow avatar-output feedback chains, including aliases through scalar topics.
        if (enabled.Any(r => r.Target.StartsWith('/') && enabled.Any(other => other.Source == r.Target || other.Source == "osc/value" + r.Target)))
            throw new ArgumentException("Avatar-Ausgänge dürfen nicht zugleich Zuordnungs-Eingänge sein (Rückkopplung).");
    }
    public static double? Number(JsonElement value) => value.ValueKind switch
    { JsonValueKind.True => 1, JsonValueKind.False => 0, JsonValueKind.Number when value.TryGetDouble(out var n) && double.IsFinite(n) => n, _ => null };
    private void Receive(DataEvent e)
    {
        if (e.Source == "routing" || e.Topic.StartsWith("osc/sent", StringComparison.Ordinal)) return;
        if (e.Topic == "osc/avatar-id")
        {
            lock (gate) Avatar = e.Value.GetString() ?? "";
            bus.Publish("routing/reset", true, "routing"); return;
        }
        if (e.Topic == "osc/receive/avatar/change")
        {
            lock (gate) Avatar = e.Value.GetProperty("arguments")[0].GetString() ?? "";
            bus.Publish("routing/reset", true, "routing"); return;
        }
        string source = e.Topic; var value = e.Value;
        if (source.StartsWith("osc/receive/"))
        { source = e.Value.GetProperty("address").GetString()!; var args = value.GetProperty("arguments"); if (args.GetArrayLength() == 0) return; value = args[0]; }
        var number = Number(value);
        OscRoute[] active;
        lock (gate) active = routes.Where(r => r.Enabled && r.Source == source && (r.Avatar == "*" || r.Avatar == Avatar)).ToArray();
        foreach (var route in active)
        {
            lock (gate)
            {
            if (!routes.Contains(route) || (route.Avatar != "*" && route.Avatar != Avatar)) continue;
            if (route.Type == "s")
            {
                if (value.ValueKind == JsonValueKind.String) bus.Publish("osc/send", new { address = route.Target, arguments = new[] { value.GetString() }, type = "s" }, "routing");
                continue;
            }
            if (!number.HasValue) continue;
            double n = route.Transform(number.Value);
            if (!double.IsFinite(n)) continue;
            if (route.Target.StartsWith('/'))
                bus.Publish("osc/send", new { address = route.Target, arguments = new[] { n }, type = route.Type }, "routing");
            else bus.Publish(route.Target, n, "routing");
            }
        }
    }
    public void Dispose() { subscription.Dispose(); bus.Publish("routing/reset", true, "routing"); }
}
