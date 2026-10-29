using System.Globalization;
namespace HydroLink.Core;
public static class DurationDisplay
{
    public static bool IsDuration(string topic) => topic is "fitness/active-seconds" or "session/active-seconds" or "session/elapsed-seconds";
    public static string Format(double seconds)
    {
        if (!double.IsFinite(seconds) || seconds < 0) return "—";
        seconds = Math.Floor(seconds);
        return string.Format(CultureInfo.InvariantCulture, "{0:0}:{1:00}:{2:00}", Math.Floor(seconds / 3600), Math.Floor(seconds / 60) % 60, seconds % 60);
    }
}
