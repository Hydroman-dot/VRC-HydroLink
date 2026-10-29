using HydroLink.Core;
using System.Text.Json;
internal static class HeartrateChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        var small = BleHeartMeasurement.Parse([0, 85]);
        check(small.Bpm == 85 && small.Contact == null && small.RrSeconds.Length == 0, "BLE 8-bit BPM with optional flags absent");
        var full = BleHeartMeasurement.Parse([31, 44, 1, 7, 0, 0, 4, 0, 2]);
        check(full.Bpm == 300 && full.Contact == true && full.EnergyKj == 7 && full.RrSeconds.SequenceEqual(new[] { 1d, .5d }), "BLE 16-bit BPM, contact, energy and multiple RR intervals");
        check(BleHeartMeasurement.Parse([4,80]).Contact == false, "BLE skin-contact failure is distinguished from unsupported contact");
        foreach (var bytes in new byte[][] { [], [1,1], [8,90,0], [16,90], [16,90,1] })
        {
            bool rejected = false; try { BleHeartMeasurement.Parse(bytes); } catch (FormatException) { rejected = true; }
            check(rejected, "truncated BLE packet rejected " + Convert.ToHexString(bytes));
        }
        var cfg = new HeartrateOptions { SmoothValue = false, SmoothAverage = false, VrcOscParameters = true }; cfg.Validate();
        var now = DateTimeOffset.UtcNow; var state = new HeartrateFrame(cfg); state.Receive(123, now);
        var frame = state.Tick(now);
        check(frame.Count == 9 && HeartrateOptions.Names.All(frame.ContainsKey), "all nine VRCOSC heartrate parameters available");
        check(frame["Connected"] is true && frame["Enabled"] is true && frame["Value"] is int value && value == 123 && frame["Normalised"] is float n && Math.Abs(n - 123f/240) < .0001, "VRCOSC parameter types and normalisation");
        check(Equals(frame["Units"], .3f) && Equals(frame["Tens"], .2f) && Equals(frame["Hundreds"], .1f), "legacy digits divide by ten as expected by prefab");
        state.Receive(127, now.AddSeconds(1)); frame = state.Tick(now.AddSeconds(1));
        check(Equals(frame["Average"],125), "rolling average across received measurements");
        frame = state.Tick(now.AddSeconds(7));
        check(frame["Connected"] is false && frame["Beat"] is false && Equals(frame["Value"],0) && Equals(frame["Average"],0), "stale pulse resets avatar status and stops synthetic beat");
        state.Receive(300, now.AddSeconds(8)); frame = state.Tick(now.AddSeconds(8));
        check(Equals(frame["Normalised"],1f) && Equals(frame["Average"],300), "reconnection clears stale average and clamps normalised value");
        state.Receive(0,now.AddSeconds(9)); check(state.Tick(now.AddSeconds(9))["Connected"] is false, "invalid pulse disconnects output");
        var beat = new HeartrateFrame(cfg); beat.Receive(60,now); beat.Tick(now);
        check(beat.Tick(now.AddSeconds(1))["Beat"] is true && beat.Tick(now.AddSeconds(2))["Beat"] is false, "toggle beat follows BPM");
        cfg.PulseBeat = true; beat = new(cfg); beat.Receive(60,now); beat.Tick(now);
        check(beat.Tick(now.AddSeconds(1))["Beat"] is true && beat.Tick(now.AddMilliseconds(1050))["Beat"] is false, "pulse beat resets without needing OSC echo");
        var smoothCfg = new HeartrateOptions(); var smooth = new HeartrateFrame(smoothCfg); smooth.Receive(100,now);
        var first = (int)smooth.Tick(now)["Value"]; var second = (int)smooth.Tick(now.AddMilliseconds(50))["Value"];
        check(first > 0 && second > first && second < 100, "value smoothing converges instead of jumping");
        cfg.Parameters["Beat"] = cfg.Address("Value"); bool duplicate = false; try { cfg.Validate(); } catch (ArgumentException) { duplicate = true; }
        check(duplicate, "conflicting avatar parameter addresses rejected");
        var settings = new Settings { Fitness = true, HeartSource = "Bluetooth", Heartrate = new() { VrcOscParameters = true, SmoothValue = false, SmoothAverage = false } };
        check(HydroHost.RequiredSources(settings).Contains("Bluetooth") && HydroHost.RequiredSources(settings).Contains("heartrate-output") && HydroHost.RequiredSources(settings).Contains("osc"), "Bluetooth source and compatibility output dependencies selected");
        var bus = new DataBus(); var packets = new System.Collections.Concurrent.ConcurrentQueue<JsonElement>();
        using var sub = bus.Subscribe("osc/send", e => packets.Enqueue(e.Value));
        await using var output = new HeartrateOutput(settings,bus,_=>{});
        await output.StartAsync(CancellationToken.None);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        while (packets.Count == 0) await Task.Delay(10, timeout.Token);
        bus.Publish("fitness/heart/bpm", 88,"Bluetooth");
        while (!packets.Any(p => p.GetProperty("address").GetString()!.EndsWith("/Value") && p.GetProperty("arguments")[0].GetInt32() == 88)) await Task.Delay(10,timeout.Token);
        check(true,"heartrate worker publishes the actual compatibility OSC envelope");
        var zeroFloat = packets.First(p => p.GetProperty("address").GetString()!.EndsWith("/Normalised"));
        check(OscBridge.TypedValue(zeroFloat.GetProperty("arguments")[0], zeroFloat.GetProperty("type").GetString()) is float, "zero-valued normalised output preserves OSC float type across JSON bus");
        await output.DisposeAsync();
        check(packets.Last(p => p.GetProperty("address").GetString()!.EndsWith("/Connected")).GetProperty("arguments")[0].ValueKind == JsonValueKind.False, "stopping worker sends disconnected state");
    }
}
