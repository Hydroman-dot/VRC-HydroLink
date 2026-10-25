using Buttplug.Core.Messages;
namespace VrcHapticsBridge.Core;

public enum ToyKind { Max2, Ridge, SolacePro, Dolce, Gemini, Nora, Gravity, LushMini }
public sealed record ToyDevice(uint Id, string Name)
{
    public override string ToString() => Name;
}

/// <summary>Independent toy sessions. Discovery learns socket addresses from fresh OSC, never query values.</summary>
public sealed class ToyHaptics : IAsyncDisposable
{
    private readonly IUniversalHapticsBackend backend;
    private readonly Dictionary<ToyKind, UniversalHaptics> engines;
    private readonly Dictionary<ToyKind, uint> assigned = [];
    private readonly SemaphoreSlim gate = new(1, 1);
    private long epoch;
    public event Action? SpsContactsRequested;
    public ToyHaptics(IUniversalHapticsBackend backend)
    {
        this.backend = backend;
        engines = Enum.GetValues<ToyKind>().ToDictionary(k => k, _ => new UniversalHaptics(backend));
    }
    public UniversalHaptics this[ToyKind toy] => engines[toy];
    public static bool Matches(ToyKind toy, string name)
    {
        var words = System.Text.RegularExpressions.Regex.Replace(name.ToLowerInvariant(), "[^a-z0-9]+", " ").Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (ToyProfiles.Extended(toy)) return toy switch {
            ToyKind.Dolce => words.Contains("dolce") || words.Contains("quake"),
            ToyKind.LushMini => words.Contains("lushmini") || words.Contains("lush") && words.Contains("mini"),
            _ => words.Contains(toy.ToString().ToLowerInvariant()) };
        return toy == ToyKind.SolacePro ? words.Contains("solace") && words.Contains("pro") : toy == ToyKind.Ridge ? words.Contains("ridge") : words.Contains("max") || words.Contains("max2");
    }
    public static bool Compatible(ToyKind toy, HapticFeature feature) => feature.Supported &&
        (ToyProfiles.Extended(toy) ? ToyProfiles.Channels(toy).Any(c => c.Type == feature.Type) : toy == ToyKind.SolacePro ? feature.Type == OutputType.Oscillate : feature.Type == OutputType.Vibrate || feature.Type == (toy == ToyKind.Max2 ? OutputType.Constrict : OutputType.Rotate));
    public ToyDevice[] Devices(ToyKind toy) => backend.Features.Where(f => Compatible(toy, f)).GroupBy(f => f.DeviceId)
        .Select(g => new ToyDevice(g.Key, g.First().DeviceName)).OrderBy(d => d.Name).ToArray();
    public void StartWorkers() { foreach (var engine in engines.Values) engine.Start(); }
    public void Receive(IReadOnlyList<OscValue> values)
    {
        if (values.Any(v => v.Address == "/avatar/change")) Invalidate();
        if (values.Any(v => v.Address == "/avatar/parameters/OGB_ENABLED" && v.Value < .5))
        {
            Interlocked.Increment(ref epoch);
            foreach (var engine in engines.Values.Where(e => !e.ManualToy)) engine.Invalidate("SPS ausgeschaltet");
        }
        foreach (var toy in Enum.GetValues<ToyKind>())
        {
            var prefix = "/avatar/parameters/HydroLink/" + toy + "/";
            var menu = values.Where(v => v.Address.StartsWith(prefix, StringComparison.Ordinal)).ToArray();
            if (menu.Any(v => v.Address == prefix + "Stop" && v.Value >= .5))
            {
                Interlocked.Increment(ref epoch);
                engines[toy].Invalidate("Im Avatar-Menue gestoppt");
                continue;
            }
            if (menu.Any(v => v.Address == prefix + "Enabled" && v.Value < .5))
            {
                engines[toy].SetSpsChannel("Enabled", 0);
                continue;
            }
            foreach (var value in menu)
            {
                var channel = value.Address[prefix.Length..];
                if (value.Value is not { } number || !double.IsFinite(number)) continue;
                if (ToyProfiles.Extended(toy) ? ToyProfiles.Accepts(toy, channel) : channel is "Enabled" or "Manual" || toy != ToyKind.SolacePro && channel is "Vibration" or "ManualVibration" || toy == ToyKind.SolacePro && channel is "Motion" or "ManualMotion" or "StrokeStart" or "StrokeEnd" or "Position" or "TravelTime" or "PositionMode" or "ManualStrokeStart" or "ManualStrokeEnd" or "ManualPosition" or "ManualTravelTime" or "ManualPositionMode" || toy == ToyKind.Max2 && channel is "Pump" or "ManualPump" || toy == ToyKind.Ridge && channel is "Rotation" or "Clockwise" or "ManualRotation" or "ManualClockwise")
                    engines[toy].SetSpsChannel(channel, number);
            }
        }
        foreach (var engine in engines.Values) engine.Receive(values);
    }
    public void Invalidate()
    {
        Interlocked.Increment(ref epoch);
        foreach (var engine in engines.Values) engine.Invalidate();
    }
    public void ForgetDevices()
    {
        Interlocked.Increment(ref epoch);
        foreach (var engine in engines.Values) engine.ForgetDevices();
    }
    public async Task ActivateAsync(ToyKind toy, uint id, double vibration, double secondary, bool clockwise)
    {
        var ticket = Interlocked.Read(ref epoch);
        await gate.WaitAsync();
        try
        {
            if (ticket != Interlocked.Read(ref epoch)) return;
            if (assigned.Any(kv => kv.Key != toy && kv.Value == id && engines[kv.Key].Snapshot.Armed))
                throw new InvalidOperationException("Dieses Gerät ist bereits im anderen Toy-Reiter aktiv.");
            var features = backend.Features.Where(f => f.DeviceId == id && Compatible(toy, f)).ToArray();
            if (ToyProfiles.Extended(toy)) { var mapping = ToyProfiles.Bind(toy, features); features = features.Where(f => mapping.ContainsKey(f.Key)).ToArray(); }
            if (!backend.Connected || features.Length == 0) throw new InvalidOperationException("Toy noch nicht verbunden. Intiface starten und Geräte suchen lassen.");
            await engines[toy].ConfigureAsync(features.Select(f => new FeatureRoute(f, 1)), UniversalSourceMode.Sps, [], allSps: true, toyGate: true, solace: toy == ToyKind.SolacePro, featureChannels: ToyProfiles.Bind(toy, features));
            if (ticket != Interlocked.Read(ref epoch)) return;
            await engines[toy].ArmAsync();
            if (ticket != Interlocked.Read(ref epoch)) { await engines[toy].StopAsync(); return; }
            assigned[toy] = id;
            engines[toy].SetSpsLevels(vibration, secondary, clockwise);
            // VRCFury keeps its OSC contact receivers inactive until the receiving app opts in.
            // Request only after an explicit, successful activation; never from query snapshots.
            SpsContactsRequested?.Invoke();
        }
        finally { gate.Release(); }
    }
    public Task StopAsync(ToyKind toy)
    {
        Interlocked.Increment(ref epoch);
        return engines[toy].StopAsync();
    }
    public async Task StopAllAsync()
    {
        Invalidate();
        var errors = new List<Exception>();
        foreach (var engine in engines.Values) try { await engine.StopAsync(); } catch (Exception ex) { errors.Add(ex); }
        if (errors.Count > 0) throw new AggregateException(errors);
    }
    public async ValueTask DisposeAsync()
    {
        Invalidate();
        try { await Task.WhenAll(engines.Values.Select(e => e.DisposeAsync().AsTask())); }
        finally { gate.Dispose(); }
    }
}
