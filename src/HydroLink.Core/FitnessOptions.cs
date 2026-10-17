namespace HydroLink.Core;
public sealed class FitnessOptions
{
    public string DataDirectory { get; set; } = Path.Combine(Settings.Folder, "sessions");
    public double WeightKg { get; set; }
    public int Age { get; set; }
    public string Sex { get; set; } = "unspecified";
    public double MaxHeartJump { get; set; } = 35;
    public double MaxSpeedKmh { get; set; } = 8;
    public double StaleSeconds { get; set; } = 10;
    public bool AutoSessions { get; set; }
    public double AutoStartSeconds { get; set; } = 3;
    public double AutoStopSeconds { get; set; } = 300;
    public bool StopWithSteamVr { get; set; }
    public double WeeklyKm { get; set; } = 10;
    public double WeeklyHours { get; set; } = 5;
    public int WeeklySteps { get; set; } = 50000;
    public bool SendHealthSteps { get; set; }
    public bool SendHealthCalories { get; set; } = true;
    public static double? CaloriesPerMinute(double bpm, double weight, int age, string sex)
    {
        if (!double.IsFinite(bpm) || !double.IsFinite(weight) || bpm is < 35 or > 230 || weight is < 20 or > 300 || age is < 12 or > 100 || sex is not ("male" or "female")) return null;
        double kj = sex == "male" ? -55.0969 + .6309 * bpm + .1988 * weight + .2017 * age : -20.4022 + .4472 * bpm - .1263 * weight + .074 * age;
        return Math.Max(0, kj / 4.184);
    }
}
