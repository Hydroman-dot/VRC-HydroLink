using System.Text.Json;
using HydroLink.Sdk;
namespace HydroLink.Core;
public sealed class HydroHost : IAsyncDisposable
{
    private readonly Settings settings;
    private readonly Action<string> log;
    private readonly IProgramLauncher launcher;
    private readonly Func<string, IComponent>? factory;
    private readonly Dictionary<string, IComponent> running = [];
    private readonly HashSet<string> modules = [];
    private readonly SemaphoreSlim transition = new(1, 1);
    private readonly CancellationTokenSource stop = new();
    private IDisposable? heartOutput;
    private bool pluginsStarted;
    private (bool Manual, double Percent, bool Paused)? windControls;
    public DataBus Bus { get; } = new();
    public OscRouting Routing { get; }
    public OscBridge? Osc => running.GetValueOrDefault("osc") as OscBridge;
    public WindOutput? Wind => running.GetValueOrDefault("windforce") as WindOutput;
    public HapticsService? Haptics => running.GetValueOrDefault("intiface") as HapticsService;
    public PluginLoader? Plugins { get; private set; }
    public IReadOnlyList<string> Running => running.Keys.ToArray();
    public IReadOnlySet<string> ActiveModules => modules;
    public HydroHost(Settings settings, Action<string> log, IProgramLauncher? launcher = null, Func<string, IComponent>? factory = null)
    {
        this.settings = settings; this.log = log; this.launcher = launcher ?? new ProgramLauncher(log); this.factory = factory;
        Routing = new(Bus, settings.OscRoutes);
        Bus.SubscriberError += ex => log("Bus subscriber: " + ex.GetType().Name);
    }
    public static IReadOnlyList<string> RequiredSources(Settings s)
    {
        var result = new HashSet<string>();
        if (s.OscHub || s.Haptics || s.WindForce || (s.HeartRate && (s.SendHeartToOsc || s.Heartrate.VrcOscParameters))) result.Add("osc");
        void Add(string id) { if (id != "None") result.Add(id); }
        if (s.HeartRate) Add(s.HeartSource);
        if (s.HeartRate && s.Heartrate.VrcOscParameters) result.Add("heartrate-output");
        if (s.Fitness) { Add(s.MovementSource); Add(s.HeartSource); if (s.Companion) result.Add("companion"); if (s.HomeAssistant.Enabled) result.Add("home-assistant"); }
        if (s.WindForce) { Add(s.WindMovementSource); Add(s.WindHeartSource); result.Add("windforce"); }
        if (s.Haptics) result.Add("intiface");
        return result.ToArray();
    }
    public IReadOnlyList<(string Id, string State, string Detail)> ServiceDiagnostics => running.Select(pair =>
        pair.Value is Worker worker ? (pair.Key, worker.Health.State, worker.Health.Detail)
        : (pair.Key, "Status unbekannt", "Plugin meldet keinen Dienstzustand")).ToArray();
    public string[] ModuleSources(string id) => id switch
    {
        "fitness" => new[] { settings.MovementSource, settings.HeartSource }.Concat(settings.Companion ? new[] { "companion" } : []).Concat(settings.HomeAssistant.Enabled ? new[] { "home-assistant" } : []).Where(x => x != "None").Distinct().ToArray(),
        "heartrate" => new[] { settings.HeartSource }.Concat(settings.SendHeartToOsc || settings.Heartrate.VrcOscParameters ? new[] { "osc" } : []).Concat(settings.Heartrate.VrcOscParameters ? new[] { "heartrate-output" } : []).Where(x => x != "None").Distinct().ToArray(),
        "windforce" => new[] { "windforce", "osc", settings.WindMovementSource, settings.WindHeartSource }.Where(x => x != "None").Distinct().ToArray(),
        "haptics" => ["intiface", "osc"], "osc" => ["osc"], _ => []
    };
    public async Task StartAsync()
    {
        foreach (var (id, enabled) in new[] { ("fitness", settings.Fitness), ("heartrate", settings.HeartRate), ("windforce", settings.WindForce), ("osc", settings.OscHub), ("haptics", settings.Haptics) })
            if (enabled) await SetModuleAsync(id, true);
        await StartPluginsAsync();
    }
    public async Task StartPluginsAsync()
    {
        if (pluginsStarted) return;
        pluginsStarted = true; Plugins = new(Bus, log);
        Plugins.Load(Path.Combine(AppContext.BaseDirectory, "plugins"), settings.EnabledPlugins);
        foreach (var component in Plugins.Components)
        {
            if (running.ContainsKey(component.Id)) { log("Plugin-ID bereits belegt: " + component.Id); continue; }
            try { await component.StartAsync(stop.Token); running.Add(component.Id, component); }
            catch (Exception ex) { await component.DisposeAsync(); log("Plugin-Start: " + ex.Message); }
        }
    }
    public async Task SetModuleAsync(string id, bool enabled)
    {
        if (id is not ("fitness" or "heartrate" or "windforce" or "osc" or "haptics")) throw new ArgumentException("Unknown module");
        await transition.WaitAsync();
        try { await SetModuleCoreAsync(id, enabled); }
        finally { transition.Release(); }
    }
    private async Task SetModuleCoreAsync(string id, bool enabled)
    {
            var desired = new HashSet<string>(modules);
            if (enabled) desired.Add(id); else desired.Remove(id);
            var selection = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!;
            selection.Fitness = desired.Contains("fitness"); selection.HeartRate = desired.Contains("heartrate"); selection.WindForce = desired.Contains("windforce"); selection.OscHub = desired.Contains("osc"); selection.Haptics = desired.Contains("haptics");
            var required = RequiredSources(selection).ToHashSet();
            var additions = new List<IComponent>();
            try
            {
                foreach (var source in required.Where(s => !running.ContainsKey(s)))
                {
                    var component = factory?.Invoke(source) ?? Create(source); additions.Add(component);
                    foreach (var program in settings.Programs.Where(p => p.AutoStart && p.Provider == source))
                        try { launcher.Start(program); } catch (Exception ex) { log("Programmstart: " + ex.Message); }
                    await component.StartAsync(stop.Token);
                }
            }
            catch { foreach (var component in additions.AsEnumerable().Reverse()) await component.DisposeAsync(); throw; }
            foreach (var component in additions) running.Add(component.Id, component);
            if (!selection.HeartRate) { heartOutput?.Dispose(); heartOutput = null; }
            foreach (var key in running.Keys.Where(k => !required.Contains(k) && !(Plugins?.Components.Any(p => p.Id == k) ?? false)).Reverse().ToArray())
            {
                try { await running[key].DisposeAsync(); } catch (Exception ex) { log(key + " Stop: " + ex.Message); }
                running.Remove(key);
            }
            if (selection.HeartRate && settings.SendHeartToOsc && !settings.Heartrate.VrcOscParameters && heartOutput is null)
                heartOutput = Bus.Subscribe("fitness/heart/bpm", e => Bus.Publish("osc/send", new { address = settings.HeartOscAddress, arguments = new[] { (int)e.Value.GetDouble() } }, "fitness"));
            modules.Clear(); modules.UnionWith(desired);
            log(id + (enabled ? " gestartet" : " gestoppt"));
    }
    public async Task RestartModuleAsync(string id)
    {
        if (id is not ("fitness" or "heartrate" or "windforce" or "osc" or "haptics")) throw new ArgumentException("Unknown module");
        await transition.WaitAsync();
        try
        {
            if (id == "osc" && running.TryGetValue("osc", out var previous))
            {
                Bus.Publish("routing/reset", true, "restart");
                await previous.DisposeAsync(); running.Remove("osc");
                var replacement = factory?.Invoke("osc") ?? Create("osc");
                try { await replacement.StartAsync(stop.Token); running.Add("osc", replacement); }
                catch { await replacement.DisposeAsync(); throw; }
                log("OSC neu gestartet; Haptics erneut aktivieren.");
            }
            else
            {
                await SetModuleCoreAsync(id, false);
                await SetModuleCoreAsync(id, true);
                log(id + " neu gestartet; gemeinsam genutzte Quellen bleiben aktiv.");
            }
        }
        finally { transition.Release(); }
    }
    public async Task ConfigureWindAsync(WindConfiguration configuration)
    {
        configuration.Validate();
        await transition.WaitAsync();
        try
        {
            var previous = WindConfiguration.From(settings);
            windControls = Wind?.ControlState;
            bool active = modules.Contains("windforce");
            if (active) await SetModuleCoreAsync("windforce", false);
            try
            {
                configuration.Apply(settings);
                if (active) await SetModuleCoreAsync("windforce", true);
            }
            catch
            {
                previous.Apply(settings);
                if (active) await SetModuleCoreAsync("windforce", true);
                throw;
            }
        }
        finally { windControls = null; transition.Release(); }
    }
    private WindOutput CreateWind()
    {
        var wind = new WindOutput(settings, Bus, log);
        if (windControls is { } controls) { wind.SetManual(controls.Manual, controls.Percent); wind.Pause(controls.Paused); }
        return wind;
    }
    private IComponent Create(string id) => id switch
    {
        "heartrate-output" => new HeartrateOutput(settings, Bus, log), "home-assistant" => new HomeAssistantService(settings, Bus, log), "osc" => new OscBridge(settings, Bus, log), "windforce" => CreateWind(), "intiface" => new HapticsService(settings, Bus, log), "companion" => new CompanionSource(settings, Bus, log),
        "VRTI" => Socket(id, settings.VrtiUrl), "FitOSC" => Socket(id, settings.FitOscUrl), "VRCOSC" => Socket(id, settings.HeartUrl),
        "Pulsoid" when !string.IsNullOrWhiteSpace(settings.PulsoidToken) => Socket(id, "wss://dev.pulsoid.net/api/v1/data/real_time?access_token=" + Uri.EscapeDataString(settings.PulsoidToken)),
        "HypeRate" when !string.IsNullOrWhiteSpace(settings.HypeRateKey) && !string.IsNullOrWhiteSpace(settings.HypeRateId) => Socket(id, "wss://app.hyperate.io/socket/websocket?token=" + Uri.EscapeDataString(settings.HypeRateKey)),
        _ => throw new InvalidOperationException(id + ": Konfiguration fehlt oder Quelle unbekannt")
    };
    private WebSocketSource Socket(string id, string uri) => new(id, new Uri(uri), settings, Bus, log);
    public async ValueTask DisposeAsync()
    {
        await transition.WaitAsync();
        try
        {
            Routing.Dispose(); await stop.CancelAsync(); heartOutput?.Dispose(); heartOutput = null;
            foreach (var component in running.Values.Reverse()) try { await component.DisposeAsync(); } catch (Exception ex) { log(component.Id + " Stop: " + ex.GetType().Name); }
            running.Clear(); modules.Clear();
        }
        finally { transition.Release(); }
    }
}
