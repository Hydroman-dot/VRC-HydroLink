using System.Text.Json;
namespace HydroLink.Core;

public sealed class WindConfiguration
{
    public string Movement { get; set; } = "None";
    public string Heart { get; set; } = "None";
    public string HeartMode { get; set; } = "SOURCE";
    public bool MultiPwm { get; set; }
    public List<WindRule> Rules { get; set; } = WindRule.Defaults();
    public static WindConfiguration From(Settings settings) => new()
    {
        Movement = settings.WindMovementSource, Heart = settings.WindHeartSource,
        HeartMode = settings.WindHeartMode, MultiPwm = settings.WindMultiPwm,
        Rules = Clone(settings.WindRules)
    };
    private static List<WindRule> Clone(List<WindRule> rules) => JsonSerializer.Deserialize<List<WindRule>>(JsonSerializer.Serialize(rules))!;
    public void Apply(Settings settings)
    {
        Validate(); settings.WindMovementSource = Movement; settings.WindHeartSource = Heart;
        settings.WindHeartMode = HeartMode; settings.WindMultiPwm = MultiPwm; settings.WindRules = Clone(Rules);
    }
    public void Validate()
    {
        if (Movement is not ("None" or "VRTI" or "FitOSC") || Heart is not ("None" or "Bluetooth" or "VRCOSC" or "Pulsoid" or "HypeRate") || HeartMode is not ("OFF" or "SOURCE" or "MINIMUM")) throw new ArgumentException("Ungültige Quellenwahl.");
        var names = WindRule.Defaults().Select(r => r.Source).ToHashSet();
        if (Rules.Count != names.Count || !Rules.Select(r => r.Source).ToHashSet().SetEquals(names)) throw new ArgumentException("Jede WindForce-Quelle muss genau einmal vorkommen.");
        foreach (var rule in Rules)
            if (new[] { rule.Start, rule.Mid, rule.Full, rule.MaxPwm, rule.TimeoutSeconds }.Any(n => !double.IsFinite(n)) || rule.Start >= rule.Mid || rule.Mid >= rule.Full || rule.MaxPwm is < 0 or > 100 || rule.TimeoutSeconds is < .1 or > 60 || rule.FadeMs is < 0 or > 30000 || rule.Curve is not ("INSTANT" or "LINEAR" or "SMOOTH" or "EXPONENTIAL"))
                throw new ArgumentException(rule.Source + ": Start < 50 % < 100 % erforderlich; PWM 0–100, Timeout 0,1–60 s, Ausklang 0–30000 ms.");
    }
}
