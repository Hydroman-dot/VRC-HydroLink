using Buttplug.Client;
using Buttplug.Core.Messages;

namespace VrcHapticsBridge.Core;

public enum Channel { Vibrate, Pump }
public sealed record DeviceInfo(uint Id, string Name, bool Vibrate, bool Pump, uint TimingGap)
{
    public override string ToString() => $"{Name}  ·  #{Id}";
}

/// <summary>Extension boundary for an optional future backend; Pump always means constriction.</summary>
public interface IHapticsBackend
{
    bool Connected { get; }
    DeviceInfo? Selected { get; }
    Task SetAsync(Channel channel, double value, CancellationToken token);
    Task StopAsync(CancellationToken token);
}

public sealed partial class ButtplugBackend : IHapticsBackend
{
    private readonly ButtplugClient client = new("VRC HydroLink Haptics");
    private ButtplugClientDevice? selected;
    private ReliableWebSocketConnector? connector;
    public event Action? Changed;
    public event Action? ConnectionLost;
    public event Action<uint>? DeviceLost;
    public event Action<string>? Log;
    public bool Connected => client.Connected;
    public DeviceInfo? Selected => selected is { } device ? Describe(device) : null;
    public IReadOnlyList<DeviceInfo> Devices => client.Devices.Select(Describe).ToArray();

    public ButtplugBackend()
    {
        client.DeviceAdded += (_, e) => { Log?.Invoke($"Gerät gefunden: {e.Device.Name}"); Changed?.Invoke(); };
        client.DeviceRemoved += (_, e) =>
        {
            DeviceLost?.Invoke(e.Device.Index);
            if (selected?.Index == e.Device.Index) { selected = null; ConnectionLost?.Invoke(); }
            Log?.Invoke($"Gerät entfernt: #{e.Device.Index}");
            Changed?.Invoke();
        };
        client.ServerDisconnect += (_, _) =>
        {
            selected = null;
            ConnectionLost?.Invoke();
            Log?.Invoke("Intiface-Verbindung getrennt. Ausgänge deaktiviert.");
            Changed?.Invoke();
        };
        client.ErrorReceived += (_, e) => Log?.Invoke($"Buttplug-Fehler: {e.Exception.Message}");
        client.ScanningFinished += (_, _) => Log?.Invoke("Gerätesuche beendet.");
    }

    private static DeviceInfo Describe(ButtplugClientDevice d) =>
        new(d.Index, d.DisplayName ?? d.Name, d.HasOutput(OutputType.Vibrate), d.HasOutput(OutputType.Constrict), d.MessageTimingGap);

    public async Task ConnectAsync(string address, CancellationToken token)
    {
        if (Connected) throw new InvalidOperationException("Intiface ist bereits verbunden.");
        if (connector != null) { await connector.DisconnectAsync().ConfigureAwait(false); connector.Dispose(); }
        connector = new(new Uri(address));
        try { await client.ConnectAsync(connector, token).ConfigureAwait(false); }
        catch
        {
            await connector.DisconnectAsync().ConfigureAwait(false);
            connector.Dispose();
            connector = null;
            throw;
        }
        Log?.Invoke("Intiface verbunden (Buttplug-Protokoll v4).");
        Changed?.Invoke();
    }

    public Task ScanAsync(CancellationToken token) => client.StartScanningAsync(token);
    public Task StopScanAsync(CancellationToken token) => client.StopScanningAsync(token);

    // Caller must stop/disarm the controller before changing devices.
    public void Select(uint? id)
    {
        selected = id.HasValue ? client.Devices.FirstOrDefault(d => d.Index == id) : null;
        Changed?.Invoke();
    }

    public Task SetAsync(Channel channel, double value, CancellationToken token)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(value));
        var device = selected ?? throw new InvalidOperationException("Kein Gerät ausgewählt.");
        var type = channel == Channel.Vibrate ? OutputType.Vibrate : OutputType.Constrict;
        if (!device.HasOutput(type)) throw new NotSupportedException($"{type} wird von diesem Gerät nicht angeboten.");
        return device.RunOutputAsync(channel == Channel.Vibrate ? DeviceOutput.Vibrate.Percent(value) : DeviceOutput.Constrict.Percent(value), token);
    }

    public Task StopAsync(CancellationToken token) => selected?.StopAsync(token) ?? Task.CompletedTask;

    public async Task DisconnectAsync()
    {
        try { await client.DisconnectAsync().ConfigureAwait(false); }
        finally
        {
            if (connector != null)
            {
                await connector.DisconnectAsync().ConfigureAwait(false);
                connector.Dispose();
                connector = null;
            }
        }
        selected = null;
        Changed?.Invoke();
    }
    public void DisposeClient() => client.Dispose();
}
