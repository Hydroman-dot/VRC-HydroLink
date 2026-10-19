using Buttplug.Core.Messages;
using VrcHapticsBridge.Core;
using VrcHapticsBridge.Tests;
using HydroLink.Core;

internal static class UniversalChecks
{
    static OscValue V(string key, double value) => new("/avatar/parameters/HydroLink/" + key, value);
    static OscValue[] Controls(double value = 1) => [V("Enabled", 1), V("Strength", value), V("Vibration", 1), V("Pump", 1), V("Rotation", 1), V("Motion", 1)];
    public static async Task Run(Action<bool, string> check, bool network)
    {
        var plug = new SpsSource("Plug", "/avatar/parameters/OGB/Pen/Test/");
        var socket = new SpsSource("Socket", "/avatar/parameters/OGB/Orf/Test/", .2);
        var input = new SpsInput();
        input.Receive(plug.Prefix + "PenOthers", .6);
        input.Receive(socket.Prefix + "PenOthersNewRoot", .9);
        check(input.Level(socket) == 0, "socket requires both root and tip");
        input.Receive(socket.Prefix + "PenOthersNewTip", .98);
        check(input.Level(socket) == 0, "socket proximity outside entry does not actuate");
        input.Receive(socket.Prefix + "PenOthersNewTip", 1);
        check(Math.Abs(input.Level(socket) - .5) < .0001 && input.Level(plug) == .6, "plug and socket geometry interpreted separately");
        input.Receive(socket.Prefix + "PenOthersNewTip", 0);
        check(input.Level(socket) == 0, "socket exit resets level");
        input.Clear(); check(input.Level(plug) == 0, "SPS state cleared on reset");
        var discovered = SpsSource.Discover([plug.Prefix + "PenOthers", socket.Prefix + "PenOthersNewRoot", "/avatar/parameters/Unrelated"]);
        check(discovered.Length == 2 && discovered.Select(s => s.Kind).Distinct().Count() == 2, "both SPS source namespaces discovered");
        var backend = new UniversalMemory(); var clock = new HapticsClock();
        await using (var controller = new UniversalHaptics(backend, clock))
        {
            async Task Drain() { for (var i = 0; i < 10; i++) { clock.Advance(1); await controller.TickAsync(); } }
            await controller.ConfigureAsync(backend.Features.Select(f => new FeatureRoute(f, .4)), UniversalSourceMode.Menu, []);
            controller.Receive(Controls()); await controller.ArmAsync(); await Drain();
            check(backend.Values.Count == 0, "universal arm never replays earlier menu values");
            controller.Receive(Controls(.5)); await Drain();
            check(backend.Values.Count == 4 && backend.Values.All(v => Math.Abs(v.Amount - .2) < .0001), "common input reaches every selected feature with its cap");
            controller.Receive([V("Clockwise", 0)]); await Drain();
            check(backend.Values.Any(v => v.Feature.Type == OutputType.Rotate && !v.Clockwise), "rotation direction updates even at constant intensity");
            backend.Values.Clear();
            controller.Receive([V("Mode", 2)]); await Drain();
            check(controller.Snapshot.Sent.Where(kv => kv.Key == backend.Features[1].Key).All(kv => kv.Value == 0)
                && controller.Snapshot.Sent[backend.Features[2].Key] > 0, "Ridge profile keeps rotation and silences Constrict");
            controller.Receive([V("Mode", 1)]); await Drain();
            check(controller.Snapshot.Sent[backend.Features[2].Key] == 0 && controller.Snapshot.Sent[backend.Features[1].Key] > 0,
                "Max 2 profile keeps Constrict and silences rotation");
            controller.Receive([V("Enabled", 0)]); await Drain();
            check(!controller.Snapshot.Armed && backend.Stops.Contains(1u) && backend.Stops.Contains(2u), "avatar switch off stops every selected device");
            await controller.ArmAsync(); controller.Receive(Controls());
            controller.Receive([new("/avatar/change", null), V("Enabled", 1)]); await Drain();
            check(!controller.Snapshot.Armed, "avatar change wins over other packet values");
            await controller.ConfigureAsync([new(backend.Features[0])], UniversalSourceMode.Sps, [plug, socket]);
            await controller.ArmAsync(); controller.Receive(Controls());
            controller.Receive([new(plug.Prefix + "PenOthers", .5), new(socket.Prefix + "PenOthersNewRoot", .9), new(socket.Prefix + "PenOthersNewTip", 1)]);
            await Drain();
            check(Math.Abs(controller.Snapshot.SourceLevel - .5) < .0001 && Math.Abs(controller.Snapshot.Sent.Values.Single() - .125) < .0001, "multiple sources use maximum instead of double-counting");
            controller.Receive([new("/avatar/parameters/OGB_ENABLED", 0)]); await Drain();
            check(!controller.Snapshot.Armed, "OGB disable disarms SPS routing");
            await controller.ConfigureAsync([new(backend.Features[0])], UniversalSourceMode.Sps, [plug], 2);
            await controller.ArmAsync(); controller.Receive(Controls()); controller.Receive([new(plug.Prefix + "PenOthers", .8)]);
            await controller.TickAsync(); clock.Advance(3); await controller.TickAsync();
            check(controller.Snapshot.Sent.Values.All(v => v == 0), "optional idle limit clears stale SPS output");
            backend.FailOutput = true; await controller.ArmAsync(); controller.Receive(Controls()); controller.Receive([new(plug.Prefix + "PenOthers", .8)]); await Drain();
            check(!controller.Snapshot.Armed, "output failure disarms universal engine");
            backend.FailOutput = false; backend.FailStopId = 1;
            bool refused = false; try { await controller.ArmAsync(); } catch (AggregateException) { refused = true; }
            check(refused && !controller.Snapshot.Armed, "failed stop blocks rearming");
            backend.FailStopId = null; await controller.StopAsync();
            await controller.ConfigureAsync(backend.Features.Select(f => new FeatureRoute(f)), UniversalSourceMode.Menu, []);
            await controller.ArmAsync(); controller.Receive(Controls());
            backend.Connected = false; await Drain();
            check(!controller.Snapshot.Armed, "backend loss disarms all selected features");
            backend.Connected = true;
            await controller.ConfigureAsync([new(backend.Features[0])], UniversalSourceMode.Menu, []);
            await controller.ArmAsync(); controller.Receive(Controls());
            backend.Hold = new(TaskCreationOptions.RunContinuationsAsynchronously);
            backend.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
            var tick = controller.TickAsync(); await backend.Entered.Task;
            var stop = controller.StopAsync(); backend.Hold.TrySetResult(); await tick; await stop;
            check(!controller.Snapshot.Armed && controller.Snapshot.Sent.Count == 0 && backend.Events.Last() == "stop:1", "in-flight command is followed by stop with no stale acknowledgment");
            backend.Hold = null;
        }
        if (!network) return;
        await using var server = new FakeServer(); server.UniversalDevices = true; await server.StartAsync();
        var bus = new DataBus();
        await using var service = new HapticsService(new() { IntifaceUrl = server.Url }, bus, Console.WriteLine);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(25));
        await service.StartAsync(timeout.Token);
        while (service.Backend.Features.Count < 8) await Task.Delay(25, timeout.Token);
        var features = service.Backend.Features;
        check(features.Any(f => f.Reversible) && features.Count(f => f.DeviceId == 9 && f.Type == OutputType.Vibrate) == 2, "real client discovers signed rotation and independent motors");
        check(features.Any(f => f.Type == OutputType.Temperature && !f.Supported), "unknown-context functions are visible but not automatically driven");
        var selected = features.Where(f => f.Supported && f.DeviceId != 7 && f.DeviceId != 8).Select(f => new FeatureRoute(f, .2)).ToArray();
        await service.ConfigureUniversalAsync(selected, UniversalSourceMode.Menu, []);
        await service.ArmUniversalAsync();
        foreach (var v in Controls().Append(V("Clockwise", 0))) bus.Publish("osc/receive" + v.Address, new { address = v.Address, arguments = new[] { v.Value } }, "test");
        while (service.Universal.Snapshot.Sent.Count < selected.Count(r => r.Feature.Reversible || r.Feature.Type != OutputType.Rotate)) await Task.Delay(25, timeout.Token);
        var messages = server.Messages.Where(m => m.TryGetProperty("OutputCmd", out _)).Select(m => m.GetProperty("OutputCmd")).ToArray();
        check(messages.Any(m => m.GetProperty("FeatureIndex").GetInt32() == 15 && m.GetProperty("Command").GetProperty("Rotate").GetProperty("Value").GetInt32() < 0), "real wire rotation uses advertised negative range");
        check(messages.Any(m => m.GetProperty("FeatureIndex").GetInt32() == 21 && m.GetProperty("Command").GetProperty("HwPositionWithDuration").GetProperty("Duration").GetInt32() == 500), "linear output uses bounded duration and advertised index");
        check(messages.Any(m => m.GetProperty("DeviceIndex").GetInt32() == 10 && m.GetProperty("Command").TryGetProperty("Oscillate", out _)), "oscillation is routed as its own capability");
        check(!messages.Any(m => m.GetProperty("DeviceIndex").GetInt32() == 7), "unselected devices never receive output");
        bus.Publish("osc/target-changed", false, "test");
        while (service.Universal.Snapshot.Armed) await Task.Delay(25, timeout.Token);
        check(!service.Universal.Snapshot.Armed, "OSC target loss disarms service");
        await service.ArmUniversalAsync(); await server.DropAsync();
        while (service.Backend.Connected) await Task.Delay(25, timeout.Token);
        check(!service.Universal.Snapshot.Armed && service.Universal.Snapshot.SelectedFeatures == 0, "Intiface disconnect clears universal selection");
    }
}
internal sealed class UniversalMemory(IReadOnlyList<HapticFeature>? extra = null) : IUniversalHapticsBackend
{
    public bool Connected { get; set; } = true;
    public IReadOnlyList<HapticFeature> Features { get; } = new HapticFeature[] {
        new(1, 2, "A", "Motor", OutputType.Vibrate, 0, 20, 0),
        new(1, 9, "A", "Pump", OutputType.Constrict, 0, 3, 0),
        new(2, 15, "B", "Rotation", OutputType.Rotate, -20, 20, 0),
        new(2, 19, "B", "Second motor", OutputType.Vibrate, 0, 20, 0)
    }.Concat(extra ?? []).ToArray();
    public List<(HapticFeature Feature, double Amount, bool Clockwise)> Values { get; } = [];
    public List<uint> Stops { get; } = [];
    public List<string> Events { get; } = [];
    public bool FailOutput;
    public uint? FailStopId;
    public TaskCompletionSource? Hold, Entered;
    public async Task SetFeatureAsync(HapticFeature feature, double amount, bool clockwise, CancellationToken token)
    {
        if (FailOutput) throw new IOException("Injected output failure");
        Entered?.TrySetResult();
        if (Hold != null) await Hold.Task.WaitAsync(token);
        Values.Add((feature, amount, clockwise)); Events.Add("output");
    }
    public Task StopDeviceAsync(uint id, CancellationToken token)
    {
        Stops.Add(id); Events.Add("stop:" + id);
        return id == FailStopId ? Task.FromException(new IOException("Injected stop failure")) : Task.CompletedTask;
    }
}
