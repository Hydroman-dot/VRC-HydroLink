using Buttplug.Core.Messages;
namespace VrcHapticsBridge.Core;

public sealed record ToyChannel(string Key, string Label, OutputType Type, int Index = 0);
public static class ToyProfiles
{
    public static bool Extended(ToyKind toy) => toy >= ToyKind.Dolce;
    public static string Label(ToyKind toy) => toy switch { ToyKind.Max2 => "Max 2", ToyKind.SolacePro => "Solace Pro", ToyKind.LushMini => "Lush Mini", _ => toy.ToString() };
    public static ToyChannel[] Channels(ToyKind toy) => toy switch {
        ToyKind.Dolce or ToyKind.Gemini => [new("Vibration1", "Vibration · Motor 1", OutputType.Vibrate), new("Vibration2", "Vibration · Motor 2", OutputType.Vibrate, 1)],
        ToyKind.Nora => [new("Vibration", "Vibration", OutputType.Vibrate), new("Rotation", "Rotation", OutputType.Rotate)],
        ToyKind.Gravity => [new("Vibration", "Vibration", OutputType.Vibrate), new("Thrusting", "Hubgeschwindigkeit", OutputType.Oscillate)],
        ToyKind.LushMini => [new("Vibration", "Vibration", OutputType.Vibrate)],
        _ => [] };
    public static HapticFeature? Feature(ToyChannel channel, IEnumerable<HapticFeature> features) => features.Where(f => f.Supported && f.Type == channel.Type).OrderBy(f => f.FeatureId).ElementAtOrDefault(channel.Index);
    public static Dictionary<string,string> Bind(ToyKind toy, IEnumerable<HapticFeature> features) => Channels(toy).Select(c => (c, f: Feature(c, features))).Where(x => x.f != null).ToDictionary(x => x.f!.Key, x => x.c.Key);
    public static bool Accepts(ToyKind toy, string channel) => channel is "Enabled" or "Manual" || toy == ToyKind.Nora && channel is "Clockwise" or "ManualClockwise" || Channels(toy).Any(c => channel == c.Key || channel == "Manual" + c.Key);
}
