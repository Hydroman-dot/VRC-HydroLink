using System.Collections.Concurrent;
using System.IO;
using HydroLink.Core;
using HydroLink.Sdk;
using Windows.Devices.Bluetooth;
using Windows.Devices.Bluetooth.Advertisement;
using Windows.Devices.Bluetooth.GenericAttributeProfile;
using Windows.Storage.Streams;
namespace HydroLink.App;

public sealed record HeartSensor(ulong Address, string Name)
{
    public override string ToString() => $"{Name} · {Address:X12}";
}
public sealed class BluetoothHeartSource(Settings settings, IDataBus bus, Action<string> log) : Worker("Bluetooth", ComponentKind.Source, log)
{
    private static readonly Guid HeartService = Guid.Parse("0000180d-0000-1000-8000-00805f9b34fb");
    private static readonly Guid Measurement = Guid.Parse("00002a37-0000-1000-8000-00805f9b34fb");
    public static async Task<HeartSensor[]> ScanAsync(CancellationToken token)
    {
        var found = new ConcurrentDictionary<ulong, HeartSensor>();
        var watcher = new BluetoothLEAdvertisementWatcher { ScanningMode = BluetoothLEScanningMode.Active };
        watcher.Received += (_, e) =>
        {
            var name = e.Advertisement.LocalName;
            if (e.Advertisement.ServiceUuids.Contains(HeartService) || name.Contains("H9Z", StringComparison.OrdinalIgnoreCase) || name.Contains("COOSPO", StringComparison.OrdinalIgnoreCase))
            {
                if (found.Count >= 128 && !found.ContainsKey(e.BluetoothAddress)) return;
                found.AddOrUpdate(e.BluetoothAddress, new HeartSensor(e.BluetoothAddress, string.IsNullOrWhiteSpace(name) ? "Pulssensor" : name), (_, old) => string.IsNullOrWhiteSpace(name) ? old : new(e.BluetoothAddress, name));
            }
        };
        BluetoothError? failure = null;
        watcher.Stopped += (_, e) => { if (e.Error != BluetoothError.Success) failure = e.Error; };
        try { watcher.Start(); await Task.Delay(TimeSpan.FromSeconds(8), token); }
        finally { watcher.Stop(); }
        if (failure.HasValue) throw new InvalidOperationException("Bluetooth-Suche: " + failure.Value);
        return found.Values.OrderByDescending(x => x.Name.Contains("H9Z", StringComparison.OrdinalIgnoreCase)).ThenBy(x => x.Name).ToArray();
    }
    private void Status(string state, string detail)
    { bus.Publish("provider/Bluetooth/status", state, "Bluetooth"); bus.Publish("bluetooth/status", detail, "Bluetooth"); }
    protected override async Task Run(CancellationToken token)
    {
        var address = settings.Heartrate.BluetoothAddress;
        if (address == 0)
        { Status("not-configured", "Kein Sensor ausgewählt · unter Einstellungen → Puls & Avatar auswählen und speichern."); return; }
        try
        {
            while (!token.IsCancellationRequested)
            {
                BluetoothLEDevice? device = null; GattDeviceService? service = null; GattCharacteristic? characteristic = null;
                int receiving = 0; long last = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                void Changed(GattCharacteristic sender, GattValueChangedEventArgs args)
                {
                    if (token.IsCancellationRequested || Volatile.Read(ref receiving) == 0) return;
                    try
                    {
                        using var reader = DataReader.FromBuffer(args.CharacteristicValue);
                        var bytes = new byte[reader.UnconsumedBufferLength]; reader.ReadBytes(bytes);
                        var measurement = BleHeartMeasurement.Parse(bytes);
                        Interlocked.Exchange(ref last, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                        if (measurement.Contact == false || measurement.Bpm is < 20 or > 300)
                        { Status("no-signal", "Verbunden · kein gültiger Hautkontakt/Puls"); return; }
                        Status("connected", "Verbunden · " + settings.Heartrate.DeviceName);
                        bus.Publish("sources/Bluetooth/heart/bpm", measurement.Bpm, "Bluetooth");
                        if (settings.HeartSource == "Bluetooth") bus.Publish("fitness/heart/bpm", measurement.Bpm, "Bluetooth");
                        bus.Publish("bluetooth/rr-seconds", measurement.RrSeconds, "Bluetooth");
                        if (measurement.Contact is { } contact) bus.Publish("bluetooth/contact", contact, "Bluetooth");
                        if (measurement.EnergyKj is { } energy) bus.Publish("bluetooth/energy-kj", energy, "Bluetooth");
                    }
                    catch (FormatException) { Status("invalid-data", "Ungültige Bluetooth-Pulsmeldung; warte auf nächste Messung"); }
                    catch (Exception ex) { Status("error", "Bluetooth-Messung: " + ex.GetType().Name); }
                }
                try
                {
                    Status("connecting", "Verbinde mit " + settings.Heartrate.DeviceName + " …");
                    using var setup = CancellationTokenSource.CreateLinkedTokenSource(token);
                    setup.CancelAfter(TimeSpan.FromSeconds(45));
                    // Windows can show device consent; device creation must run on the UI dispatcher.
                    device = await System.Windows.Application.Current.Dispatcher.InvokeAsync(() => BluetoothLEDevice.FromBluetoothAddressAsync(address).AsTask(setup.Token)).Task.Unwrap();
                    token.ThrowIfCancellationRequested();
                    if (device == null) throw new IOException("Sensor nicht erreichbar oder Zugriff nicht erlaubt");
                    var services = await device.GetGattServicesForUuidAsync(HeartService, BluetoothCacheMode.Uncached).AsTask(setup.Token);
                    if (services.Status != GattCommunicationStatus.Success) throw new IOException("Heart-Rate-Dienst: " + services.Status);
                    service = services.Services.FirstOrDefault();
                    foreach (var extra in services.Services.Skip(1)) extra.Dispose();
                    if (service == null) throw new IOException("Sensor meldet keinen Bluetooth-Heart-Rate-Dienst");
                    var chars = await service.GetCharacteristicsForUuidAsync(Measurement, BluetoothCacheMode.Uncached).AsTask(setup.Token);
                    if (chars.Status != GattCommunicationStatus.Success) throw new IOException("Pulsmessung: " + chars.Status);
                    characteristic = chars.Characteristics.FirstOrDefault() ?? throw new IOException("Heart-Rate-Messwert fehlt");
                    Status("waiting", "Verbunden · warte auf erste Pulsmessung. Sensor tragen und Elektroden prüfen.");
                    characteristic.ValueChanged += Changed; Volatile.Write(ref receiving, 1);
                    var subscribed = await characteristic.WriteClientCharacteristicConfigurationDescriptorAsync(GattClientCharacteristicConfigurationDescriptorValue.Notify).AsTask(setup.Token);
                    if (subscribed != GattCommunicationStatus.Success) throw new IOException("Pulsmeldungen abonnieren: " + subscribed);
                    Interlocked.Exchange(ref last, DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
                    setup.CancelAfter(Timeout.InfiniteTimeSpan);
                    while (!token.IsCancellationRequested)
                    {
                        await Task.Delay(1000, token);
                        if (device.ConnectionStatus != BluetoothConnectionStatus.Connected)
                            throw new IOException("Bluetooth-Verbindung zum Sensor getrennt");
                        var age = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - Interlocked.Read(ref last);
                        if (age > 30000) throw new IOException("Verbunden, aber seit 30 Sekunden keine Pulsmeldung");
                        if (age > 5000) Status("no-signal", "Verbunden · keine aktuelle Pulsmessung. Sitz/Hautkontakt prüfen; Wiederverbindung nach 30 s ohne Daten.");
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (OperationCanceledException) { Status("disconnected", "Bluetooth-Verbindungsaufbau dauert zu lange · neuer Versuch in 5 s"); }
                catch (Exception ex) { Status("disconnected", ex.Message + " · neuer Versuch in 5 s"); }
                finally
                {
                    Volatile.Write(ref receiving, 0);
                    if (characteristic != null) characteristic.ValueChanged -= Changed;
                    service?.Dispose(); device?.Dispose();
                }
                await Task.Delay(5000, token);
            }
        }
        finally { Status("disconnected", "Bluetooth-Puls gestoppt"); }
    }
}
