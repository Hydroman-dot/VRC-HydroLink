using System.Text.Json;
using HydroLink.Core;
using VrcHapticsBridge.Core;
using VrcHapticsBridge.Tests;
internal static class ExplorerChecks
{
    public static async Task Run(Action<bool, string> check, bool network)
    {
        var monitor = new OscMonitor();
        using var tree = JsonDocument.Parse("""{"CONTENTS":{"avatar":{"CONTENTS":{"change":{"FULL_PATH":"/avatar/change","VALUE":["avtr_test"]},"parameters":{"CONTENTS":{"A":{"FULL_PATH":"/avatar/parameters/A","TYPE":"f","ACCESS":3,"VALUE":[0.5]}}}}}}}""");
        monitor.Query(tree.RootElement);
        check(monitor.Snapshot().Single().QueryValue == "[0.5]" && monitor.Snapshot().Single().Received == "—", "OSCQuery values are not falsely reported as received UDP");
        check(OscMonitor.FindAvatar(tree.RootElement) == "avtr_test", "initial avatar profile identified from query tree");
        monitor.Message("/avatar/parameters/A", [.25f], false); monitor.Message("/avatar/parameters/A", [.75f], true);
        check(monitor.Snapshot().Single().Received == "[0.25]" && monitor.Snapshot().Single().Sent == "[0.75]", "incoming and outgoing values remain distinct");
        monitor.SetAvatar("avtr_other"); check(monitor.Snapshot().Length == 0, "avatar change clears previous explorer values");
        var bus = new DataBus(); double mapped = -1; int emissions = 0;
        using var listener = bus.Subscribe("haptics/input/vibrate", e => { mapped = e.Value.GetDouble(); emissions++; });
        var route = new OscRoute { Enabled = true, Avatar = "avtr_test", Source = "/avatar/parameters/A", Scale = 2, Offset = .1, Max = .8 };
        using var routing = new OscRouting(bus, [route]);
        void Input(double n) => bus.Publish("osc/receive/avatar/parameters/A", new { address = "/avatar/parameters/A", arguments = new[] { n } }, "osc");
        Input(1); check(emissions == 0, "avatar-specific routing inactive without matching avatar");
        bus.Publish("osc/avatar-id", "avtr_test", "osc"); Input(.7);
        check(mapped == .8, "avatar route scales and clamps input");
        Input(0); check(mapped == .1 && emissions == 2, "rapid final input is not lost by rate limiting");
        routing.Replace([route with { Enabled = false }]); Input(.2); check(emissions == 2, "disabled mapping sends nothing");
        bool cycleRejected = false;
        try { OscRouting.ValidateRoutes([route with { Source = "/avatar/parameters/A", Target = "/avatar/parameters/B" }, new OscRoute { Enabled = true, Source = "/avatar/parameters/B", Target = "/avatar/parameters/A" }]); } catch (ArgumentException) { cycleRejected = true; }
        check(cycleRejected, "OSC feedback chain rejected");
        bool sentRejected = false; try { (route with { Source = "osc/sent-value/avatar/parameters/A" }).Validate(); } catch (ArgumentException) { sentRejected = true; }
        check(sentRejected, "sent telemetry cannot create local routing feedback");
        string? textSent = null;
        using var textListener = bus.Subscribe("osc/send", e => { if (e.Value.GetProperty("address").GetString() == "/avatar/parameters/TextOut") textSent = e.Value.GetProperty("arguments")[0].GetString(); });
        routing.Replace([new() { Enabled = true, Source = "plugin/text", Target = "/avatar/parameters/TextOut", Type = "s" }]);
        bus.Publish("plugin/text", "Hallo", "test"); check(textSent == "Hallo", "text topics can map to string avatar parameters without numeric conversion");
        var saved = JsonSerializer.Deserialize<List<OscRoute>>(JsonSerializer.Serialize(new[] { route with { Invert = true } }))!;
        check(saved.Single().Avatar == "avtr_test" && saved.Single().Invert, "avatar route profiles and transforms survive serialization");
        using var one = JsonDocument.Parse("1");
        check(OscBridge.TypedValue(one.RootElement, "f") is float && OscBridge.TypedValue(one.RootElement, "i") is int && OscBridge.TypedValue(one.RootElement, "T") is true, "explicit outgoing OSC Float Int Bool preserved");
        check(HydroHost.RequiredSources(new() { Haptics = true }).Contains("osc"), "Haptics acquires shared OSCQuery source");
        var backend = new MemoryHaptics(); var clock = new HapticsClock();
        await using (var controller = new BridgeController(backend, clock))
        {
            controller.Receive([new("/avatar/parameters/MaxVibrate", .7)]); await controller.ArmOscAsync(); await controller.TickAsync();
            check(backend.Values.Count == 0, "arming never replays old haptics values");
            controller.Receive([new("/avatar/parameters/MaxVibrate", .6), new("/avatar/parameters/MaxPump", .4)]); await controller.TickAsync();
            check(backend.Values.Contains((Channel.Vibrate, .6)) && backend.Values.Contains((Channel.Pump, .4)), "both Haptics channels operate independently in same cycle");
            controller.Receive([new("/avatar/change", null)]); await controller.TickAsync();
            check(controller.Snapshot.Mode == OutputMode.Off && controller.Snapshot.PumpSent == 0, "avatar change disarms and stops Haptics");
            await controller.TestAsync(Channel.Pump, .1); await controller.TickAsync(); clock.Advance(4); await controller.TickAsync();
            check(controller.Snapshot.Mode == OutputMode.Off, "manual Haptics test ends automatically");
        }
        if (!network) return;
        await using var server = new FakeServer(); await server.StartAsync();
        var liveBus = new DataBus();
        await using var service = new HapticsService(new() { IntifaceUrl = server.Url }, liveBus, Console.WriteLine);
        using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(12)); await service.StartAsync(deadline.Token);
        while (service.Backend.Devices.Count == 0) await Task.Delay(25, deadline.Token);
        await service.SelectAsync(7); await service.Controller.ArmOscAsync();
        foreach (var address in new[] { "/avatar/parameters/MaxVibrate", "/avatar/parameters/MaxPump" }) liveBus.Publish("osc/receive" + address, new { address, arguments = new[] { .5 } }, "osc");
        while (server.Messages.Count(m => m.TryGetProperty("OutputCmd", out _)) < 2) await Task.Delay(25, deadline.Token);
        var outputs = server.Messages.Where(m => m.TryGetProperty("OutputCmd", out _)).Select(m => m.GetProperty("OutputCmd")).ToArray();
        check(outputs.Any(m => m.GetProperty("FeatureIndex").GetInt32() == 2) && outputs.Any(m => m.GetProperty("FeatureIndex").GetInt32() == 9), "integrated Haptics uses advertised motor and Constrict feature indices over real WebSocket");
        liveBus.Publish("routing/reset", true, "routing");
        check(service.Controller.Snapshot.Mode == OutputMode.Off, "mapping changes disarm live Haptics service");
        await service.SelectAsync(8);
        check(service.Backend.Selected is { Vibrate: true, Pump: false }, "vibration-only device does not falsely advertise Pump");
        await service.SelectAsync(7); await service.Controller.ArmOscAsync(); await server.DropAsync();
        while (service.Backend.Connected) await Task.Delay(25, deadline.Token);
        check(service.Controller.Snapshot.Mode == OutputMode.Off && service.Backend.Selected is null, "server disconnect clears device selection and disarms integrated bridge");
    }
}
internal sealed class HapticsClock : TimeProvider
{
    private DateTimeOffset now = DateTimeOffset.UtcNow;
    public override DateTimeOffset GetUtcNow() => now;
    public void Advance(int seconds) => now = now.AddSeconds(seconds);
}
internal sealed class MemoryHaptics : IHapticsBackend
{
    public bool Connected => true;
    public DeviceInfo Selected => new(1, "test", true, true, 0);
    public List<(Channel, double)> Values { get; } = [];
    public Task SetAsync(Channel channel, double value, CancellationToken token) { Values.Add((channel, value)); return Task.CompletedTask; }
    public Task StopAsync(CancellationToken token) => Task.CompletedTask;
}
