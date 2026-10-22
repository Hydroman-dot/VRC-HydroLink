using VrcHapticsBridge.Core;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using HydroLink.Sdk;
using VRC.OSCQuery;

namespace HydroLink.Core;

public sealed class OscBridge(Settings settings, IDataBus bus, Action<string> log, string discoveryPrefix = "VRChat") : Worker("osc", ComponentKind.Service, log)
{
    private readonly ConcurrentDictionary<string, OSCQueryServiceProfile> profiles = new();
    private IPEndPoint? target;
    public OscMonitor Monitor { get; } = new();
    public string QueryStatus { get; private set; } = "Noch keine OSCQuery-Abfrage";
    public int ReceivePort { get; private set; }
    public int QueryPort { get; private set; }
    public string Target => Volatile.Read(ref target)?.ToString() ?? "VRChat wird gesucht …";
    protected override async Task Run(CancellationToken token)
    {
        using var udp = OpenReceiver();
        ReceivePort = ((IPEndPoint)udp.Client.LocalEndPoint!).Port;
        QueryPort = Extensions.GetAvailableTcpPort();
        using var service = new OSCQueryServiceBuilder().WithServiceName("VRC-HydroLink-" + Environment.ProcessId + "-" + Guid.NewGuid().ToString("N")[..8])
            .WithTcpPort(QueryPort).WithUdpPort(ReceivePort).Build();
        service.AddEndpoint("/avatar", "", Attributes.AccessValues.WriteOnly);
        service.AddEndpoint("/avatar/change", "s", Attributes.AccessValues.WriteOnly);
        service.AddEndpoint("/avatar/parameters", "", Attributes.AccessValues.WriteOnly);
        foreach (var parameter in new[] { "Strength", "Vibration", "Pump", "Rotation", "Motion" })
            service.AddEndpoint("/avatar/parameters/HydroLink/" + parameter, "f", Attributes.AccessValues.WriteOnly);
        service.AddEndpoint("/avatar/parameters/HydroLink/Enabled", "T", Attributes.AccessValues.WriteOnly);
        service.AddEndpoint("/avatar/parameters/HydroLink/Clockwise", "T", Attributes.AccessValues.WriteOnly);
        service.AddEndpoint("/avatar/parameters/HydroLink/Mode", "i", Attributes.AccessValues.WriteOnly);
        service.AddEndpoint(settings.WindOscAddress, "f", Attributes.AccessValues.WriteOnly);
        foreach (var address in new[] { settings.HapticsOptions.VibrateAddress, settings.HapticsOptions.PumpAddress }.Concat(settings.OscRoutes.Where(r => r.Source.StartsWith('/')).Select(r => r.Source)).Distinct().Where(a => a != settings.WindOscAddress))
            service.AddEndpoint(address, "f", Attributes.AccessValues.WriteOnly);
        foreach (var parameter in new[] { "VelocityMagnitude", "VelocityX", "VelocityY", "VelocityZ", "WinForceSensor_Angle" })
            service.AddEndpoint("/avatar/parameters/" + parameter, "f", Attributes.AccessValues.WriteOnly);
        service.AddEndpoint("/avatar/parameters/InStation", "T", Attributes.AccessValues.WriteOnly);
        service.OnOscQueryServiceAdded += p => { if (p.name.StartsWith(discoveryPrefix, StringComparison.OrdinalIgnoreCase)) profiles[p.name] = p; };
        // Publish only after the complete receive tree exists. A running VRChat may query
        // immediately on discovery and otherwise cache an empty tree for this session.
        service.StartHttpServer();
        service.SetDiscovery(new MeaModDiscovery());
        service.AdvertiseOSCQueryService(service.ServerName, QueryPort);
        service.AdvertiseOSCService(service.ServerName, ReceivePort);
        foreach (var p in service.GetOSCQueryServices().ToArray())
            if (p.name.StartsWith(discoveryPrefix, StringComparison.OrdinalIgnoreCase)) profiles[p.name] = p;
        using var session = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var outgoing = bus.Subscribe("osc/send", e =>
        {
            if (Volatile.Read(ref target) is not { } endpoint) return;
            var address = e.Value.GetProperty("address").GetString()!;
            var type = e.Value.TryGetProperty("type", out var tag) ? tag.GetString() : null;
            var values = e.Value.GetProperty("arguments").EnumerateArray().Select(x => TypedValue(x, type)).ToArray();
            var packet = OscCodec.Encode(address, values);
            udp.Send(packet, packet.Length, endpoint);
            Monitor.Message(address, values, true);
            bus.Publish("osc/sent" + address, new { address, arguments = values }, Id);
            if (values.Length > 0) bus.Publish("osc/sent-value" + address, values[0], Id);
        });
        Log($"OSCQuery aktiv · Empfang UDP {ReceivePort} · HTTP {QueryPort}");
        var discovery = Discover(service, session.Token);
        try
        {
            while (!token.IsCancellationRequested)
            {
                var packet = await udp.ReceiveAsync(token);
                try
                {
                    var messages = OscCodec.Decode(packet.Buffer);
                    foreach (var change in messages.Where(m => m.Address == "/avatar/change"))
                        if (change.Arguments.FirstOrDefault() is string id && Monitor.SetAvatar(id)) bus.Publish("osc/avatar-id", id, Id);
                    foreach (var message in messages)
                    {
                        Monitor.Message(message.Address, message.Arguments, false);
                        bus.Publish("osc/receive" + message.Address, new { address = message.Address, arguments = message.Arguments }, Id);
                        if (message.Arguments.Length > 0) bus.Publish("osc/value" + message.Address, message.Arguments[0], Id);
                    }
                }
                catch (FormatException) { /* A malformed datagram must not stop reception. */ }
            }
        }
        finally { await session.CancelAsync(); try { await discovery; } catch (OperationCanceledException) { } target = null; bus.Publish("osc/target-changed", false, Id); }
    }
    public static UdpClient OpenReceiver()
    {
        var udp = new UdpClient(AddressFamily.InterNetwork);
        udp.ExclusiveAddressUse = true;
        try { udp.Client.Bind(new IPEndPoint(IPAddress.Loopback, 9001)); return udp; }
        catch (SocketException) { udp.Dispose(); return new UdpClient(new IPEndPoint(IPAddress.Loopback, 0)); }
    }
    private static object ToOsc(JsonElement x) => x.ValueKind switch
    {
        JsonValueKind.True => true, JsonValueKind.False => false,
        JsonValueKind.String => x.GetString()!,
        JsonValueKind.Number when x.TryGetInt32(out var i) => i,
        JsonValueKind.Number => x.GetSingle(), _ => throw new FormatException("Invalid OSC value")
    };
    public static object TypedValue(JsonElement value, string? type) => type switch
    {
        "f" => (object)(float)(OscRouting.Number(value) ?? throw new FormatException("Zahl erwartet")),
        "i" => checked((int)(OscRouting.Number(value) ?? throw new FormatException("Zahl erwartet"))),
        "T" => (OscRouting.Number(value) ?? throw new FormatException("Bool/Zahl erwartet")) >= .5,
        _ => ToOsc(value)
    };
    public static IPEndPoint ParseHostInfo(JsonElement info, IPAddress fallback)
    {
        var port = info.GetProperty("OSC_PORT").GetInt32();
        if (port is < 1 or > 65535) throw new FormatException("Invalid OSC port");
        if (info.TryGetProperty("OSC_TRANSPORT", out var transport) && transport.GetString() != "UDP") throw new FormatException("OSC transport is not UDP");
        var address = info.TryGetProperty("OSC_IP", out var ip) && IPAddress.TryParse(ip.GetString(), out var parsed) && !parsed.Equals(IPAddress.Any) ? parsed : fallback;
        return new(address, port);
    }
    private async Task Discover(OSCQueryService service, CancellationToken token)
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(2) };
        while (!token.IsCancellationRequested)
        {
            IPEndPoint? found = null;
            if (settings.ManualOsc)
                found = new((await Dns.GetHostAddressesAsync(settings.ManualOscHost, token)).First(x => x.AddressFamily == AddressFamily.InterNetwork), settings.ManualOscPort);
            else
            {
                service.RefreshServices();
                foreach (var p in profiles.Values.OrderBy(x => x.name))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(await http.GetStringAsync(new UriBuilder("http", p.address.ToString(), p.port) { Query = "HOST_INFO" }.Uri, token));
                        if (!doc.RootElement.GetProperty("NAME").GetString()!.StartsWith(discoveryPrefix, StringComparison.OrdinalIgnoreCase)) continue;
                        found = ParseHostInfo(doc.RootElement, p.address);
                        try
                        {
                            var avatarAtStart = Monitor.Avatar;
                            var queryStarted = DateTimeOffset.UtcNow;
                            using var tree = JsonDocument.Parse(await http.GetStringAsync(new UriBuilder("http", p.address.ToString(), p.port).Uri, token));
                            if (avatarAtStart == Monitor.Avatar)
                            {
                                if (OscMonitor.FindAvatar(tree.RootElement) is { Length: > 0 } avatar && Monitor.SetAvatar(avatar)) bus.Publish("osc/avatar-id", avatar, Id);
                                Monitor.Query(tree.RootElement);
                                var queryAvatar = OscMonitor.FindAvatar(tree.RootElement);
                                if (!string.IsNullOrEmpty(queryAvatar))
                                    foreach (var item in OscMonitor.ParseTree(tree.RootElement).Where(x => Enum.GetValues<ToyKind>().Any(t => x.Address == "/avatar/parameters/HydroLink/" + t + "/Enabled")))
                                    {
                                        if (item.Value == "—") continue;
                                        using var value = JsonDocument.Parse(item.Value);
                                        if (value.RootElement.ValueKind == JsonValueKind.Array && value.RootElement.GetArrayLength() > 0 && OscRouting.Number(value.RootElement[0]) is { } enabled)
                                            bus.Publish("osc/avatar-gate", new { avatar = queryAvatar, queryStarted, address = item.Address, enabled }, Id);
                                    }
                            }
                            QueryStatus = "Parameter abgefragt · " + DateTimeOffset.Now.ToString("HH:mm:ss");
                        }
                        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException) { QueryStatus = "Parameterabfrage fehlgeschlagen · letzte Werte bleiben sichtbar"; bus.Publish("osc/query-lost", true, Id); }
                        break;
                    }
                    catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException or JsonException or FormatException or KeyNotFoundException) { }
                }
            }
            if (!Equals(target, found)) { Volatile.Write(ref target, found); bus.Publish("osc/target-changed", found is not null, Id); Log(found is null ? "VRChat nicht erreichbar · OSC-Ausgabe pausiert" : $"VRChat OSC-Ziel: {found}"); }
            await Task.Delay(3000, token);
        }
    }
}
