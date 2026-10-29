namespace HydroLink.Core;
// Independent implementation of the public VRCOSC heartrate parameter contract.
public sealed class HeartrateFrame(HeartrateOptions options)
{
    private readonly Queue<(DateTimeOffset Time, int Bpm)> samples = new();
    private DateTimeOffset lastSample, lastTick, nextBeat, pulseEnd;
    private double current, average;
    private int target;
    private bool beat;
    public void Receive(int bpm, DateTimeOffset now)
    {
        if (bpm is < 20 or > 300) { Disconnect(); return; }
        target = bpm; lastSample = now; samples.Enqueue((now, bpm));
        while (samples.Count > 10000) samples.Dequeue();
    }
    public void Disconnect() { lastSample = default; target = 0; samples.Clear(); }
    public Dictionary<string, object> Tick(DateTimeOffset now)
    {
        bool connected = lastSample != default && now - lastSample < TimeSpan.FromSeconds(5);
        double dt = lastTick == default ? 50 : Math.Clamp((now - lastTick).TotalMilliseconds, 0, 1000); lastTick = now;
        while (samples.TryPeek(out var sample) && now - sample.Time >= TimeSpan.FromMilliseconds(options.AveragePeriodMs)) samples.Dequeue();
        double Move(double value, double goal, bool smooth, int ms) => !smooth || ms == 0 ? goal : value + (goal - value) * (1 - Math.Exp(-dt * 4 / ms));
        if (!connected) { current = average = 0; beat = false; nextBeat = default; samples.Clear(); }
        else
        {
            current = Move(current, target, options.SmoothValue, options.ValueSmoothingMs);
            average = Move(average, samples.Count == 0 ? 0 : samples.Average(s => s.Bpm), options.SmoothAverage, options.AverageSmoothingMs);
            if (options.PulseBeat && now >= pulseEnd) beat = false;
            if (nextBeat == default) nextBeat = now.AddSeconds(60d / target);
            if (now >= nextBeat) { beat = options.PulseBeat || !beat; pulseEnd = now.AddMilliseconds(50); nextBeat = now.AddSeconds(60d / target); }
        }
        int value = (int)Math.Round(current);
        return new()
        {
            ["Connected"] = connected, ["Enabled"] = connected, ["Value"] = value, ["Average"] = (int)Math.Round(average),
            ["Normalised"] = connected ? (float)Math.Clamp((current - options.LowerBpm) / (options.UpperBpm - options.LowerBpm), 0, 1) : 0f,
            ["Beat"] = beat, ["Units"] = value % 10 / 10f, ["Tens"] = value / 10 % 10 / 10f, ["Hundreds"] = value / 100 % 10 / 10f
        };
    }
}
