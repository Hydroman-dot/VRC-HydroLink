using Buttplug.Core.Messages;
namespace VrcHapticsBridge.Core;

public enum UniversalSourceMode { Menu, Sps, Legacy }
public sealed record FeatureRoute(HapticFeature Feature, double Limit = .25);
public sealed record UniversalSnapshot(bool Armed, bool AvatarEnabled, double Strength, int Profile,
    int SelectedFeatures, double SourceLevel, string Status, IReadOnlyDictionary<string, double> Sent);

/// <summary>Single serialized writer for explicitly selected device features. Session-only device IDs.</summary>
public sealed class UniversalHaptics(IUniversalHapticsBackend backend, TimeProvider? clock = null) : IAsyncDisposable
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private readonly object sync = new();
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly SpsInput sps = new();
    private FeatureRoute[] routes = [];
    private SpsSource[] sources = [];
    private readonly Dictionary<string, double> controls = new();
    private readonly Dictionary<string, double> sent = new();
    private readonly Dictionary<string, bool> directions = new();
    private readonly Dictionary<uint, DateTimeOffset> nextSend = new();
    private readonly HashSet<uint> pendingStops = [];
    private UniversalSourceMode sourceMode;
    private bool armed, avatarEnabled, clockwise = true;
    private int profile = 3, cursor;
    private double sourceLevel;
    private string status = "Aus";
    private long generation;
    private DateTimeOffset lastSource;
    private int idleSeconds;
    private bool automaticSps;
    private bool manualToy, requireToyEnable, manualClockwise = true;
    public bool ManualToy { get { lock (sync) return manualToy; } }
    public int SourceCount { get { lock (sync) return sources.Length; } }
    private Task? worker;
    private const string P = "/avatar/parameters/HydroLink/";
    public event Action<string>? Log;
    public UniversalSnapshot Snapshot { get { lock (sync) return new(armed, avatarEnabled, controls.GetValueOrDefault("Strength"), profile,
        routes.Length, sourceLevel, status, new Dictionary<string, double>(sent)); } }
    public void Start() => worker ??= Loop();
    private void Disarm(string reason)
    {
        generation++; armed = false; avatarEnabled = false; status = reason;
        foreach (var r in routes) pendingStops.Add(r.Feature.DeviceId);
        controls.Clear(); sps.Clear(); sourceLevel = 0; clockwise = true; profile = 3; lastSource = default;
        if (automaticSps) sources = [];
        manualToy = false; manualClockwise = true;
    }
    public void Invalidate(string reason = "Verbindung oder Avatar geändert")
    { lock (sync) { Disarm(reason); } }
    public void ForgetDevices()
    { lock (sync) { Disarm("Geräte neu auswählen"); routes = []; } }

    public async Task ConfigureAsync(IEnumerable<FeatureRoute> selected, UniversalSourceMode mode, IEnumerable<SpsSource> selectedSources, int idleTimeout = 0, bool allSps = false, bool toyGate = false)
    {
        var list = selected.ToArray(); var inputs = selectedSources.ToArray();
        if (!Enum.IsDefined(mode) || idleTimeout is < 0 or > 600) throw new ArgumentException("Ungültiger Eingangsmodus.");
        if (list.Length > 64 || list.Select(r => r.Feature.Key).Distinct().Count() != list.Length)
            throw new ArgumentException("Zu viele oder doppelte Ausgänge.");
        if (list.Any(r => !r.Feature.Supported || !double.IsFinite(r.Limit) || r.Limit is < 0 or > 1))
            throw new ArgumentException("Nicht unterstützte Funktion oder ungültige Grenze.");
        if (list.GroupBy(r => (r.Feature.DeviceId, r.Feature.FeatureId)).Any(g => g.Count() > 1))
            throw new ArgumentException("Pro physischer Funktion nur einen Ausgabetyp auswählen.");
        foreach (var source in inputs) source.Validate();
        if (allSps && mode != UniversalSourceMode.Sps) throw new ArgumentException("SPS-Automatik benötigt SPS.");
        if (mode == UniversalSourceMode.Sps && !allSps && (inputs.Length == 0 || inputs.Length > 32))
            throw new ArgumentException("Mindestens eine verifizierte SPS-Quelle auswählen (maximal 32).");
        await gate.WaitAsync();
        try
        {
            lock (sync) Disarm("Auswahl geändert");
            await FlushStops();
            lock (sync) { routes = list; sources = inputs; sourceMode = mode; idleSeconds = idleTimeout; automaticSps = allSps; requireToyEnable = toyGate; }
        }
        finally { gate.Release(); }
    }
    public async Task ArmAsync()
    {
        await gate.WaitAsync();
        try
        {
            long ticket;
            lock (sync) { Disarm("Freigabe wird vorbereitet"); ticket = generation; }
            await FlushStops();
            lock (sync)
            {
                if (ticket != generation) return;
                if (!backend.Connected || routes.Length == 0 || routes.Any(r => !backend.Features.Contains(r.Feature)))
                    throw new InvalidOperationException("Zuerst verfügbare Gerätefunktionen übernehmen.");
                armed = true; status = "Freigegeben; neue Avatar-Werte erforderlich";
                if (automaticSps) { avatarEnabled = !requireToyEnable; controls["ManualMotion"] = controls["ManualVibration"] = controls["ManualPump"] = controls["ManualRotation"] = .25; status = "Freigegeben · Haptik im Radialmenü einschalten"; }
            }
        }
        finally { gate.Release(); }
    }
    public async Task StopAsync()
    {
        // Invalidate before waiting for an in-flight command.
        lock (sync) Disarm("Gestoppt");
        await gate.WaitAsync();
        try { await FlushStops(); } finally { gate.Release(); }
    }
    public void Receive(IReadOnlyList<OscValue> messages)
    {
        lock (sync)
        {
            if (messages.Any(m => m.Address == "/avatar/change")) { Disarm("Avatar gewechselt"); return; }
            if (!armed) return; // Never replay values seen before the desktop release.
            foreach (var m in messages)
            {
                if (m.Value is not { } value || !double.IsFinite(value)) continue;
                if (automaticSps && m.Address.StartsWith(P, StringComparison.Ordinal)) continue;
                if (automaticSps && SpsSource.FromLiveAddress(m.Address) is { } discovered && !sources.Any(s => s.Prefix == discovered.Prefix) && sources.Length < 512)
                    sources = sources.Append(discovered).ToArray();
                if (m.Address.StartsWith(P, StringComparison.Ordinal))
                {
                    var key = m.Address[P.Length..];
                    if (key == "Enabled")
                    {
                        if (value < .5) { Disarm("Avatar-Freigabe aus"); return; }
                        avatarEnabled = true;
                    }
                    else if (key == "Clockwise") clockwise = value >= .5;
                    else if (key == "Mode")
                    {
                        if (value != Math.Truncate(value) || value is < 1 or > 3) { Disarm("Kein gültiges Profil"); return; }
                        profile = (int)value;
                    }
                    else if (key is "Strength" or "Vibration" or "Pump" or "Rotation" or "Motion")
                        controls[key] = Math.Clamp(value, 0, 1);
                }
                else if (m.Address == "/avatar/parameters/OGB_ENABLED" && value < .5 && sourceMode == UniversalSourceMode.Sps && !manualToy)
                { Disarm("SPS-Haptics ausgeschaltet"); return; }
                else if (sourceMode == UniversalSourceMode.Legacy && m.Address is "/avatar/parameters/MaxVibrate" or "/avatar/parameters/MaxPump")
                { controls[m.Address.EndsWith("MaxPump") ? "LegacyPump" : "LegacyVibration"] = Math.Clamp(value, 0, 1); lastSource = time.GetUtcNow(); }
                else if (sourceMode == UniversalSourceMode.Sps && sources.Any(s => m.Address.StartsWith(s.Prefix, StringComparison.Ordinal)))
                { sps.Receive(m.Address, value); lastSource = time.GetUtcNow(); }
            }
            sourceLevel = sourceMode == UniversalSourceMode.Sps ? sources.Select(sps.Level).DefaultIfEmpty().Max() : 0;
        }
    }
    public void SetSpsLevels(double vibration, double secondary, bool direction = true)
    {
        if (!double.IsFinite(vibration) || !double.IsFinite(secondary) || vibration is < 0 or > 1 || secondary is < 0 or > 1)
            throw new ArgumentException("Stärke muss zwischen 0 und 1 liegen.");
        lock (sync)
        {
            if (!automaticSps || !armed) return;
            controls["Strength"] = 1; controls["Motion"] = controls["Vibration"] = vibration;
            controls["Pump"] = secondary; controls["Rotation"] = secondary; clockwise = direction;
        }
    }
    public (double Vibration, double Secondary, bool Clockwise) SpsLevels
    {
        get { lock (sync) return (controls.GetValueOrDefault("Vibration"), controls.GetValueOrDefault("Pump"), clockwise); }
    }
    public (double Vibration, double Secondary, bool Clockwise) ActiveToyLevels
    { get { lock (sync) return manualToy ? (controls.GetValueOrDefault("ManualVibration"), controls.GetValueOrDefault("ManualPump"), manualClockwise) : SpsLevels; } }
    public void SetActiveToyLevels(double vibration, double secondary, bool direction)
    {
        lock (sync)
        {
            if (manualToy) { SetSpsChannel("ManualVibration", vibration); SetSpsChannel("ManualPump", secondary); SetSpsChannel("ManualClockwise", direction ? 1 : 0); }
            else SetSpsLevels(vibration, secondary, direction);
        }
    }
    public void SetSpsChannel(string channel, double value)
    {
        if (!double.IsFinite(value)) return;
        lock (sync)
        {
            if (!automaticSps || !armed) return;
            if (channel == "Enabled")
            {
                var next = value >= .5;
                if (avatarEnabled != next) { generation++; avatarEnabled = next; manualToy = false; sps.Clear(); sourceLevel = 0; lastSource = default;
                    if (!next) foreach (var r in routes) pendingStops.Add(r.Feature.DeviceId);
                }
            }
            else if (channel == "Manual")
            {
                var next = value >= .5 && avatarEnabled;
                if (manualToy != next) { manualToy = next; sps.Clear(); sourceLevel = 0; lastSource = default; }
            }
            else if (channel == "ManualClockwise") manualClockwise = value >= .5;
            else if (channel is "ManualVibration" or "ManualMotion") controls["ManualMotion"] = controls["ManualVibration"] = Math.Clamp(value, 0, 1);
            else if (channel is "ManualPump" or "ManualRotation") controls["ManualPump"] = controls["ManualRotation"] = Math.Clamp(value, 0, 1);
            else if (channel == "Clockwise") clockwise = value >= .5;
            else if (channel is "Vibration" or "Motion") controls["Motion"] = controls["Vibration"] = Math.Clamp(value, 0, 1);
            else if (channel is "Pump" or "Rotation") controls["Pump"] = controls["Rotation"] = Math.Clamp(value, 0, 1);
        }
    }
    private double Desired(FeatureRoute route)
    {
        if (!avatarEnabled) return 0;
        var t = route.Feature.Type;
        // Profiles filter capabilities, never guess a model name.
        if (profile == 1 && t is not (OutputType.Vibrate or OutputType.Constrict)) return 0;
        if (profile == 2 && t is not (OutputType.Vibrate or OutputType.Rotate)) return 0;
        if (t == OutputType.Rotate && !(manualToy ? manualClockwise : clockwise) && !route.Feature.Reversible) return 0;
        var channel = t switch { OutputType.Vibrate => "Vibration", OutputType.Constrict => "Pump", OutputType.Rotate => "Rotation", _ => "Motion" };
        var signal = automaticSps && manualToy ? 1 : sourceMode switch {
            UniversalSourceMode.Menu => 1,
            UniversalSourceMode.Sps => sourceLevel,
            _ => controls.GetValueOrDefault(t == OutputType.Constrict ? "LegacyPump" : "LegacyVibration")
        };
        if (!manualToy && sourceMode != UniversalSourceMode.Menu && idleSeconds > 0 && time.GetUtcNow() - lastSource >= TimeSpan.FromSeconds(idleSeconds)) signal = 0;
        return signal * controls.GetValueOrDefault("Strength") * controls.GetValueOrDefault(manualToy ? "Manual" + channel : channel) * route.Limit;
    }
    private async Task FlushStops()
    {
        uint[] ids;
        lock (sync) ids = pendingStops.ToArray();
        var errors = new List<Exception>();
        foreach (var id in ids)
        {
            try
            {
                if (backend.Connected) { using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2)); await backend.StopDeviceAsync(id, timeout.Token); }
                lock (sync) pendingStops.Remove(id);
            }
            catch (Exception e) { errors.Add(e); }
        }
        lock (sync) { sent.Clear(); directions.Clear(); nextSend.Clear(); }
        if (errors.Count > 0) throw new AggregateException("Stop nicht für alle Geräte bestätigt.", errors);
    }
    public async Task TickAsync()
    {
        await gate.WaitAsync();
        try
        {
            bool stop;
            lock (sync) stop = pendingStops.Count > 0;
            if (stop) { await FlushStops(); return; }
            FeatureRoute[] batch;
            lock (sync) batch = routes;
            for (var n = 0; n < batch.Length; n++)
            {
                FeatureRoute route; double value; bool direction; long ticket;
                lock (sync)
                {
                    if (!armed) return;
                    if (!backend.Connected || batch.Any(r => !backend.Features.Contains(r.Feature)))
                    { Disarm("Gerätefunktionen geändert"); break; }
                    route = batch[(cursor + n) % batch.Length];
                    if (nextSend.GetValueOrDefault(route.Feature.DeviceId) > time.GetUtcNow()) continue;
                    value = Desired(route); direction = manualToy ? manualClockwise : clockwise; ticket = generation;
                    if (sent.TryGetValue(route.Feature.Key, out var old) && old == value &&
                        (route.Feature.Type != OutputType.Rotate || directions.GetValueOrDefault(route.Feature.Key, true) == direction)) continue;
                    // No initial zero command: position zero is a movement, not a stop.
                    if (!sent.ContainsKey(route.Feature.Key) && value == 0) continue;
                }
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                // A direction unavailable on hardware produces a zero in the supported direction.
                await backend.SetFeatureAsync(route.Feature, value, route.Feature.Reversible ? direction : true, timeout.Token);
                lock (sync)
                {
                    if (ticket != generation) break;
                    sent[route.Feature.Key] = value; directions[route.Feature.Key] = direction;
                    nextSend[route.Feature.DeviceId] = time.GetUtcNow().AddMilliseconds(Math.Max(50, route.Feature.TimingGap));
                }
            }
            lock (sync) { cursor = batch.Length == 0 ? 0 : (cursor + 1) % batch.Length; stop = pendingStops.Count > 0; }
            if (stop) await FlushStops();
        }
        catch (Exception e)
        {
            lock (sync) Disarm("Ausgabefehler: " + e.Message);
            Log?.Invoke(e.Message);
            try { await FlushStops(); } catch (Exception stopError) { Log?.Invoke(stopError.Message); }
        }
        finally { gate.Release(); }
    }
    private async Task Loop()
    {
        try { using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50), time);
            while (await timer.WaitForNextTickAsync(lifetime.Token)) await TickAsync(); }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }
    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        if (worker != null) await worker;
        try { await StopAsync(); } finally { lifetime.Dispose(); gate.Dispose(); }
    }
}
