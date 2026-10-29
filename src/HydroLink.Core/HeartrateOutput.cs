using System.Text.Json;
using HydroLink.Sdk;
namespace HydroLink.Core;
public sealed class HeartrateOutput(Settings settings, IDataBus bus, Action<string> log) : Worker("heartrate-output", ComponentKind.Output, log)
{
    protected override async Task Run(CancellationToken token)
    {
        var cfg = settings.Heartrate; cfg.Validate(); var frame = new HeartrateFrame(cfg); var gate = new object();
        using var subscription = bus.Subscribe("", e =>
        {
            lock (gate)
            {
                if (e.Topic == "fitness/heart/bpm" && e.Value.ValueKind == JsonValueKind.Number && e.Value.TryGetDouble(out var bpm) && double.IsFinite(bpm)) frame.Receive((int)Math.Round(bpm), e.Timestamp);
                if (e.Topic == "provider/" + settings.HeartSource + "/status" && e.Value.ValueKind == JsonValueKind.String && e.Value.GetString() != "connected") frame.Disconnect();
            }
        });
        Dictionary<string, object> previous = []; var refresh = DateTimeOffset.MinValue;
        void Send(Dictionary<string, object> values, bool force)
        {
            foreach (var (key, value) in values)
                if (force || !previous.TryGetValue(key, out var old) || !Equals(old, value))
                    bus.Publish("osc/send", new { address = cfg.Address(key), type = value is float ? "f" : value is int ? "i" : "T", arguments = new[] { value } }, "heartrate-output");
            previous = values;
        }
        try
        {
            while (!token.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow; Dictionary<string, object> values;
                lock (gate) values = frame.Tick(now);
                Send(values, now >= refresh);
                if (now >= refresh) refresh = now.AddSeconds(1);
                await Task.Delay(50, token);
            }
        }
        finally { lock (gate) { frame.Disconnect(); Send(frame.Tick(DateTimeOffset.UtcNow), true); } }
    }
}
