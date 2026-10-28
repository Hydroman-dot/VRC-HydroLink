using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using System.Text.Json;
using HydroLink.Sdk;
namespace HydroLink.Core;

public sealed class FitnessSession(IDataBus bus, string directory, Settings? config = null) : IAsyncDisposable
{
    private readonly FitnessOptions options = config?.FitnessOptions ?? new();
    private readonly ConcurrentDictionary<string, DataEvent> values = new();
    private CancellationTokenSource? stop;
    private Task? writer;
    private IDisposable? subscription;
    private double? baselineDistance, baselineSteps;
    private double? lastCleanHeart;
    public string? CsvPath { get; private set; }
    public string Name { get; private set; } = "";
    public DateTimeOffset Started { get; private set; }
    public double Distance { get; private set; }
    public double Steps { get; private set; }
    public double ActiveSeconds { get; private set; }
    public int Samples { get; private set; }
    public int ValidHeartSamples { get; private set; }
    public double? Calories { get; private set; }
    public string? RecordingError { get; private set; }
    public bool Running => writer is { IsCompleted: false };
    public void Start(string name)
    {
        if (Running) throw new InvalidOperationException("Session läuft bereits");
        if (stop is not null) throw new InvalidOperationException("Vorherige Session zuerst beenden");
        Name = name; Started = DateTimeOffset.Now;
        values.Clear(); baselineDistance = baselineSteps = lastCleanHeart = null;
        Distance = Steps = ActiveSeconds = 0; Samples = ValidHeartSamples = 0; Calories = null; RecordingError = null;
        Directory.CreateDirectory(directory);
        CsvPath = Path.Combine(directory, $"{DateTime.Now:yyyyMMdd_HHmmss}_{Guid.NewGuid():N}.csv");
        // Open synchronously so permissions and disk errors reach the Start caller.
        var file = new StreamWriter(new FileStream(CsvPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read), new UTF8Encoding(true));
        stop = new(); subscription = bus.Subscribe("", e => { if (e.Topic.StartsWith("fitness/") || (e.Topic.StartsWith("provider/") && e.Topic.EndsWith("/status"))) values[e.Topic] = e; });
        writer = Task.Run(async () =>
        {
            try { await Record(file, stop.Token); }
            catch (Exception ex) { RecordingError = ex.Message; }
        });
    }
    private double? Number(string topic)
    {
        if (!values.TryGetValue("fitness/" + topic, out var value) || DateTimeOffset.UtcNow - value.Timestamp > TimeSpan.FromSeconds(options.StaleSeconds)) return null;
        return value.Value.ValueKind == JsonValueKind.Number && value.Value.TryGetDouble(out var number) ? number : null;
    }
    private static string Cell(object? value)
    {
        var text = value is IFormattable f ? f.ToString(null, CultureInfo.InvariantCulture) : value?.ToString() ?? "";
        return "\"" + text.Replace("\"", "\"\"") + "\"";
    }
    private async Task Record(StreamWriter output, CancellationToken token)
    {
        using var file = output;
        await file.WriteLineAsync("timestamp;session_name;session_elapsed_s;pulse_source;movement_source;bpm;clean_bpm;bpm_anomaly;pulse_valid;pulse_age_s;pulse_zone;pulse_ws_connected;vrti_connected;treadmill_connected;treadmill_running;current_speed_kmh;clean_speed_kmh;speed_anomaly;target_speed_kmh;speed_limit_kmh;steps_total;distance_total_km;active_time_total_s;session_steps;session_distance_km;session_active_s;auto_walk_speed");
        var previous = DateTimeOffset.UtcNow;
        try
        {
            while (!token.IsCancellationRequested)
            {
                var now = DateTimeOffset.UtcNow; double delta = Math.Clamp((now - previous).TotalSeconds, 0, 5); previous = now;
                var bpm = Number("heart/bpm"); var speed = Number("speed-kmh"); var distance = Number("distance-km"); var steps = Number("steps");
                var cleanBpm = bpm.HasValue && lastCleanHeart.HasValue && Math.Abs(bpm.Value - lastCleanHeart.Value) > options.MaxHeartJump ? null : bpm;
                if (cleanBpm.HasValue) lastCleanHeart = cleanBpm;
                var cleanSpeed = speed < 0 || speed > options.MaxSpeedKmh ? null : speed;
                if (cleanBpm is { } heart && FitnessOptions.CaloriesPerMinute(heart, options.WeightKg, options.Age, options.Sex) is { } calories) Calories = Math.Min(9999, (Calories ?? 0) + calories * delta / 60);
                baselineDistance ??= distance; baselineSteps ??= steps;
                if (distance.HasValue && baselineDistance.HasValue) Distance = Math.Max(Distance, Math.Max(0, distance.Value - baselineDistance.Value));
                if (steps.HasValue && baselineSteps.HasValue) Steps = Math.Max(Steps, Math.Max(0, steps.Value - baselineSteps.Value));
                if (speed > .05) ActiveSeconds += delta;
                Samples++; if (bpm.HasValue) ValidHeartSamples++;
                var elapsed = (now - Started).TotalSeconds;
                values.TryGetValue("fitness/heart/bpm", out var heartEvent); values.TryGetValue("fitness/speed-kmh", out var speedEvent);
                bool pulseConnected = heartEvent is not null && values.TryGetValue("provider/" + heartEvent.Source + "/status", out var ps) && ps.Value.GetString() == "connected";
                bool movementConnected = speedEvent is not null && values.TryGetValue("provider/" + speedEvent.Source + "/status", out var ms) && ms.Value.GetString() == "connected";
                values.TryGetValue("fitness/connected", out var connected); values.TryGetValue("fitness/running", out var running);
                await file.WriteLineAsync(string.Join(';', new object?[] { now.ToString("O"), Name, elapsed, heartEvent?.Source, speedEvent?.Source, bpm, cleanBpm, bpm.HasValue && !cleanBpm.HasValue, bpm.HasValue, heartEvent is null ? null : (now - heartEvent.Timestamp).TotalSeconds, SessionAnalysis.Zone(cleanBpm), pulseConnected, movementConnected, connected?.Value.ToString(), running?.Value.ToString(), speed, cleanSpeed, speed.HasValue && !cleanSpeed.HasValue, Number("target-speed-kmh"), Number("speed-limit-kmh"), steps, distance, Number("active-seconds"), Steps, Distance, ActiveSeconds, Number("autowalk-speed") }.Select(Cell)));
                await file.FlushAsync(token);
                bus.Publish("session/distance-km", Distance, "fitness-session"); bus.Publish("session/steps", Steps, "fitness-session"); bus.Publish("session/active-seconds", ActiveSeconds, "fitness-session"); bus.Publish("session/elapsed-seconds", elapsed, "fitness-session"); bus.Publish("session/heart-quality", 100d * ValidHeartSamples / Samples, "fitness-session");
                if (Calories.HasValue) bus.Publish("session/calories-estimate", Calories.Value, "fitness-session");
                await Task.Delay(1000, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }
    public async Task StopAsync(string note = "")
    {
        if (stop is null) return;
        await stop.CancelAsync();
        try { if (writer is not null) await writer; }
        finally { subscription?.Dispose(); stop.Dispose(); stop = null; }
        var summary = SessionAnalysis.Read(CsvPath!).Summary;
        summary["note"] = note; summary["active_calories_kcal"] = Calories;
        summary["calorie_estimate_method"] = Calories.HasValue ? "Keytel 2005 – Schätzung" : null;
        await File.WriteAllTextAsync(Path.ChangeExtension(CsvPath!, ".json"), JsonSerializer.Serialize(summary, new JsonSerializerOptions { WriteIndented = true }));
        if (config?.Companion == true) await HealthSync.QueueAsync(CsvPath!, config, Calories, note);
    }
    public async ValueTask DisposeAsync() => await StopAsync();
}
