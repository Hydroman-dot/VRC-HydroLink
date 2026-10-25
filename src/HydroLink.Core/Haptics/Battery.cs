using System.Collections.Concurrent;
using Buttplug.Client;
using Buttplug.Core.Messages;
namespace VrcHapticsBridge.Core;

public sealed record BatteryState(int? Percent, string Status, DateTimeOffset Updated);
public sealed partial class ButtplugBackend
{
    private readonly ConcurrentDictionary<ButtplugClientDevice, BatteryState> batteries = new();
    public BatteryState Battery(uint id)
    {
        var device = Connected ? client.Devices.FirstOrDefault(d => d.Index == id) : null;
        if (device == null) return new(null, "nicht verbunden", default);
        if (!device.HasInput(InputType.Battery)) return new(null, "vom Gerät nicht gemeldet", default);
        return batteries.GetValueOrDefault(device) ?? new(null, "wird abgefragt …", default);
    }
    public async Task RefreshBatteriesAsync(CancellationToken token)
    {
        if (!Connected) return;
        foreach (var device in client.Devices.Where(d => d.HasInput(InputType.Battery)).ToArray())
        {
            token.ThrowIfCancellationRequested();
            if (batteries.TryGetValue(device, out var previous) && DateTimeOffset.UtcNow - previous.Updated < TimeSpan.FromSeconds(60)) continue;
            BatteryState result;
            try
            {
                var level = await device.BatteryAsync(TimeSpan.FromSeconds(3), token).ConfigureAwait(false);
                result = double.IsFinite(level) && level is >= 0 and <= 1
                    ? new((int)Math.Round(level * 100), "aktuell", DateTimeOffset.UtcNow)
                    : new(null, "ungültiger Gerätewert", DateTimeOffset.UtcNow);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { throw; }
            catch (Exception) { result = new(null, "Abfrage fehlgeschlagen · neuer Versuch folgt", DateTimeOffset.UtcNow); }
            if (Connected && client.Devices.Any(d => ReferenceEquals(d, device))) batteries[device] = result;
        }
    }
    public async Task MonitorBatteriesAsync(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            await RefreshBatteriesAsync(token).ConfigureAwait(false);
            await Task.Delay(2000, token).ConfigureAwait(false);
        }
    }
}
