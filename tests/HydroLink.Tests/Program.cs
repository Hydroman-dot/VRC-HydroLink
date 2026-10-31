using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using HydroLink.Core;
using VRC.OSCQuery;

int checks = 0;
void Check(bool condition, string name) { if (!condition) throw new Exception("FAIL: " + name); Console.WriteLine("PASS: " + name); checks++; }
if (args.Contains("--mqtt-only")) { await HomeAssistantChecks.Run(Check); Console.WriteLine($"FINAL: {checks} MQTT checks passed."); return; }
if (args.Contains("--battery-only")) { await BatteryChecks.Run(Check); Console.WriteLine($"FINAL: {checks} battery checks passed."); return; }
foreach (var (tag, draft, pre, expected) in new[] { ("v0.14.2", false, false, "0.14.2"), ("v0.14.1", false, false, (string?)null), ("v0.13.0", false, false, (string?)null), ("v0.15.0", true, false, (string?)null), ("v0.15.0", false, true, (string?)null), ("v0.15.0-beta", false, false, (string?)null) }) {
    var name = "VRC-HydroLink-" + tag[1..] + "-win-x64.zip";
    using var doc = JsonDocument.Parse(JsonSerializer.Serialize(new { tag_name=tag, draft, prerelease=pre, assets=new[] {new {name, browser_download_url="https://github.com/Hydroman-dot/VRC-HydroLink/releases/download/"+tag+"/"+name},new {name=name+".sha256",browser_download_url="https://github.com/Hydroman-dot/VRC-HydroLink/releases/download/"+tag+"/"+name+".sha256"}} }));
    Check(ReleaseUpdate.NewerVersion(doc.RootElement,new Version(0,14,1,0))==expected,"startup update version filter "+tag+" draft="+draft+" pre="+pre);
}
using (var missing = JsonDocument.Parse("{\"tag_name\":\"v0.15.0\",\"draft\":false,\"prerelease\":false,\"assets\":[]}")) Check(ReleaseUpdate.NewerVersion(missing.RootElement,new Version(0,14,1))==null,"incomplete release does not prompt an update");
using (var ownReceiver = OscBridge.OpenReceiver())
using (var anotherReceiver = OscBridge.OpenReceiver())
{
    var ownPort = ((IPEndPoint)ownReceiver.Client.LocalEndPoint!).Port;
    var anotherPort = ((IPEndPoint)anotherReceiver.Client.LocalEndPoint!).Port;
    Check(ownPort != 9001 && anotherPort != 9001 && ownPort != anotherPort, "HydroLink receivers reserve independent ports without occupying VRCOSC port 9001");
    using var probe = new UdpClient();
    await probe.SendAsync(OscCodec.Encode("/coexistence", 1), new IPEndPoint(IPAddress.Loopback, ownPort));
    using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(3));
    var incoming = await ownReceiver.ReceiveAsync(deadline.Token);
    Check(OscCodec.Decode(incoming.Buffer).Single().Address == "/coexistence", "dynamically allocated HydroLink port receives OSC packets");
}
foreach (var (seconds, expected) in new[] { (0d, "0:00:00"), (59d, "0:00:59"), (60d, "0:01:00"), (3661.9, "1:01:01"), (90061d, "25:01:01"), (730364761d, "202879:06:01"), (-1d, "—"), (double.NaN, "—"), (double.PositiveInfinity, "—") })
    Check(DurationDisplay.Format(seconds) == expected, "duration clock " + seconds);
await HeartrateChecks.Run(Check);
var bus = new DataBus(); int count = 0;
using (bus.Subscribe("fitness", _ => count++)) { bus.Publish("fitness/bpm", 80, "test"); bus.Publish("fitnessExtra", 1, "test"); }
bus.Publish("fitness/bpm", 81, "test"); Check(count == 1, "topic segment matching and unsubscribe");
int errors = 0; bus.SubscriberError += _ => errors++;
using (bus.Subscribe("fault", _ => throw new Exception())) bus.Publish("fault", 1, "test");
Check(errors == 1, "subscriber failure isolation");
var osc = OscCodec.Decode(OscCodec.Encode("/test", 7, .25f, "Grüße", true, false)).Single();
Check(osc.Address == "/test" && (int)osc.Arguments[0] == 7 && (float)osc.Arguments[1] == .25f && (string)osc.Arguments[2] == "Grüße" && (bool)osc.Arguments[3] && !(bool)osc.Arguments[4], "OSC wire roundtrip");
var packet = OscCodec.Encode("/test", 5); var bundle = new byte[20 + packet.Length]; Encoding.ASCII.GetBytes("#bundle").CopyTo(bundle, 0); BinaryPrimitives.WriteInt32BigEndian(bundle.AsSpan(16), packet.Length); packet.CopyTo(bundle, 20);
Check(OscCodec.Decode(bundle).Count == 1, "OSC bundle");
try { OscCodec.Decode([1, 2, 3]); throw new Exception("Malformed packet accepted"); } catch (FormatException) { checks++; Console.WriteLine("PASS: malformed OSC rejected"); }
using (var json = JsonDocument.Parse("{\"OSC_PORT\":12347,\"OSC_IP\":\"127.0.0.1\",\"OSC_TRANSPORT\":\"UDP\"}"))
    Check(OscBridge.ParseHostInfo(json.RootElement, IPAddress.Loopback).Port == 12347, "HOST_INFO determines UDP port, not HTTP or 9000");
Check(HydroHost.RequiredSources(new()).Count == 0, "disabled modules open no sources");
Check(HydroHost.RequiredSources(new() { OscHub = true, WindForce = true }).Count(x => x == "osc") == 1, "shared OSC source deduplicated");
double speed = 0, distance = 0, bpm = 0;
using var s1 = bus.Subscribe("fitness/speed-kmh", e => speed = e.Value.GetDouble());
using var s2 = bus.Subscribe("fitness/distance-km", e => distance = e.Value.GetDouble());
using var s3 = bus.Subscribe("fitness/heart/bpm", e => bpm = e.Value.GetDouble());
Telemetry.Parse("VRTI", "{\"event\":\"StatisticsUpdated\",\"payload\":\"{\\\"distance\\\":1250}\"}", bus);
Check(distance == 1.25, "VRTI JSON-string payload and meters-to-km migration");
double vrtiSeconds = 0;
using var timeSubscription = bus.Subscribe("fitness/active-seconds", e => vrtiSeconds = e.Value.GetDouble());
Telemetry.Parse("VRTI", "{\"event\":\"StatisticsUpdated\",\"payload\":{\"time\":730364761}}", bus);
Check(DurationDisplay.Format(vrtiSeconds) == "202:52:44", "VRTI millisecond counter matches treadmill screenshot clock");
Telemetry.Parse("VRTI", "{\"payload\":{\"time\":730364.761}}", bus, vrtiTimeMilliseconds: false);
Check(DurationDisplay.Format(vrtiSeconds) == "202:52:44", "VRTI seconds mode remains available for documented API variants");
Telemetry.Parse("FitOSC", "{\"type\":\"telemetry\",\"data\":{\"metrics\":{\"speed\":{\"value\":2,\"unit\":\"MPH\"}}}}", bus);
Check(Math.Abs(speed - 3.218688) < .00001, "FitOSC mph-to-kmh migration");
Telemetry.Parse("Pulsoid", "{\"data\":{\"heart_rate\":92}}", bus); Check(bpm == 92, "Pulsoid payload");
Telemetry.Parse("HypeRate", "{\"event\":\"phx_reply\",\"payload\":{\"hr\":100}}", bus); Check(bpm == 92, "HypeRate ignores non-heart events");
Telemetry.Parse("VRCOSC", "81", bus); Check(bpm == 81, "VRCOSC numeric payload");
Check(WindOutput.Command(120, 42) == "FAN 100.0 42" && WindOutput.Command(double.NaN, 1) == "FAN 0.0 1", "WindForce compatible command and clamp");
using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
using var receiver = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
var receiverPort = ((IPEndPoint)receiver.Client.LocalEndPoint!).Port;
await using (var wind = new WindOutput(new() { WindHost = "127.0.0.1", WindPort = receiverPort }, bus, Console.WriteLine))
{
    wind.Set(30); await wind.StartAsync(timeout.Token);
    var message = await receiver.ReceiveAsync(timeout.Token); Check(Encoding.ASCII.GetString(message.Buffer).StartsWith("FAN 30.0"), "real WindForce UDP send");
    await Task.Delay(2300, timeout.Token);
    bool zero = false;
    while (receiver.Available > 0) zero |= Encoding.ASCII.GetString((await receiver.ReceiveAsync(timeout.Token)).Buffer).StartsWith("FAN 0.0");
    Check(zero, "WindForce stale signal failsafe");
}
if (args.Contains("--network"))
{
    using var mockUdp = new UdpClient(new IPEndPoint(IPAddress.Loopback, 0));
    int mockPort = ((IPEndPoint)mockUdp.Client.LocalEndPoint!).Port;
    using var mock = new OSCQueryServiceBuilder().WithServiceName("VRChat-HydroLink-Test").WithTcpPort(Extensions.GetAvailableTcpPort()).WithUdpPort(mockPort).WithDefaults().Build();
    mock.AddEndpoint("/avatar/parameters/ExplorerTest", "f", VRC.OSCQuery.Attributes.AccessValues.ReadWrite, [.4f]);
    await using var bridge = new OscBridge(new(), bus, Console.WriteLine, "VRChat-HydroLink-Test");
    await bridge.StartAsync(timeout.Token);
    while (bridge.Target != $"127.0.0.1:{mockPort}") await Task.Delay(200, timeout.Token);
    Check(true, "mDNS discovery + HOST_INFO resolve a random OSC port");
    Check(bridge.Monitor.Snapshot().Any(r => r.Address == "/avatar/parameters/ExplorerTest" && r.Type == "f"), "discovered OSCQuery tree populates explorer");
    bus.Publish("osc/send", new { address = "/test", arguments = new[] { 42 } }, "test");
    OscMessage? routed = null;
    while (routed == null)
    {
        var sent = await mockUdp.ReceiveAsync(timeout.Token);
        routed = OscCodec.Decode(sent.Buffer).FirstOrDefault(m => m.Address == "/test");
    }
    Check(routed.Arguments[0] is int routedValue && routedValue == 42, "OSC routed to discovered port, independent of a running VRChat client");
    using var http = new HttpClient();
    using var own = JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{bridge.QueryPort}/?HOST_INFO", timeout.Token));
    Check(own.RootElement.GetProperty("OSC_PORT").GetInt32() == bridge.ReceivePort, "own receive port advertised by OSCQuery");
    using var avatarTree = JsonDocument.Parse(await http.GetStringAsync($"http://127.0.0.1:{bridge.QueryPort}/avatar", timeout.Token));
    Check(avatarTree.RootElement.GetProperty("FULL_PATH").GetString() == "/avatar", "avatar root advertised for all VRChat avatar messages");
    var incoming = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
    using var sub = bus.Subscribe("osc/receive/avatar/parameters/Test", _ => incoming.TrySetResult(true));
    await mockUdp.SendAsync(OscCodec.Encode("/avatar/parameters/Test", .5f), new IPEndPoint(IPAddress.Loopback, bridge.ReceivePort), timeout.Token);
    Check(await incoming.Task.WaitAsync(timeout.Token), "received OSC reaches data bus");
}
if (args.Contains("--network")) await OscStartupChecks.Run(Check);
Console.WriteLine($"{checks} checks passed.");
var pluginArg = Array.IndexOf(args, "--plugin");
if (pluginArg >= 0)
{
    using var pluginTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(20));
    var plugins = new PluginLoader(bus, Console.WriteLine);
    plugins.Load(args[pluginArg + 1], []);
    Check(plugins.Components.Count == 0 && plugins.Plugins.Count == 1, "disabled plugin does not start components");
    plugins = new PluginLoader(bus, Console.WriteLine);
    plugins.Load(args[pluginArg + 1], ["example.counter"]);
    var signal = new TaskCompletionSource<bool>();
    using var subscription = bus.Subscribe("example/counter", _ => signal.TrySetResult(true));
    Check(plugins.Components.Count == 1, "external SDK assembly loaded");
    foreach (var component in plugins.Components) await component.StartAsync(pluginTimeout.Token);
    Check(await signal.Task.WaitAsync(pluginTimeout.Token), "external source publishes arbitrary topic");
    foreach (var component in plugins.Components) await component.DisposeAsync();
    Console.WriteLine($"{checks} total checks passed.");
}

// Independent module lifetime and program launch regression checks.
var components = new Dictionary<string, FakeComponent>();
var launcher = new FakeLauncher();
var moduleSettings = new Settings { MovementSource = "VRTI", HeartSource = "VRCOSC", WindMovementSource = "VRTI", WindHeartSource = "VRCOSC", Programs = [new() { Provider = "VRTI", AutoStart = true, Path = "test.exe" }] };
await using (var independent = new HydroHost(moduleSettings, Console.WriteLine, launcher, id => components[id] = new FakeComponent(id)))
{
    await independent.SetModuleAsync("fitness", true);
    var shared = components["VRTI"];
    await independent.SetModuleAsync("windforce", true);
    Check(ReferenceEquals(shared, components["VRTI"]) && shared.Starts == 1, "shared source is not restarted for second tool");
    Check(launcher.Count == 1, "dependency autostart occurs once while shared");
    var oldWind = components["windforce"];
    await independent.RestartModuleAsync("windforce");
    Check(oldWind.Stops == 1 && !ReferenceEquals(oldWind, components["windforce"]) && shared.Stops == 0 && independent.ActiveModules.Contains("fitness"), "single module restart preserves other module and shared sources");
    var oldOsc = components["osc"];
    var resets = 0;
    using (independent.Bus.Subscribe("routing/reset", _ => resets++)) await independent.RestartModuleAsync("osc");
    Check(oldOsc.Stops == 1 && !ReferenceEquals(oldOsc, components["osc"]) && resets == 1 && shared.Stops == 0, "OSC restart replaces shared receiver and invalidates output without stopping other sources");
    await independent.SetModuleAsync("fitness", false);
    Check(shared.Stops == 0 && independent.ActiveModules.SetEquals(new[] { "windforce" }), "stopping Fitness leaves WindForce and shared source running");
    await independent.SetModuleAsync("osc", true);
    await independent.SetModuleAsync("windforce", false);
    Check(shared.Stops == 1 && components["osc"].Stops == 0, "last consumer releases provider but OSC Hub remains active");
    await independent.SetModuleAsync("osc", false);
    Check(independent.Running.Count == 0, "all module resources released independently");
}
await using (var failureHost = new HydroHost(new() { MovementSource = "None", HeartSource = "None" }, Console.WriteLine, launcher, id => new FakeComponent(id, id == "intiface")))
{
    await failureHost.SetModuleAsync("osc", true);
    try { await failureHost.SetModuleAsync("haptics", true); } catch (InvalidOperationException) { }
    Check(failureHost.ActiveModules.SetEquals(new[] { "osc" }) && failureHost.Running.SequenceEqual(new[] { "osc" }), "failed tool start does not stop unrelated tool");
}
var heartRule = WindRule.Defaults().Single(r => r.Source == "Heart Rate");
Check(heartRule.Map(90) == 0 && heartRule.Map(120) == 50 && heartRule.Map(150) == 100, "original WindForce heart calibration points");
Check(WindRule.Fade("SMOOTH", 0) == 0 && WindRule.Fade("SMOOTH", 1) == 1 && Math.Abs(WindRule.Fade("EXPONENTIAL", 1) - 1) < .00001, "original fade endpoint behavior");
var roundtrip = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(new Settings { Dashboard = [new() { Title = "Custom", Topic = "custom/data", Width = 420 }], Programs = [new() { Provider = "FitOSC", Path = "C:/test.exe", AutoStart = true }] }))!;
Check(roundtrip.Dashboard.Single().Topic == "custom/data" && roundtrip.Programs.Single().AutoStart, "custom dashboard and launch preferences survive serialization");
double isolated = 0; using var isolatedSub = bus.Subscribe("sources/Pulsoid/heart/bpm", e => isolated = e.Value.GetDouble());
var previousBpm = bpm; Telemetry.Parse("Pulsoid", "{\"data\":{\"heart_rate\":123}}", bus, false);
Check(isolated == 123 && bpm == previousBpm, "independent tool source does not overwrite Fitness data");
var pulseComponents = new Dictionary<string, FakeComponent>();
var pulseSettings = new Settings { HeartSource = "Bluetooth", MovementSource = "None", WindHeartSource = "Bluetooth", WindMovementSource = "None", Heartrate = new() { VrcOscParameters = true } };
await using (var pulseHost = new HydroHost(pulseSettings, _ => {}, new FakeLauncher(), id => pulseComponents[id] = new FakeComponent(id)))
{
    await pulseHost.SetModuleAsync("heartrate", true);
    var sensor = pulseComponents["Bluetooth"]; var pulseOutput = pulseComponents["heartrate-output"];
    Check(pulseHost.ActiveModules.SetEquals(new[] { "heartrate" }) && pulseHost.Running.Contains("osc") && !pulseHost.Running.Contains("VRTI"), "Heartrate alone starts sensor and avatar output without Fitness");
    await pulseHost.SetModuleAsync("fitness", true);
    await pulseHost.SetModuleAsync("fitness", false);
    Check(sensor.Stops == 0 && pulseOutput.Stops == 0, "stopping Fitness preserves Heartrate sensor and avatar output");
    await pulseHost.SetModuleAsync("fitness", true);
    await pulseHost.SetModuleAsync("windforce", true);
    await pulseHost.SetModuleAsync("heartrate", false);
    Check(sensor.Stops == 0 && pulseOutput.Stops == 1 && !pulseHost.Running.Contains("heartrate-output"), "stopping Heartrate stops avatar output but preserves shared sensor");
    await pulseHost.RestartModuleAsync("heartrate");
    Check(ReferenceEquals(sensor, pulseComponents["Bluetooth"]) && pulseHost.Running.Contains("heartrate-output"), "Heartrate restart reuses Fitness and WindForce sensor");
    await pulseHost.SetModuleAsync("heartrate", false);
    await pulseHost.SetModuleAsync("fitness", false);
    Check(sensor.Stops == 0, "WindForce keeps pulse sensor after other consumers stop");
    await pulseHost.SetModuleAsync("windforce", false);
    Check(sensor.Stops == 1 && pulseHost.Running.Count == 0, "last pulse consumer releases sensor and shared OSC");
}
Check(!HydroHost.RequiredSources(new() { Fitness = true, HeartSource = "Bluetooth", MovementSource = "None", Heartrate = new() { VrcOscParameters = true } }).Contains("heartrate-output"), "Fitness alone no longer enables avatar pulse output");
var sessionFolder = Path.Combine(Path.GetTempPath(), "HydroLink-test-" + Guid.NewGuid().ToString("N"));
await using (var recording = new FitnessSession(bus, sessionFolder))
{
    recording.Start("Test, \"session\""); bus.Publish("fitness/distance-km", 5, "test"); bus.Publish("fitness/steps", 100, "test");
    await Task.Delay(1150); bus.Publish("fitness/distance-km", 5.25, "test"); bus.Publish("fitness/steps", 130, "test");
    await Task.Delay(1150); await recording.StopAsync("saved note");
    Check(Math.Abs(recording.Distance - .25) < .00001 && recording.Steps == 30 && File.Exists(Path.ChangeExtension(recording.CsvPath, ".json")), "session records relative totals and saves summary");
}
var invalidSessionFolder = Path.Combine(sessionFolder, "not-a-folder");
File.WriteAllText(invalidSessionFolder, "occupied");
await using (var invalidRecording = new FitnessSession(bus, invalidSessionFolder))
{
    bool rejected = false;
    try { invalidRecording.Start("Must fail visibly"); } catch (IOException) { rejected = true; }
    Check(rejected && !invalidRecording.Running, "recording reports unwritable location before announcing a start");
}
await using (var quickRecording = new FitnessSession(bus, Path.Combine(sessionFolder, "quick")))
{
    quickRecording.Start("Immediate stop");
    Check(File.Exists(quickRecording.CsvPath), "recording file exists immediately after successful start");
    await quickRecording.StopAsync();
    Check(File.Exists(Path.ChangeExtension(quickRecording.CsvPath, ".json")) && quickRecording.RecordingError is null, "immediate stop flushes a readable recording and summary");
}
var legacyCsv = Path.Combine(sessionFolder, "legacy.csv");
await File.WriteAllTextAsync(legacyCsv, "timestamp;session_name;bpm;current_speed_kmh;steps;distance_km;active_time_s;session_elapsed_s\n2026-10-06T10:00:00+02:00;Legacy;90;1;100;5;10;0\n2026-10-06T10:00:02+02:00;Legacy;110;2;130;5.25;12;2\n");
var legacyReport = SessionAnalysis.Read(legacyCsv);
Check(Convert.ToDouble(legacyReport.Summary["distance_km"]) == .25 && Convert.ToDouble(legacyReport.Summary["steps"]) == 30 && Convert.ToDouble(legacyReport.Summary["avg_bpm"]) == 100, "original semicolon CSV and legacy columns reconstruct session totals");
Check(Convert.ToDouble(legacyReport.Summary["pulse_speed_correlation"]) == 1, "original pulse-speed correlation analysis");
var recorded = Directory.GetFiles(sessionFolder, "*.csv").First(p => p != legacyCsv);
Check(SessionAnalysis.Read(recorded).Summary["session_name"]?.ToString() == "Test, \"session\"", "new session CSV quoting roundtrip through analysis");
var chartReport = new SessionReport("test", new(), [
    new() { ["timestamp"]="2026-01-01T10:00:00Z", ["clean_bpm"]="80" },
    new() { ["timestamp"]="2026-01-01T10:00:02Z", ["clean_bpm"]="90", ["pulse_valid"]="false" },
    new() { ["timestamp"]="2026-01-01T10:00:12Z", ["clean_bpm"]="100" }]);
var chartPoints = SessionCharts.Points(chartReport,"clean_bpm");
Check(chartPoints.Select(p=>p.Seconds).SequenceEqual(new double[] {0,2,12}), "report chart uses elapsed timestamps rather than sample index");
Check(chartPoints[1].Value == null && chartPoints[2].Value == 100, "report chart preserves invalid-pulse gaps");
Check(SessionCharts.Points(new("test",new(),[new() { ["session_elapsed_s"]="3", ["clean_speed_kmh"]="0" }]),"clean_speed_kmh")[0] == new SessionChartPoint(3,0), "report chart supports elapsed-only data and stationary speed");
Check(SessionCharts.Points(new("test",new(),[new() { ["clean_bpm"]="80" }]),"clean_bpm").Count == 0, "report chart does not invent time for undated data");
var fitWalk = 0d;
using (bus.Subscribe("sources/FitOSC/autowalk-speed", e => fitWalk = e.Value.GetDouble()))
    Telemetry.Parse("FitOSC", "{\"type\":\"state\",\"data\":{\"metrics\":{},\"walking\":{\"velocity\":0.7,\"mode\":\"Auto\",\"isManualOverride\":false}}}", bus);
Check(fitWalk == .7, "FitOSC normalized walking data preserved independently");
Check(Math.Abs(FitnessOptions.CaloriesPerMinute(120, 80, 30, "male")!.Value - 10.173542065) < .00001, "original calorie formula reference value");
Check(FitnessOptions.CaloriesPerMinute(120, 0, 30, "male") is null && FitnessOptions.CaloriesPerMinute(double.NaN, 80, 30, "male") is null, "invalid fitness profile produces no calorie estimate");
var syncSettings = new Settings { FitnessOptions = new() { DataDirectory = sessionFolder, SendHealthSteps = true } };
await HealthSync.QueueAsync(legacyCsv, syncSettings, 42, "offline note");
var queued = Path.Combine(HealthSync.QueueFolder(syncSettings), "legacy.json");
using (var payload = JsonDocument.Parse(await File.ReadAllTextAsync(queued)))
    Check(payload.RootElement.GetProperty("write_steps").GetBoolean() && payload.RootElement.GetProperty("active_calories_kcal").GetDouble() == 42 && payload.RootElement.GetProperty("heart_rate_samples").GetArrayLength() == 1, "Health Companion payload includes selected metrics and decimated heart samples");
using (var rejected = new HttpClient(new CompanionReply(false)) { BaseAddress = new Uri("http://localhost:9876") })
{
    try { await HealthSync.SyncAsync(syncSettings, rejected, default); } catch (InvalidOperationException) { }
    Check(File.Exists(queued), "unconfirmed health upload keeps session queued");
}
using (var accepted = new HttpClient(new CompanionReply(true)) { BaseAddress = new Uri("http://localhost:9876") })
    Check(await HealthSync.SyncAsync(syncSettings, accepted, default) == 1 && !File.Exists(queued), "confirmed health upload removes only acknowledged queue file");
await ExplorerChecks.Run(Check, args.Contains("--network"));
await UniversalChecks.Run(Check, args.Contains("--network"));
await ToyChecks.Run(Check, args.Contains("--network"));
await AdditionalToyChecks.Run(Check);
await WizardSyncChecks.Run(Check, args.Contains("--network"));
if (args.Contains("--wizard-sync-live"))
{
    var handoff = new HydroLink.Core.WizardSyncReader().Read();
    Check(handoff.Profile is { AvatarId.Length: > 0 }, "live Unity handoff read directly from shared local folder");
    Check(handoff.Profile?.Includes(VrcHapticsBridge.Core.ToyKind.Max2) == true && handoff.Profile.Includes(VrcHapticsBridge.Core.ToyKind.Ridge), "live Unity handoff contains both installed toy prefabs");
}
var windSettings = new Settings { MovementSource = "VRTI", HeartSource = "None", WindMovementSource = "VRTI" };
var windComponents = new Dictionary<string, FakeComponent>();
bool rejectFit = false;
await using (var windHost = new HydroHost(windSettings, Console.WriteLine, new FakeLauncher(), id => windComponents[id] = new FakeComponent(id, rejectFit && id == "FitOSC")))
{
    await windHost.SetModuleAsync("fitness", true); await windHost.SetModuleAsync("osc", true); await windHost.SetModuleAsync("windforce", true);
    var vrtiBefore = windComponents["VRTI"]; var oscBefore = windComponents["osc"];
    var draft = WindConfiguration.From(windSettings); var first = draft.Rules[0]; draft.Rules.RemoveAt(0); draft.Rules.Add(first); draft.Rules[0].Enabled = true; draft.Movement = "FitOSC";
    Check(windSettings.WindRules[0].Source == "Direct OSC", "editing WindForce draft does not mutate running rules");
    await windHost.ConfigureWindAsync(draft);
    Check(ReferenceEquals(vrtiBefore, windComponents["VRTI"]) && ReferenceEquals(oscBefore, windComponents["osc"]) && vrtiBefore.Stops == 0 && windHost.ActiveModules.SetEquals(new[] { "fitness", "osc", "windforce" }), "applying WindForce sources preserves other modules and shared connections");
    Check(windSettings.WindMovementSource == "FitOSC" && windSettings.WindRules[0].Source == "PhysBone", "WindForce source and reordered priority applied together");
    draft.Rules[0].Enabled = false; Check(windSettings.WindRules[0].Enabled, "applied WindForce rules are detached from editable draft");
    draft.Movement = "VRTI"; await windHost.ConfigureWindAsync(draft); rejectFit = true; draft.Movement = "FitOSC";
    bool failed = false; try { await windHost.ConfigureWindAsync(draft); } catch (InvalidOperationException) { failed = true; }
    Check(failed && windSettings.WindMovementSource == "VRTI" && windHost.ActiveModules.Contains("windforce") && vrtiBefore.Stops == 0, "failed WindForce source change restores prior settings without stopping Fitness");
    var invalid = WindConfiguration.From(windSettings); invalid.Rules[0].Mid = invalid.Rules[0].Start;
    bool rejected = false; try { await windHost.ConfigureWindAsync(invalid); } catch (ArgumentException) { rejected = true; }
    Check(rejected && windHost.ActiveModules.Contains("windforce"), "invalid WindForce calibration rejected before stopping active module");
}
await RobustnessChecks.Run(Check);
Console.WriteLine($"FINAL: {checks} checks passed.");

sealed class FakeLauncher : IProgramLauncher
{
    public int Count; public void Start(LaunchProgram program) => Count++;
}
sealed class CompanionReply(bool accepted) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(accepted ? "{\"ok\":true}" : "{\"ok\":false}") });
}
sealed class FakeComponent(string id, bool fail = false) : HydroLink.Sdk.IComponent
{
    public string Id => id;
    public HydroLink.Sdk.ComponentKind Kind => HydroLink.Sdk.ComponentKind.Source;
    public int Starts, Stops;
    public Task StartAsync(CancellationToken token) { if (fail) throw new InvalidOperationException("Test failure"); Starts++; return Task.CompletedTask; }
    public ValueTask DisposeAsync() { Stops++; return ValueTask.CompletedTask; }
}
