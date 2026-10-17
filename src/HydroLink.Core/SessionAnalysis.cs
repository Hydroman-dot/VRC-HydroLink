using System.Globalization;
using System.Text.Json;
using Microsoft.VisualBasic.FileIO;
namespace HydroLink.Core;

public sealed record SessionReport(string FilePath, Dictionary<string, object?> Summary, List<Dictionary<string, string>> Rows)
{
    public string Text => string.Join(Environment.NewLine, Summary.Select(p => $"{p.Key}: {p.Value}"));
}
public static class SessionAnalysis
{
    public static SessionReport Read(string path)
    {
        using var parser = new TextFieldParser(path) { TextFieldType = FieldType.Delimited, HasFieldsEnclosedInQuotes = true };
        var firstLine = File.ReadLines(path).FirstOrDefault() ?? "";
        parser.SetDelimiters(firstLine.Contains(';') ? ";" : ",");
        var header = parser.ReadFields() ?? throw new InvalidDataException("CSV-Datei ist leer");
        if (!header.Contains("timestamp")) throw new InvalidDataException("Keine VR-Fitness-CSV (timestamp fehlt)");
        var rows = new List<Dictionary<string, string>>();
        while (!parser.EndOfData)
        {
            var cells = parser.ReadFields(); if (cells is null) continue;
            if (rows.Count >= 1000000) throw new InvalidDataException("Maximal eine Million Messpunkte pro Analyse");
            rows.Add(header.Select((h, i) => (h, value: i < cells.Length ? cells[i] : "")).GroupBy(x => x.h).ToDictionary(g => g.Key, g => g.First().value));
        }
        foreach (var row in rows)
        {
            foreach (var (key, legacy) in new[] { ("clean_bpm", "bpm"), ("clean_speed_kmh", "current_speed_kmh"), ("steps_total", "steps"), ("distance_total_km", "distance_km"), ("active_time_total_s", "active_time_s") })
                if (!row.ContainsKey(key)) row[key] = row.GetValueOrDefault(legacy) ?? "";
            if (!row.ContainsKey("pulse_valid")) row["pulse_valid"] = Number(row, "bpm").HasValue.ToString();
        }
        foreach (var (key, total) in new[] { ("session_distance_km", "distance_total_km"), ("session_steps", "steps_total"), ("session_active_s", "active_time_total_s") })
        {
            var baseline = rows.Select(r => Number(r, total)).FirstOrDefault(v => v.HasValue);
            foreach (var row in rows) if (!Number(row, key).HasValue && baseline.HasValue && Number(row, total) is { } value) row[key] = Math.Max(0, value - baseline.Value).ToString(CultureInfo.InvariantCulture);
        }
        double[] Numbers(string key) => rows.Select(r => Number(r, key)).Where(x => x.HasValue).Select(x => x!.Value).ToArray();
        double? Last(string key) => rows.Select(r => Number(r, key)).LastOrDefault(x => x.HasValue);
        var heart = Numbers("clean_bpm"); var speed = Numbers("clean_speed_kmh").Where(v => v > .05).ToArray();
        var elapsed = Last("session_elapsed_s");
        if (DateTimeOffset.TryParse(rows.FirstOrDefault()?.GetValueOrDefault("timestamp"), out var start) && DateTimeOffset.TryParse(rows.LastOrDefault()?.GetValueOrDefault("timestamp"), out var end)) elapsed = Math.Max(0, (end - start).TotalSeconds);
        double Quality(string key) => rows.Count == 0 ? 0 : rows.Count(r => r.GetValueOrDefault(key)?.ToLowerInvariant() is "true" or "1") * 100d / rows.Count;
        var summary = new Dictionary<string, object?>
        {
            ["session_name"] = rows.Select(r => r.GetValueOrDefault("session_name")).FirstOrDefault(n => !string.IsNullOrWhiteSpace(n)) ?? Path.GetFileNameWithoutExtension(path),
            ["start"] = rows.FirstOrDefault()?.GetValueOrDefault("timestamp"), ["end"] = rows.LastOrDefault()?.GetValueOrDefault("timestamp"),
            ["duration_s"] = elapsed, ["active_s"] = Last("session_active_s"), ["distance_km"] = Last("session_distance_km"), ["steps"] = Last("session_steps"),
            ["avg_bpm"] = heart.Length == 0 ? null : heart.Average(), ["min_bpm"] = heart.Length == 0 ? null : heart.Min(), ["max_bpm"] = heart.Length == 0 ? null : heart.Max(),
            ["avg_active_speed_kmh"] = speed.Length == 0 ? null : speed.Average(), ["max_speed_kmh"] = speed.Length == 0 ? null : speed.Max(),
            ["pulse_connection_pct"] = Quality("pulse_ws_connected"), ["movement_connection_pct"] = Quality("vrti_connected"), ["valid_pulse_pct"] = Quality("pulse_valid"),
            ["sample_count"] = rows.Count, ["note"] = ""
        };
        summary["bpm_anomaly_count"] = rows.Count(r => r.GetValueOrDefault("bpm_anomaly")?.ToLowerInvariant() == "true");
        summary["speed_anomaly_count"] = rows.Count(r => r.GetValueOrDefault("speed_anomaly")?.ToLowerInvariant() == "true");
        var intervals = Numbers("session_elapsed_s").Zip(Numbers("session_elapsed_s").Skip(1), (a, b) => b - a).Where(d => d is > 0 and < 10).Order().ToArray();
        double step = intervals.Length == 0 ? 1 : (intervals[(intervals.Length - 1) / 2] + intervals[intervals.Length / 2]) / 2;
        foreach (var zone in new[] { "< 100", "100-119", "120-139", "140-159", "160+" }) summary["zone_seconds " + zone] = rows.Count(r => Zone(Number(r, "bpm")) == zone) * step;
        var pairs = rows.Select(r => (Speed: Number(r, "current_speed_kmh"), Heart: Number(r, "bpm"))).Where(p => p.Speed.HasValue && p.Heart.HasValue).ToArray();
        if (pairs.Length > 1)
        {
            var xs = pairs.Select(p => p.Speed!.Value).ToArray(); var ys = pairs.Select(p => p.Heart!.Value).ToArray(); double xm = xs.Average(), ym = ys.Average();
            double den = Math.Sqrt(xs.Sum(x => (x - xm) * (x - xm)) * ys.Sum(y => (y - ym) * (y - ym)));
            summary["pulse_speed_correlation"] = den <= 0 ? null : xs.Zip(ys, (x, y) => (x - xm) * (y - ym)).Sum() / den;
        }
        foreach (var candidate in new[] { Path.ChangeExtension(path, ".json"), Path.Combine(Path.GetDirectoryName(path)!, Path.GetFileNameWithoutExtension(path) + "_summary.json") })
            if (File.Exists(candidate))
            {
                using var doc = JsonDocument.Parse(File.ReadAllText(candidate));
                if (doc.RootElement.TryGetProperty("note", out var note)) summary["note"] = note.GetString();
                foreach (var key in new[] { "active_calories_kcal", "calorie_estimate_method" })
                    if (doc.RootElement.TryGetProperty(key, out var value)) summary[key] = value.Clone();
            }
        return new(path, summary, rows);
    }
    public static double? Number(Dictionary<string, string> row, string key) => double.TryParse(row.GetValueOrDefault(key), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) && double.IsFinite(number) ? number : null;
    public static string Zone(double? bpm) => bpm switch { null => "", < 100 => "< 100", < 120 => "100-119", < 140 => "120-139", < 160 => "140-159", _ => "160+" };
}
