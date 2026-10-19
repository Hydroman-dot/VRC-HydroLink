using Buttplug.Client;
using Buttplug.Core.Messages;
namespace VrcHapticsBridge.Core;

public sealed record HapticFeature(uint DeviceId, uint FeatureId, string DeviceName, string Description,
    OutputType Type, int Minimum, int Maximum, uint TimingGap, int DurationMin = 0, int DurationMax = 0)
{
    public string Key => $"{DeviceId}:{FeatureId}:{Type}";
    public bool Supported => Minimum <= 0 && Maximum > 0 && (Type is OutputType.Vibrate or OutputType.Constrict or OutputType.Rotate or OutputType.Oscillate
        || Type == OutputType.HwPositionWithDuration && DurationMin <= 500 && DurationMax >= 500);
    public bool Reversible => Type == OutputType.Rotate && Minimum < 0;
    public override string ToString() => $"{DeviceName} · {Description} · {Type} (#{FeatureId})" + (Supported ? "" : " · nicht angebunden");
}
public interface IUniversalHapticsBackend
{
    bool Connected { get; }
    IReadOnlyList<HapticFeature> Features { get; }
    Task SetFeatureAsync(HapticFeature feature, double amount, bool clockwise, CancellationToken token);
    Task StopDeviceAsync(uint id, CancellationToken token);
    Task SetPositionAsync(HapticFeature feature, double position, uint duration, CancellationToken token)
        => throw new NotSupportedException("Zeitgesteuerte Position nicht angebunden.");
}
public sealed partial class ButtplugBackend : IUniversalHapticsBackend
{
    public IReadOnlyList<HapticFeature> Features => client.Devices.SelectMany(d => d.Features.Values.SelectMany(f =>
        Enum.GetValues<OutputType>().Where(f.HasOutput).Select(t => {
            f.TryGetOutputRange(t, out var min, out var max);
            var duration = f.FeatureDefinition.GetOutput(t)?.Duration;
            return new HapticFeature(d.Index, f.FeatureIndex, d.DisplayName ?? d.Name, f.FeatureDescription,
                t, min, max, d.MessageTimingGap, duration?.FirstOrDefault() ?? 0, duration?.LastOrDefault() ?? 0);
        }))).ToArray();

    public Task SetFeatureAsync(HapticFeature feature, double amount, bool clockwise, CancellationToken token)
    {
        if (!double.IsFinite(amount) || amount is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(amount));
        var current = Features.FirstOrDefault(f => f.Key == feature.Key);
        if (current != feature || !feature.Supported) throw new InvalidOperationException("Gerätefunktion wurde geändert oder nicht unterstützt.");
        if (feature.Type == OutputType.Rotate && !clockwise && !feature.Reversible)
            throw new NotSupportedException("Dieses Gerät meldet keinen Richtungswechsel.");
        var d = client.Devices.Single(x => x.Index == feature.DeviceId);
        // Use exact advertised feature indices and steps, preserving a real stop value of zero.
        var steps = feature.Type == OutputType.Rotate && !clockwise
            ? -(int)Math.Round(amount * -(double)feature.Minimum)
            : (int)Math.Round(amount * feature.Maximum);
        uint? duration = feature.Type == OutputType.HwPositionWithDuration ? 500u : null;
        return d.RunOutputAsync(feature.FeatureId, new DeviceOutputCommand(feature.Type, PercentOrSteps.FromSteps(steps), duration), token);
    }
    public Task SetPositionAsync(HapticFeature feature, double position, uint duration, CancellationToken token)
    {
        if (!double.IsFinite(position) || position is < 0 or > 1 || feature.Type != OutputType.HwPositionWithDuration
            || !Features.Contains(feature) || duration < feature.DurationMin || duration > feature.DurationMax)
            throw new ArgumentException("Ungültige Position, Dauer oder geänderte Gerätefunktion.");
        var device = client.Devices.Single(d => d.Index == feature.DeviceId);
        var steps = (int)Math.Round(feature.Minimum + position * (feature.Maximum - feature.Minimum));
        return device.RunOutputAsync(feature.FeatureId, new DeviceOutputCommand(feature.Type, PercentOrSteps.FromSteps(steps), duration), token);
    }
    public Task StopDeviceAsync(uint id, CancellationToken token)
        => client.Devices.FirstOrDefault(d => d.Index == id)?.StopAsync(token) ?? Task.CompletedTask;
}
