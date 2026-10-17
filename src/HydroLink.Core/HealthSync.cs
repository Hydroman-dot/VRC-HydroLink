using System.Net.Http.Json;
using System.Text.Json;
namespace HydroLink.Core;
public static class HealthSync
{
    public static string QueueFolder(Settings settings) => Path.Combine(settings.FitnessOptions.DataDirectory, "health-queue");
    private static readonly SemaphoreSlim SyncGate = new(1, 1);
    public static async Task QueueAsync(string csvPath, Settings settings, double? calories, string note)
    {
        var report = SessionAnalysis.Read(csvPath);
        var samples = new List<object>(); DateTimeOffset? previous = null;
        foreach (var row in report.Rows)
            if (DateTimeOffset.TryParse(row.GetValueOrDefault("timestamp"), out var timestamp) && SessionAnalysis.Number(row, "clean_bpm") is { } bpm && (previous is null || timestamp - previous >= TimeSpan.FromSeconds(15)))
            { samples.Add(new { timestamp = timestamp.ToString("O"), bpm = (int)Math.Round(bpm) }); previous = timestamp; }
        var folder = QueueFolder(settings); Directory.CreateDirectory(folder);
        var payload = new
        {
            protocol_version = 1, session_id = Path.GetFileNameWithoutExtension(csvPath), session_name = report.Summary["session_name"], start = report.Summary["start"], end = report.Summary["end"], note,
            distance_km = report.Summary["distance_km"], steps = report.Summary["steps"], write_steps = settings.FitnessOptions.SendHealthSteps,
            active_calories_kcal = settings.FitnessOptions.SendHealthCalories ? calories : null,
            calorie_estimate_method = calories.HasValue ? "Keytel 2005 (Puls + Gewicht + Alter + Geschlecht)" : null,
            heart_rate_samples = samples.Where((_, i) => i % Math.Max(1, (int)Math.Ceiling(samples.Count / 20000d)) == 0), source = "VR Fitness - VRTI und Heartrate", app_version = "HydroLink 0.3.3"
        };
        var path = Path.Combine(folder, payload.session_id + ".json");
        await File.WriteAllTextAsync(path + ".tmp", JsonSerializer.Serialize(payload)); File.Move(path + ".tmp", path, true);
    }
    public static async Task<int> SyncAsync(Settings settings, HttpClient http, CancellationToken token)
    {
        if (!await SyncGate.WaitAsync(0, token)) return 0;
        try
        {
            var folder = QueueFolder(settings); if (!Directory.Exists(folder)) return 0; int sent = 0;
            foreach (var path in Directory.EnumerateFiles(folder, "*.json").Order())
            {
                using var payload = JsonDocument.Parse(await File.ReadAllTextAsync(path, token));
                using var response = await http.PostAsJsonAsync("/session", payload.RootElement, token); response.EnsureSuccessStatusCode();
                using var answer = JsonDocument.Parse(await response.Content.ReadAsStringAsync(token));
                if (!answer.RootElement.TryGetProperty("ok", out var ok) || ok.ValueKind != JsonValueKind.True) throw new InvalidOperationException("Companion hat die Session nicht bestätigt");
                File.Delete(path); sent++;
            }
            return sent;
        }
        finally { SyncGate.Release(); }
    }
}
