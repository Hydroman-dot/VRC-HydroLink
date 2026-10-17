using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using Buttplug.Client;
using HydroLink.Sdk;

namespace HydroLink.Core;

public sealed class WindOutput(Settings settings, IDataBus bus, Action<string> log) : Worker("windforce", ComponentKind.Output, log)
{
    private readonly object gate = new();
    private readonly Dictionary<string, (double Value, DateTimeOffset Time)> inputs = [];
    private bool manual, paused;
    private bool inStation, autoWalkEnabled;
    private double vx, vy, vz;
    private DateTimeOffset magnitudeTime;
    private double manualPercent;
    public (bool Manual, double Percent, bool Paused) ControlState { get { lock (gate) return (manual, manualPercent, paused); } }
    private System.Net.IPEndPoint? target;
    public void Set(double value) { lock (gate) inputs["Direct OSC"] = (double.IsFinite(value) ? Math.Clamp(value, 0, 100) / 100 : 0, DateTimeOffset.UtcNow); }
    public void SetManual(bool enabled, double percent) { lock (gate) { manual = enabled; manualPercent = double.IsFinite(percent) ? Math.Clamp(percent, 0, 100) : 0; } }
    public void Pause(bool value) { lock (gate) paused = value; }
    public static string Command(double percent, uint sequence) => $"FAN {Math.Clamp(double.IsFinite(percent) ? percent : 0, 0, 100).ToString("0.0", CultureInfo.InvariantCulture)} {sequence}";
    protected override async Task Run(CancellationToken token)
    {
        using var udp = new UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Any, 0));
        if (!string.IsNullOrWhiteSpace(settings.WindHost))
        {
            var address = (await System.Net.Dns.GetHostAddressesAsync(settings.WindHost, token)).First(x => x.AddressFamily == AddressFamily.InterNetwork);
            target = new(address, settings.WindPort);
        }
        UdpClient? discovery = null;
        try { discovery = new UdpClient(new System.Net.IPEndPoint(System.Net.IPAddress.Any, 4211)); }
        catch (SocketException) { Log("WindForce: Discovery-Port belegt; feste IP und Befehlsantworten bleiben nutzbar"); }
        using var reception = CancellationTokenSource.CreateLinkedTokenSource(token);
        var receive = ReceiveStatus(udp, reception.Token);
        var discover = discovery is null ? Task.CompletedTask : ReceiveStatus(discovery, reception.Token);
        using var input = bus.Subscribe("", e =>
        {
            if (e.Topic == "routing/reset") { Set(0); return; }
            if (e.Topic == "sources/" + settings.WindMovementSource + "/autowalk-enabled") { lock (gate) autoWalkEnabled = e.Value.ValueKind == JsonValueKind.True; return; }
            if (e.Topic == "osc/receive/avatar/change") { lock (gate) { inputs.Remove("VRChat Velocity"); inputs.Remove("PhysBone"); inStation = false; vx = vy = vz = 0; } return; }
            if (e.Topic == "osc/receive/avatar/parameters/InStation")
            {
                var arguments = e.Value.GetProperty("arguments");
                if (arguments.GetArrayLength() > 0) { var state = arguments[0]; lock (gate) inStation = state.ValueKind == JsonValueKind.True || (state.ValueKind == JsonValueKind.Number && state.GetDouble() != 0); }
                return;
            }
            if (e.Topic is "osc/receive/avatar/parameters/VelocityX" or "osc/receive/avatar/parameters/VelocityY" or "osc/receive/avatar/parameters/VelocityZ")
            {
                var arguments = e.Value.GetProperty("arguments");
                if (arguments.GetArrayLength() == 0 || arguments[0].ValueKind != JsonValueKind.Number || !arguments[0].TryGetDouble(out var axis) || !double.IsFinite(axis)) return;
                lock (gate)
                {
                    if (e.Topic.EndsWith('X')) vx = axis; else if (e.Topic.EndsWith('Y')) vy = axis; else vz = axis;
                    if (e.Timestamp - magnitudeTime > TimeSpan.FromSeconds(1)) inputs["VRChat Velocity"] = (Math.Sqrt(vx * vx + vy * vy + vz * vz), e.Timestamp);
                }
                return;
            }
            string? source = e.Topic switch
            {
                var t when t == "sources/" + settings.WindMovementSource + "/speed-kmh" => "Treadmill",
                var t when settings.WindMovementSource == "VRTI" && t == "sources/VRTI/target-speed-kmh" => "AutoWalk",
                var t when t == "sources/" + settings.WindMovementSource + "/autowalk-speed" => settings.WindMovementSource == "FitOSC" ? "FitOSC Walking" : "AutoWalk",
                var t when t == "sources/" + settings.WindHeartSource + "/heart/bpm" => "Heart Rate",
                var t when t == "osc/receive" + settings.WindOscAddress => "Direct OSC",
                "windforce/input/mapped" => "Direct OSC",
                "osc/receive/avatar/parameters/WinForceSensor_Angle" => "PhysBone",
                "osc/receive/avatar/parameters/VelocityMagnitude" => "VRChat Velocity",
                _ => null
            };
            if (source is null) return;
            var value = e.Value;
            if (value.ValueKind == JsonValueKind.Object && value.TryGetProperty("arguments", out var args) && args.GetArrayLength() > 0) value = args[0];
            if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number)) return;
            lock (gate) { inputs[source] = (number, e.Timestamp); if (source == "VRChat Velocity") magnitudeTime = e.Timestamp; }
            if (source is "Treadmill" or "Heart Rate") bus.Publish("windforce/input/" + (source == "Treadmill" ? "speed" : "heart"), number, Id);
        });
        uint sequence = 0;
        double output = 0, fadeStart = 0, fadeTarget = 0; DateTimeOffset fadeTime = default;
        string lastSource = "", fadeSource = "";
        try
        {
            while (!token.IsCancellationRequested)
            {
                double desired = 0; string source = "Keine Quelle"; bool isPaused;
                var now = DateTimeOffset.UtcNow;
                WindRule? selected = null;
                lock (gate)
                {
                    isPaused = paused;
                    if (manual) { desired = manualPercent; source = "Manuell"; }
                    else foreach (var rule in settings.WindRules.Where(r => r.Enabled))
                    {
                        if (rule.Source == "Heart Rate" && settings.WindHeartMode != "SOURCE") continue;
                        if (rule.Source is "AutoWalk" or "FitOSC Walking" && !autoWalkEnabled) continue;
                        if (!inputs.TryGetValue(rule.Source, out var datum) || now - datum.Time > TimeSpan.FromSeconds(rule.TimeoutSeconds)) continue;
                        if (rule.Source == "PhysBone" && datum.Value <= rule.Start) continue;
                        desired = rule.Source == "VRChat Velocity" && inStation ? 0 : rule.Map(datum.Value); source = rule.Source; selected = rule; break;
                    }
                    if (settings.WindHeartMode == "MINIMUM" && inputs.TryGetValue("Heart Rate", out var heart))
                    {
                        var rule = settings.WindRules.FirstOrDefault(r => r.Source == "Heart Rate" && r.Enabled);
                        if (rule is not null && now - heart.Time < TimeSpan.FromSeconds(rule.TimeoutSeconds)) desired = Math.Max(desired, rule.Map(heart.Value));
                    }
                }
                if (isPaused) { output = 0; desired = 0; source = "Pause"; fadeTime = default; }
                else if (source == "Direct OSC" || source == "Manuell") { output = desired; fadeTime = default; }
                else if (desired >= output)
                {
                    output += (desired - output) * (settings.WindRiseMs <= 0 ? 1 : 1 - Math.Exp(-100d / settings.WindRiseMs)); fadeTime = default;
                }
                else
                {
                    var rule = settings.WindRules.FirstOrDefault(r => r.Source == lastSource) ?? selected;
                    if (fadeTime == default || fadeSource != lastSource || desired > fadeTarget + .05) { fadeTime = now; fadeStart = output; fadeTarget = desired; fadeSource = lastSource; }
                    else fadeTarget = Math.Min(fadeTarget, desired);
                    var progress = rule is null || rule.FadeMs <= 0 ? 1 : (now - fadeTime).TotalMilliseconds / rule.FadeMs;
                    output = fadeStart + (fadeTarget - fadeStart) * WindRule.Fade(rule?.Curve ?? "INSTANT", progress);
                }
                if (source != "Keine Quelle") lastSource = source;
                if (target is { } endpoint)
                {
                    var command = settings.WindMultiPwm ? $"FANS {output.ToString("0.0", CultureInfo.InvariantCulture)} {output.ToString("0.0", CultureInfo.InvariantCulture)} {output.ToString("0.0", CultureInfo.InvariantCulture)} {sequence++}" : Command(output, sequence++);
                    await udp.SendAsync(Encoding.ASCII.GetBytes(command), endpoint, token);
                }
                bus.Publish("windforce/source", source, Id); bus.Publish("windforce/target-percent", desired, Id); bus.Publish("windforce/output-percent", output, Id);
                await Task.Delay(100, token);
            }
        }
        finally
        {
            if (target is { } endpoint) try { await udp.SendAsync(Encoding.ASCII.GetBytes(Command(0, sequence)), endpoint); } catch (SocketException) { }
            await reception.CancelAsync(); try { await Task.WhenAll(receive, discover); } catch (OperationCanceledException) { } discovery?.Dispose();
        }
    }
    private async Task ReceiveStatus(UdpClient udp, CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            var packet = await udp.ReceiveAsync(token);
            try
            {
                using var document = JsonDocument.Parse(packet.Buffer);
                var root = document.RootElement;
                if (!root.TryGetProperty("type", out var type) || type.GetString() is not ("winforce_discovery" or "winforce_status")) continue;
                if (target is null) { target = new(packet.RemoteEndPoint.Address, settings.WindPort); Log("WindForce ESP32 gefunden: " + packet.RemoteEndPoint.Address); }
                if (!packet.RemoteEndPoint.Address.Equals(target.Address)) continue;
                bus.Publish("windforce/device/ip", packet.RemoteEndPoint.Address.ToString(), Id);
                foreach (var property in root.EnumerateObject()) if (property.Name != "ip") bus.Publish("windforce/device/" + property.Name, property.Value, Id);
            }
            catch (JsonException) { }
        }
    }
}

public sealed class CompanionSource(Settings settings, IDataBus bus, Action<string> log) : Worker("companion", ComponentKind.Source, log)
{
    protected override async Task Run(CancellationToken token)
    {
        using var http = new HttpClient { BaseAddress = new Uri(settings.CompanionUrl), Timeout = TimeSpan.FromSeconds(5) };
        http.DefaultRequestHeaders.Add("X-VR-Fitness-Code", settings.CompanionCode);
        while (!token.IsCancellationRequested)
        {
            try
            {
                using var doc = JsonDocument.Parse(await http.GetStringAsync("/status", token)); bus.Publish("companion/status", doc.RootElement, Id);
                foreach (var property in doc.RootElement.EnumerateObject()) bus.Publish("companion/" + property.Name, property.Value, Id);
                var count = await HealthSync.SyncAsync(settings, http, token); if (count > 0) Log($"Companion: {count} Session(s) synchronisiert");
            }
            catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or IOException) { if (!token.IsCancellationRequested) Log("Companion: " + ex.GetType().Name); }
            await Task.Delay(TimeSpan.FromSeconds(30), token);
        }
    }
}
