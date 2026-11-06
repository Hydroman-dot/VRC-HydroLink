using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Channels;
using HydroLink.Sdk;
namespace HydroLink.Core;

public sealed class RouterEndpoint
{
    public string Name { get; set; } = "Programm";
    public string Host { get; set; } = "127.0.0.1";
    public int ReceivePort { get; set; }
    public int SendPort { get; set; } = 9100;
}
public sealed class RouterRule
{
    public bool Enabled { get; set; }
    public string Profile { get; set; } = "Standard";
    public string Avatar { get; set; } = "*";
    public string From { get; set; } = "Hub";
    public string To { get; set; } = "Programm";
    public string Address { get; set; } = "/avatar/parameters/*";
    public string Rename { get; set; } = "";
    public bool ConvertValue { get; set; }
    public bool Invert { get; set; }
    public double Scale { get; set; } = 1;
    public double Offset { get; set; }
    public double Min { get; set; }
    public double Max { get; set; } = 1;
    public bool Matches(string source, string address, string profile, string avatar) => Enabled && From == source && Profile == profile
        && (Avatar == "*" || Avatar == avatar) && (Address.EndsWith('*') ? address.StartsWith(Address[..^1], StringComparison.Ordinal) : address == Address);
    public OscMessage Transform(OscMessage message)
    {
        var values = message.Arguments.ToArray();
        if (ConvertValue && values.Length > 0)
        {
            var number = OscRouting.Number(JsonSerializer.SerializeToElement(values[0]));
            if (number == null) throw new FormatException("Umrechnung benötigt eine Zahl oder Bool als ersten Wert");
            var result = Math.Clamp((Invert ? 1 - number.Value : number.Value) * Scale + Offset, Min, Max);
            if (!double.IsFinite(result)) throw new FormatException("Ungültiges Umrechnungsergebnis");
            values[0] = (float)result;
        }
        return new(string.IsNullOrWhiteSpace(Rename) ? message.Address : Rename, values);
    }
}
public sealed class OscRouterOptions
{
    public string Profile { get; set; } = "Standard";
    public string ListenAddress { get; set; } = "127.0.0.1";
    public List<RouterEndpoint> Endpoints { get; set; } = [];
    public List<RouterRule> Rules { get; set; } = [];
    public OscRouterOptions Copy() => JsonSerializer.Deserialize<OscRouterOptions>(JsonSerializer.Serialize(this))!;
    public void Validate()
    {
        if (!IPAddress.TryParse(ListenAddress, out var listen) || listen.AddressFamily != AddressFamily.InterNetwork) throw new ArgumentException("Empfangs-IP muss eine IPv4-Adresse sein.");
        if (string.IsNullOrWhiteSpace(Profile) || Endpoints.Count > 32 || Rules.Count > 256) throw new ArgumentException("Profil fehlt oder zu viele Einträge (32 Endpunkte / 256 Regeln).");
        var names = new HashSet<string>(StringComparer.Ordinal) { "Hub" };
        var ports = new HashSet<int>();
        foreach (var endpoint in Endpoints)
        {
            if (string.IsNullOrWhiteSpace(endpoint.Name) || !names.Add(endpoint.Name)) throw new ArgumentException("Endpunktnamen müssen eindeutig sein; Hub ist reserviert.");
            if (!IPAddress.TryParse(endpoint.Host, out var ip) || ip.AddressFamily != AddressFamily.InterNetwork || ip.Equals(IPAddress.Any) || ip.Equals(IPAddress.Broadcast)) throw new ArgumentException("Ziel-IP muss eine konkrete IPv4-Adresse sein.");
            if (endpoint.SendPort is < 1 or > 65535 || endpoint.ReceivePort is < 0 or > 65535) throw new ArgumentException("Port muss 1–65535 sein; Empfangsport 0 deaktiviert den Eingang.");
            if (endpoint.ReceivePort != 0 && (!ports.Add(endpoint.ReceivePort) || endpoint.ReceivePort is 9000 or 9001)) throw new ArgumentException("Empfangsports müssen eindeutig sein. 9000/9001 bleiben für VRChat und andere Apps frei.");
        }
        foreach (var endpoint in Endpoints)
            if (ports.Contains(endpoint.SendPort) && (IPAddress.IsLoopback(IPAddress.Parse(endpoint.Host)) || endpoint.Host == ListenAddress)) throw new ArgumentException("Zielport darf nicht auf einen eigenen Router-Eingang zeigen.");
        foreach (var rule in Rules)
        {
            if (!names.Contains(rule.From) || !names.Contains(rule.To) || rule.From == rule.To) throw new ArgumentException("Regel benötigt zwei unterschiedliche, vorhandene Endpunkte.");
            if (string.IsNullOrWhiteSpace(rule.Profile) || string.IsNullOrWhiteSpace(rule.Avatar)) throw new ArgumentException("Regelprofil und Avatar fehlen.");
            bool Address(string a) => a.StartsWith('/') && !a.Any(char.IsWhiteSpace) && a.IndexOfAny(['*','?', '[',']','{','}',',','\0']) < 0;
            if (!Address(rule.Address.EndsWith('*') ? rule.Address[..^1] : rule.Address) || rule.Rename.Length > 0 && !Address(rule.Rename)) throw new ArgumentException("OSC-Adresse ungültig. Nur im Filter ist ein abschließendes * erlaubt.");
            if (new[] { rule.Scale, rule.Offset, rule.Min, rule.Max }.Any(n => !double.IsFinite(n) || Math.Abs(n)>1e9) || rule.Min > rule.Max) throw new ArgumentException("Ungültige Umrechnung oder Grenzen.");
        }
    }
}

public sealed class OscRouter(Settings settings, IDataBus bus, Action<string> log, Func<string>? currentAvatar = null) : Worker("osc-router", ComponentKind.Service, log)
{
    private long received, sent, dropped;
    public long Received => Interlocked.Read(ref received);
    public long Sent => Interlocked.Read(ref sent);
    public long Dropped => Interlocked.Read(ref dropped);
    public string Status { get; private set; } = "Gestoppt";
    private readonly Queue<string> history = new();
    public string[] History { get { lock (history) return history.ToArray(); } }
    private void Note(string text) { Status = text; lock(history) { history.Enqueue($"{DateTimeOffset.Now:HH:mm:ss} · {text}"); while(history.Count>100) history.Dequeue(); } }
    protected override async Task Run(CancellationToken token)
    {
        var options = settings.Router.Copy(); options.Validate();
        var queue = Channel.CreateBounded<(string Source, OscMessage Message)>(new BoundedChannelOptions(1024) { SingleReader = true, FullMode = BoundedChannelFullMode.Wait });
        var receivers = new List<UdpClient>(); var tasks = new List<Task>();
        using var session = CancellationTokenSource.CreateLinkedTokenSource(token);
        using var sender = new UdpClient(AddressFamily.InterNetwork);
        string avatar = currentAvatar?.Invoke() ?? "";
        void Enqueue(string source, OscMessage message) { Interlocked.Increment(ref received); if (!queue.Writer.TryWrite((source, message))) Interlocked.Increment(ref dropped); }
        using var subscription = bus.Subscribe("", e => {
            if (e.Topic == "osc/avatar-id") { Volatile.Write(ref avatar, e.Value.GetString() ?? ""); return; }
            if (!e.Topic.StartsWith("osc/receive/", StringComparison.Ordinal) || e.Source == Id) return;
            try { var types = e.Value.TryGetProperty("types", out var tags) ? tags : default; Enqueue("Hub", new(e.Value.GetProperty("address").GetString()!, e.Value.GetProperty("arguments").EnumerateArray().Select((v, i) => OscBridge.TypedValue(v, types.ValueKind == JsonValueKind.Array && i < types.GetArrayLength() ? types[i].GetString() : null)).ToArray())); }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException or KeyNotFoundException) { Interlocked.Increment(ref dropped); }
        });
        try
        {
            foreach (var endpoint in options.Endpoints.Where(e => e.ReceivePort != 0))
            {
                var receiver = new UdpClient(AddressFamily.InterNetwork) { ExclusiveAddressUse = true }; receivers.Add(receiver);
                receiver.Client.Bind(new IPEndPoint(IPAddress.Parse(options.ListenAddress), endpoint.ReceivePort));
                tasks.Add(Receive(receiver, endpoint));
            }
            Note("Bereit · Profil " + options.Profile);
            var echoes = new Dictionary<string, long>();
            long window = Environment.TickCount64, blockedUntil = 0; int count = 0;
            await foreach (var item in queue.Reader.ReadAllAsync(token))
            {
                var now = Environment.TickCount64;
                if (now - window >= 1000) { window = now; count = 0; }
                if (now < blockedUntil || ++count > 500) { Interlocked.Increment(ref dropped); if (now >= blockedUntil) { blockedUntil = now + 5000; Note("Schleifen-/Lastschutz · Weiterleitung für 5 s pausiert"); } continue; }
                var key = Convert.ToBase64String(OscCodec.Encode(item.Message.Address, item.Message.Arguments));
                foreach (var expired in echoes.Where(p => p.Value <= now).Select(p => p.Key).ToArray()) echoes.Remove(expired);
                if (echoes.ContainsKey(item.Source + "|" + key)) { Interlocked.Increment(ref dropped); continue; }
                foreach (var rule in options.Rules.Where(r => r.Matches(item.Source, item.Message.Address, options.Profile, Volatile.Read(ref avatar))))
                {
                    if (echoes.Count >= 4096) { Interlocked.Increment(ref dropped); Note("Echo-Speicher voll · Nachricht verworfen"); continue; }
                    try
                    {
                        var message = rule.Transform(item.Message); var packet = OscCodec.Encode(message.Address, message.Arguments);
                        if (rule.To == "Hub") bus.Publish("osc/send", new { address = message.Address, arguments = message.Arguments, types = message.Arguments.Select(OscCodec.TypeTag).ToArray() }, Id);
                        else { var destination = options.Endpoints.Single(e => e.Name == rule.To); await sender.SendAsync(packet, new IPEndPoint(IPAddress.Parse(destination.Host), destination.SendPort), token); }
                        echoes[rule.To + "|" + Convert.ToBase64String(packet)] = now + 2000;
                        Interlocked.Increment(ref sent);
                        Note($"{item.Source} → {rule.To} · {message.Address} · {JsonSerializer.Serialize(message.Arguments)}" + (rule.To == "Hub" ? " · an OSC Hub übergeben" : " · UDP gesendet"));
                    }
                    catch (Exception ex) when (ex is FormatException or SocketException or ArgumentException) { Interlocked.Increment(ref dropped); Note("Regelfehler: " + ex.Message); }
                }
            }
        }
        finally
        {
            await session.CancelAsync(); foreach(var receiver in receivers) receiver.Dispose();
            await Task.WhenAll(tasks); Note("Gestoppt");
        }
        async Task Receive(UdpClient receiver, RouterEndpoint endpoint)
        {
            try {
                while (!session.IsCancellationRequested) {
                    var packet = await receiver.ReceiveAsync(session.Token);
                    if (!packet.RemoteEndPoint.Address.Equals(IPAddress.Parse(endpoint.Host))) { Interlocked.Increment(ref dropped); continue; }
                    if (packet.Buffer.Length > 32768) { Interlocked.Increment(ref dropped); continue; }
                    try { foreach (var message in OscCodec.Decode(packet.Buffer)) Enqueue(endpoint.Name, message); }
                    catch (FormatException) { Interlocked.Increment(ref dropped); }
                }
            } catch (OperationCanceledException) when(session.IsCancellationRequested) { }
              catch (ObjectDisposedException) when(session.IsCancellationRequested) { }
              catch (SocketException ex) { Note("Empfangsfehler " + endpoint.Name + ": " + ex.Message); queue.Writer.TryComplete(ex); }
        }
    }
}
