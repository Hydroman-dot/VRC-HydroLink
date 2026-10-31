using System.Globalization;
namespace HydroLink.Core;

public sealed record SessionChartPoint(double Seconds, double? Value);
public static class SessionCharts
{
    public static IReadOnlyList<SessionChartPoint> Points(SessionReport report, string key)
    {
        DateTimeOffset? start = null;
        var result = new List<SessionChartPoint>();
        double previous = 0;
        foreach (var row in report.Rows)
        {
            double? seconds = null;
            if (DateTimeOffset.TryParse(row.GetValueOrDefault("timestamp"), CultureInfo.InvariantCulture, DateTimeStyles.None, out var timestamp))
            {
                start ??= timestamp;
                seconds = (timestamp - start.Value).TotalSeconds;
            }
            seconds ??= SessionAnalysis.Number(row, "session_elapsed_s");
            if (seconds is null || seconds < 0 || seconds < previous) continue;
            var value = SessionAnalysis.Number(row, key);
            if (value < 0 || key == "clean_bpm" && (value == 0 || row.GetValueOrDefault("pulse_valid")?.ToLowerInvariant() is "false" or "0")) value = null;
            result.Add(new(seconds.Value, value)); previous = seconds.Value;
        }
        return result;
    }
}
