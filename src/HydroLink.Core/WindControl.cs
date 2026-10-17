namespace HydroLink.Core;
public sealed class WindRule
{
    public string Source { get; set; } = "OSC";
    public bool Enabled { get; set; }
    public double Start { get; set; }
    public double Mid { get; set; } = .5;
    public double Full { get; set; } = 1;
    public double MaxPwm { get; set; } = 100;
    public double TimeoutSeconds { get; set; } = 3;
    public string Curve { get; set; } = "SMOOTH";
    public int FadeMs { get; set; } = 900;
    public static List<WindRule> Defaults() =>
    [
        new() { Source = "Direct OSC", Enabled = true, TimeoutSeconds = 2, FadeMs = 0 },
        new() { Source = "PhysBone", Start = .05, Mid = .525, Full = 1, TimeoutSeconds = 1.5 },
        new() { Source = "Heart Rate", Start = 90, Mid = 120, Full = 150, TimeoutSeconds = 5, FadeMs = 2500 },
        new() { Source = "AutoWalk", Mid = 3, Full = 6 },
        new() { Source = "FitOSC Walking", Mid = .5, Full = 1 },
        new() { Source = "Treadmill", Mid = 3, Full = 6, FadeMs = 1200 },
        new() { Source = "VRChat Velocity", Start = .2, Mid = 3.15, Full = 5.4, TimeoutSeconds = 5, Curve = "EXPONENTIAL", FadeMs = 750 }
    ];
    public double Map(double value)
    {
        if (!double.IsFinite(value) || value <= Start) return 0;
        var percent = value <= Mid ? (value - Start) / Math.Max(.000001, Mid - Start) * 50 : 50 + (value - Mid) / Math.Max(.000001, Full - Mid) * 50;
        return Math.Clamp(percent, 0, 100) * Math.Clamp(MaxPwm, 0, 100) / 100;
    }
    public static double Fade(string curve, double progress)
    {
        double p = Math.Clamp(progress, 0, 1);
        return curve switch { "INSTANT" => 1, "LINEAR" => p, "EXPONENTIAL" => (1 - Math.Exp(-5 * p)) / (1 - Math.Exp(-5)), _ => p * p * (3 - 2 * p) };
    }
}
