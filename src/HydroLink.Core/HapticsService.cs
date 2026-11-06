using System.Text.Json;
using HydroLink.Sdk;
using VrcHapticsBridge.Core;
namespace HydroLink.Core;

public sealed class HapticsService : Worker
{
    private readonly Settings settings;
    private readonly IDataBus bus;
    public ButtplugBackend Backend { get; } = new();
    public BridgeController Controller { get; }
    public UniversalHaptics Universal { get; }
    public ToyHaptics Toys { get; }
    private volatile bool toyActive;
    private long toyActivationEpoch;
    private readonly object gateSync = new();
    private readonly AvatarGateTracker gateTracker = new();
    private readonly Dictionary<ToyKind, string> gateStatus = new();
    public string GateStatus(ToyKind toy) { lock(gateSync) return gateStatus.GetValueOrDefault(toy, "Noch kein Freigabewert empfangen"); }
    private volatile bool universalActive;
    private volatile WizardSyncState wizardState = new(false, null, "missing", "");
    private volatile string currentAvatar = "";
    public void ApplyWizardSync(WizardSyncState state, string avatar)
    {
        var changed = wizardState.Fingerprint != state.Fingerprint || currentAvatar != avatar;
        wizardState = state; currentAvatar = avatar;
        if (changed) { Interlocked.Increment(ref toyActivationEpoch); Toys.Invalidate(); }
    }
    public bool WizardAllows(ToyKind toy) => !wizardState.Present || wizardState.Profile is { } p && p.MatchesAvatar(currentAvatar) && p.Includes(toy);
    public HapticsService(Settings settings, IDataBus bus, Action<string> log) : base("intiface", ComponentKind.Service, log)
    {
        this.settings = settings; this.bus = bus;
        Controller = new(Backend); Controller.Configure(settings.HapticsOptions);
        Controller.Log += log; Backend.Log += log; Backend.ConnectionLost += Controller.Invalidate;
        Universal = new(Backend); Universal.Log += log;
        Toys = new(Backend);
        Toys.SpsContactsRequested += () => bus.Publish("osc/send", new { address = "/avatar/parameters/OGB_ENABLED", arguments = new[] { true }, type = "T" }, Id);
        Backend.ConnectionLost += () => { Interlocked.Increment(ref toyActivationEpoch); Toys.ForgetDevices(); };
        Backend.DeviceLost += _ => { Interlocked.Increment(ref toyActivationEpoch); Toys.ForgetDevices(); };
        Backend.ConnectionLost += Universal.ForgetDevices;
        Backend.DeviceLost += _ => Universal.ForgetDevices();
    }
    public async Task StopDevicesAsync() { Interlocked.Increment(ref toyActivationEpoch); try { await Toys.StopAllAsync(); } finally { try { await Universal.StopAsync(); } finally { await Controller.StopAsync(); } } }
    public Task StopToyAsync(ToyKind toy) { lock(gateSync) { gateTracker.Remove(toy.ToString()); gateStatus.Remove(toy); } Interlocked.Increment(ref toyActivationEpoch); return Toys.StopAsync(toy); }
    public async Task SelectAsync(uint? id) { await StopDevicesAsync(); universalActive = false; Backend.Select(id); }
    public async Task ArmLegacyAsync() { await Toys.StopAllAsync(); toyActive = false; await Universal.StopAsync(); universalActive = false; await Controller.ArmOscAsync(); }
    public async Task TestLegacyAsync(Channel channel, double value) { await Toys.StopAllAsync(); toyActive = false; await Universal.StopAsync(); universalActive = false; await Controller.TestAsync(channel, value); }
    public async Task ActivateToyAsync(ToyKind toy, uint id, double vibration, double secondary, bool clockwise, bool desktopManual = false)
    {
        if (!desktopManual && !WizardAllows(toy)) throw new InvalidOperationException("Unity-Profil und aktiver VRChat-Avatar müssen zum Toy passen.");
        var ticket = Interlocked.Read(ref toyActivationEpoch);
        await Universal.StopAsync(); await Controller.StopAsync();
        if (ticket != Interlocked.Read(ref toyActivationEpoch) || !desktopManual && !WizardAllows(toy)) return;
        toyActive = true;

        await Toys.ActivateAsync(toy, id, vibration, secondary, clockwise, requestContacts: !desktopManual);
        if (ticket != Interlocked.Read(ref toyActivationEpoch)) { await Toys.StopAsync(toy); return; }
        if (desktopManual) { SetDesktopManual(toy, true); return; }
        lock (gateSync)
            if (ticket == Interlocked.Read(ref toyActivationEpoch) && Toys[toy].Snapshot.Armed && currentAvatar.Length > 0)
            {
                gateTracker.Begin(toy.ToString(), currentAvatar, DateTimeOffset.UtcNow);
                gateStatus[toy] = "Warte auf frischen Live- oder OSCQuery-Freigabewert";
            }
    }
    public void SetDesktopManual(ToyKind toy, bool enabled)
    {
        lock (gateSync)
        {
            gateTracker.Remove(toy.ToString());
            if (!enabled && !WizardAllows(toy)) { Toys[toy].Invalidate("Manueller Gerätetest beendet"); return; }
            Toys[toy].SetDesktopManual(enabled);
            if (!enabled && Toys[toy].Snapshot.Armed && currentAvatar.Length > 0)
            {
                gateTracker.Begin(toy.ToString(), currentAvatar, DateTimeOffset.UtcNow);
                gateStatus[toy] = "Manueller Test beendet · prüfe Menü-Freigabe erneut";
            }
        }
    }
    public async Task ConfigureUniversalAsync(IEnumerable<FeatureRoute> routes, UniversalSourceMode mode, IEnumerable<SpsSource> sources)
    {
        await StopDevicesAsync(); toyActive = false; universalActive = true;
        await Universal.ConfigureAsync(routes, mode, sources, settings.HapticsOptions.IdleTimeoutSeconds);
    }
    public async Task ArmUniversalAsync() { await Toys.StopAllAsync(); toyActive = false; await Controller.StopAsync(); universalActive = true; await Universal.ArmAsync(); }
    protected override async Task Run(CancellationToken token)
    {
        Controller.Start();
        Universal.Start();
        Toys.StartWorkers();
        using var batteryStop = CancellationTokenSource.CreateLinkedTokenSource(token);
        var batteryTask = Backend.MonitorBatteriesAsync(batteryStop.Token);
        using var subscription = bus.Subscribe("", e =>
        {
            lock (gateSync)
            {
            if (e.Topic == "osc/avatar-gate")
            {
                var address = e.Value.GetProperty("address").GetString();
                foreach (var toy in Enum.GetValues<ToyKind>())
                    if (address == "/avatar/parameters/HydroLink/" + toy + "/Enabled" && Toys[toy].Snapshot.Armed && !Toys[toy].DesktopManual && WizardAllows(toy)
                        && currentAvatar == e.Value.GetProperty("avatar").GetString()
                        && gateTracker.Accept(toy.ToString(), currentAvatar, e.Value.GetProperty("queryStarted").GetDateTimeOffset(), e.Value.GetProperty("enabled").GetDouble() >= .5))
                    {
                        var enabled = e.Value.GetProperty("enabled").GetDouble();
                        Toys.Receive([new(address, enabled)]);
                        gateStatus[toy] = $"OSCQuery: {(enabled >= .5 ? "eingeschaltet" : "ausgeschaltet")} · {DateTimeOffset.Now:HH:mm:ss}";
                    }
            }
            else if (e.Topic.StartsWith("osc/receive/", StringComparison.Ordinal))
            {
                var address = e.Value.GetProperty("address").GetString()!;
                var args = e.Value.GetProperty("arguments");
                var values = new OscValue[] { new(address, args.GetArrayLength() > 0 ? OscRouting.Number(args[0]) : null) };
                if (address == "/avatar/change" || address == "/avatar/parameters/OGB_ENABLED" && values[0].Value < .5) Interlocked.Increment(ref toyActivationEpoch);
                foreach (var toy in Enum.GetValues<ToyKind>())
                    if (address == "/avatar/parameters/HydroLink/" + toy + "/Enabled" && values[0].Value is { } enabled)
                    {
                        gateTracker.Live(toy.ToString(), enabled >= .5, e.Timestamp);
                        gateStatus[toy] = $"Live-OSC: {(enabled >= .5 ? "eingeschaltet" : "ausgeschaltet")} · {e.Timestamp.ToLocalTime():HH:mm:ss}";
                    }
                if (Enum.GetValues<ToyKind>().Any(toy => address == "/avatar/parameters/HydroLink/" + toy + "/Stop") && values[0].Value >= .5)
                {
                    Interlocked.Increment(ref toyActivationEpoch);
                    Toys.Receive(values);
                    return;
                }
                if (toyActive) Toys.Receive(values); else if (universalActive) Universal.Receive(values); else Controller.Receive(values);
            }
            else if (e.Topic is "haptics/input/vibrate" or "haptics/input/pump")
            { if (!toyActive && !universalActive) Controller.Receive([new(e.Topic.EndsWith("vibrate") ? settings.HapticsOptions.VibrateAddress : settings.HapticsOptions.PumpAddress, OscRouting.Number(e.Value))]); }
            else if (e.Topic is "routing/reset" or "osc/avatar-id" or "osc/target-changed" or "osc/query-lost")
            {
                if (e.Topic == "osc/avatar-id") currentAvatar = e.Value.ValueKind == JsonValueKind.String ? e.Value.GetString() ?? "" : "";
                gateTracker.Clear(); gateStatus.Clear();
                Interlocked.Increment(ref toyActivationEpoch); Controller.Invalidate(); Universal.Invalidate();
                if (e.Topic is "osc/query-lost" or "osc/target-changed" or "routing/reset") Toys.InvalidateSps(); else Toys.Invalidate();
            }
            }
        });
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    await Backend.ConnectAsync(settings.IntifaceUrl, token);
                    await Backend.ScanAsync(token);
                    while (Backend.Connected && !token.IsCancellationRequested)
                    {
                        bus.Publish("haptics/devices", Backend.Devices, Id);
                        var snapshot = Controller.Snapshot;
                        bus.Publish("haptics/state", snapshot, Id);
                        bus.Publish("haptics/vibrate-sent", snapshot.VibrateSent, Id);
                        bus.Publish("haptics/pump-sent", snapshot.PumpSent, Id);
                        await Task.Delay(250, token);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception ex) { Log("Intiface: " + ex.Message); }
                finally { Controller.Invalidate(); Universal.ForgetDevices(); try { await StopDevicesAsync(); await Backend.DisconnectAsync(); } catch (Exception ex) { Log("Intiface Stop: " + ex.Message); } }
                await Task.Delay(3000, token);
            }
        }
        finally { await batteryStop.CancelAsync(); try { await batteryTask; } catch (OperationCanceledException) { }
            try { await Toys.DisposeAsync(); } finally { try { await Universal.DisposeAsync(); } finally { try { await Controller.DisposeAsync(); } finally { Backend.DisposeClient(); bus.Publish("haptics/devices", Array.Empty<DeviceInfo>(), Id); } } } }
    }
}
