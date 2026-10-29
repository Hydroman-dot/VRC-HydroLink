using System.Text.Json;
namespace HydroLink.Core;

// The state and its outbox are replaced together, so retries keep the same session/sequence.
public sealed class HomeAssistantOutbox(string path, string destination)
{
    private static readonly object Gate = new();
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower };
    public sealed record Sample(double StepsSession, double DistanceSession, double CaloriesSession, double Speed, double SessionDuration, double? HeartRate);
    public sealed record Entry(int SchemaVersion, string SessionId, int Sequence, string EventType, bool Active,
        DateTimeOffset Timestamp, DateTimeOffset StartedAt, string Source, double? HeartRate,
        long StepsSession, double DistanceSession, double CaloriesSession, double Speed, double SessionDuration,
        long StepsToday, double CaloriesToday);
    public sealed class State
    {
        public string Destination { get; set; } = "";
        public List<Entry> Pending { get; set; } = [];
        public Dictionary<string, Entry> Sessions { get; set; } = [];
        public Dictionary<string, Daily> Days { get; set; } = [];
    }
    public sealed record Daily(long Steps, double Calories);
    public static string PathFor(Settings settings) => System.IO.Path.Combine(settings.FitnessOptions.DataDirectory, "home-assistant", "outbox.json");
    public static HomeAssistantOutbox For(Settings settings) => new(PathFor(settings), $"{settings.HomeAssistant.Host.Trim().ToLowerInvariant()}:{settings.HomeAssistant.Port}");
    private State Read()
    {
        var state = File.Exists(path) ? JsonSerializer.Deserialize<State>(File.ReadAllText(path), Json) ?? throw new IOException("MQTT-Puffer ist leer oder beschädigt") : new State();
        if (state.Destination != destination && state.Pending.Count > 0)
            throw new InvalidOperationException("MQTT-Puffer gehört zu einem anderen Broker. Zuerst mit dem bisherigen Broker synchronisieren.");
        state.Destination = destination;
        return state;
    }
    private void Save(State state)
    {
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(System.IO.Path.GetFullPath(path))!);
        var temp = path + ".tmp";
        using (var file = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
        { JsonSerializer.Serialize(file, state, Json); file.Flush(true); }
        File.Move(temp, path, true);
    }
    public int Count { get { lock (Gate) return Read().Pending.Count; } }
    public Entry? Next() { lock (Gate) return Read().Pending.FirstOrDefault(); }
    public static string Serialize(Entry entry) => JsonSerializer.Serialize(entry, Json);
    public Entry Append(string id, string kind, Sample sample, DateTimeOffset timestamp)
    {
        lock (Gate)
        {
            var state = Read(); var entry = Append(state, id, kind, sample, timestamp); Save(state); return entry;
        }
    }
    private static Entry Append(State state, string id, string kind, Sample sample, DateTimeOffset timestamp)
    {
        if (kind is not ("start" or "update" or "end")) throw new ArgumentException("Invalid session event");
        var old = state.Sessions.GetValueOrDefault(id);
        if (old?.EventType == "end") return old;
        if (old == null && kind != "start") throw new InvalidOperationException("Session start missing");
        foreach (var value in new[] { sample.StepsSession, sample.DistanceSession, sample.CaloriesSession, sample.Speed, sample.SessionDuration })
            if (!double.IsFinite(value) || value < 0) throw new ArgumentException("Invalid session metric");
        var steps = (long)Math.Max(Math.Min(sample.StepsSession, long.MaxValue / 2), old?.StepsSession ?? 0);
        var calories = Math.Max(sample.CaloriesSession, old?.CaloriesSession ?? 0);
        var day = timestamp.ToLocalTime().ToString("yyyy-MM-dd"); var totals = state.Days.GetValueOrDefault(day) ?? new Daily(0, 0);
        totals = new(totals.Steps + steps - (old?.StepsSession ?? 0), totals.Calories + calories - (old?.CaloriesSession ?? 0));
        var entry = new Entry(1, id, (old?.Sequence ?? -1) + 1, kind, kind != "end", timestamp, old?.StartedAt ?? timestamp,
            "HydroLink", sample.HeartRate is > 0 && double.IsFinite(sample.HeartRate.Value) ? sample.HeartRate : null,
            steps, Math.Max(sample.DistanceSession, old?.DistanceSession ?? 0), calories, sample.Speed,
            Math.Max(sample.SessionDuration, old?.SessionDuration ?? 0), totals.Steps, totals.Calories);
        state.Pending.Add(entry); state.Sessions[id] = entry; state.Days[day] = totals;
        return entry;
    }
    public void Recover()
    {
        lock (Gate)
        {
            var state = Read();
            foreach (var entry in state.Sessions.Values.Where(e => e.Active).ToArray())
                Append(state, entry.SessionId, "end", new(entry.StepsSession, entry.DistanceSession, entry.CaloriesSession, 0, entry.SessionDuration, null), entry.Timestamp);
            Save(state);
        }
    }
    public bool Acknowledge(string payload)
    {
        using var doc = JsonDocument.Parse(payload); var ack = doc.RootElement;
        if (ack.ValueKind != JsonValueKind.Object || !ack.TryGetProperty("status", out var status) || status.GetString() != "stored" ||
            !ack.TryGetProperty("test", out var test) || test.ValueKind != JsonValueKind.False ||
            !ack.TryGetProperty("session_id", out var id) || id.ValueKind != JsonValueKind.String ||
            !ack.TryGetProperty("sequence", out var seq) || !seq.TryGetInt32(out var sequence)) return false;
        lock (Gate)
        {
            var state = Read(); var removed = state.Pending.RemoveAll(e => e.SessionId == id.GetString() && e.Sequence == sequence);
            if (removed > 0) Save(state); return removed > 0;
        }
    }
}
