using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading.Channels;
using HydroLink.Sdk;

namespace HydroLink.WebSocketBridge;

public sealed class WebSocketBridgePlugin : IHydroLinkPlugin
{
    public string Id => "hydrolink.websocket-bridge";
    public string Name => "HydroLink WebSocket Bridge";
    public int ApiVersion => 1;
    public void Configure(IPluginContext context)
        => context.Register(new HeartRateBridge(context.Bus, context.Log));
}

internal sealed class BridgeSettings
{
    public int Port { get; set; } = 8766;
    public int StaleAfterSeconds { get; set; } = 10;

    public static BridgeSettings Load(Action<string> log)
    {
        var directory = Path.GetDirectoryName(typeof(WebSocketBridgePlugin).Assembly.Location)
                        ?? AppContext.BaseDirectory;
        var path = Path.Combine(directory, "bridge.settings.json");
        try
        {
            if (!File.Exists(path)) return new BridgeSettings();
            var settings = JsonSerializer.Deserialize<BridgeSettings>(File.ReadAllText(path))
                           ?? new BridgeSettings();
            if (settings.Port is < 1024 or > 65535)
                throw new InvalidDataException("Port must be 1024-65535.");
            if (settings.StaleAfterSeconds is < 2 or > 300)
                throw new InvalidDataException("StaleAfterSeconds must be 2-300.");
            return settings;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException or InvalidDataException)
        {
            log($"WebSocket Bridge: invalid settings, defaults used ({ex.Message}).");
            return new BridgeSettings();
        }
    }
}

/// <summary>Loopback-only, read-only bridge for the existing fitness/heart/bpm topic.</summary>
internal sealed class HeartRateBridge : IComponent
{
    private const string Topic = "fitness/heart/bpm";
    private const int MaxClients = 8;
    private readonly IDataBus _bus;
    private readonly Action<string> _log;
    private readonly ConcurrentDictionary<Guid, Client> _clients = new();
    // The HydroLink data bus is synchronous: never do network I/O in its callback.
    private readonly Channel<string> _outbox = Channel.CreateBounded<string>(
        new BoundedChannelOptions(64)
        {
            SingleReader = true,
            SingleWriter = false,
            FullMode = BoundedChannelFullMode.DropOldest
        });

    private BridgeSettings _settings = new();
    private HttpListener? _listener;
    private CancellationTokenSource? _lifetime;
    private IDisposable? _subscription;
    private Task? _acceptTask, _sendTask, _freshnessTask;
    private int _lastBpm, _staleSent;
    private long _lastReceivedUtcTicks;

    public HeartRateBridge(IDataBus bus, Action<string> log)
    {
        _bus = bus;
        _log = log;
    }

    public string Id => "hydrolink.websocket-bridge.output";
    public ComponentKind Kind => ComponentKind.Output;

    public Task StartAsync(CancellationToken cancellationToken)
    {
        if (_lifetime is not null) throw new InvalidOperationException("Already started.");
        _settings = BridgeSettings.Load(_log);
        var listener = new HttpListener();
        listener.Prefixes.Add($"http://localhost:{_settings.Port}/");
        listener.Start();
        _listener = listener;
        _lifetime = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        _subscription = _bus.Subscribe(Topic, OnData);
        var token = _lifetime.Token;
        _acceptTask = AcceptLoopAsync(listener, token);
        _sendTask = SendLoopAsync(token);
        _freshnessTask = FreshnessLoopAsync(token);
        _log($"WebSocket Bridge: ws://localhost:{_settings.Port}/hydrolink; http://localhost:{_settings.Port}/hr");
        return Task.CompletedTask;
    }

    private void OnData(DataEvent evt)
    {
        if (evt.Topic != Topic || evt.Value.ValueKind != JsonValueKind.Number ||
            !evt.Value.TryGetInt32(out int bpm) || bpm is < 1 or > 300)
            return;

        var now = DateTimeOffset.UtcNow;
        Interlocked.Exchange(ref _lastBpm, bpm);
        Interlocked.Exchange(ref _lastReceivedUtcTicks, now.UtcTicks);
        Interlocked.Exchange(ref _staleSent, 0);
        _outbox.Writer.TryWrite(JsonSerializer.Serialize(new
        {
            type = "measurement", topic = Topic, value = (int?)bpm,
            unit = "bpm", status = "live", timestamp = now
        }));
    }

    private bool TryGetFresh(out int bpm, out DateTimeOffset timestamp)
    {
        bpm = Volatile.Read(ref _lastBpm);
        long ticks = Interlocked.Read(ref _lastReceivedUtcTicks);
        timestamp = ticks > 0 ? new DateTimeOffset(ticks, TimeSpan.Zero) : default;
        return bpm > 0 && ticks > 0 &&
               DateTimeOffset.UtcNow - timestamp <= TimeSpan.FromSeconds(_settings.StaleAfterSeconds);
    }

    private async Task AcceptLoopAsync(HttpListener listener, CancellationToken token)
    {
        try
        {
            while (!token.IsCancellationRequested)
            {
                var context = await listener.GetContextAsync().WaitAsync(token);
                _ = HandleRequestAsync(context, token);
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (HttpListenerException) when (token.IsCancellationRequested) { }
        catch (ObjectDisposedException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { _log($"WebSocket Bridge listener failure: {ex.Message}"); }
    }

    private async Task HandleRequestAsync(HttpListenerContext context, CancellationToken token)
    {
        try
        {
            var request = context.Request;
            string path = request.Url?.AbsolutePath.TrimEnd('/') ?? "";
            if (request.HttpMethod == "GET" && path == "/hr")
            {
                if (TryGetFresh(out int bpm, out var timestamp))
                    await ReplyAsync(context, 200, new { bpm, measured_at = timestamp }, token);
                else
                    await ReplyAsync(context, 503, new { error = "heart_rate_unavailable" }, token);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/health")
            {
                await ReplyAsync(context, 200, new
                {
                    status = "running",
                    heartRate = TryGetFresh(out _, out _) ? "live" : "stale",
                    clients = _clients.Count
                }, token);
                return;
            }

            if (request.HttpMethod == "GET" && path == "/hydrolink" && request.IsWebSocketRequest)
            {
                if (_clients.Count >= MaxClients)
                {
                    context.Response.StatusCode = 503;
                    context.Response.Close();
                    return;
                }

                var upgraded = await context.AcceptWebSocketAsync(null);
                var client = new Client(upgraded.WebSocket);
                var id = Guid.NewGuid();
                if (!_clients.TryAdd(id, client))
                {
                    client.Dispose();
                    return;
                }

                await client.SendAsync(Snapshot(), token);
                await ReceiveClientAsync(id, client, token);
                return;
            }

            context.Response.StatusCode = 404;
            context.Response.Close();
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { _log($"WebSocket Bridge request failed: {ex.Message}"); }
    }

    private string Snapshot()
    {
        if (TryGetFresh(out int bpm, out var timestamp))
            return JsonSerializer.Serialize(new
            {
                type = "measurement", topic = Topic, value = (int?)bpm,
                unit = "bpm", status = "live", timestamp
            });
        return StaleMessage();
    }

    private static string StaleMessage() => JsonSerializer.Serialize(new
    {
        type = "status", topic = Topic, value = (int?)null,
        unit = "bpm", status = "stale", timestamp = DateTimeOffset.UtcNow
    });

    private static async Task ReplyAsync(HttpListenerContext context, int status,
        object payload, CancellationToken token)
    {
        byte[] data = JsonSerializer.SerializeToUtf8Bytes(payload);
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.ContentLength64 = data.LongLength;
        context.Response.Headers["Cache-Control"] = "no-store";
        await context.Response.OutputStream.WriteAsync(data, token);
        context.Response.Close();
    }

    private async Task ReceiveClientAsync(Guid id, Client client, CancellationToken token)
    {
        try
        {
            var buffer = new byte[512];
            while (client.Socket.State == WebSocketState.Open && !token.IsCancellationRequested)
            {
                var response = await client.Socket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), token);
                // No commands: clients only receive telemetry.
                if (response.MessageType == WebSocketMessageType.Close) break;
                break;
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (WebSocketException) { }
        catch (ObjectDisposedException) { }
        finally
        {
            _clients.TryRemove(id, out _);
            client.Dispose();
        }
    }

    private async Task SendLoopAsync(CancellationToken token)
    {
        try
        {
            await foreach (var json in _outbox.Reader.ReadAllAsync(token))
            {
                foreach (var (id, client) in _clients)
                {
                    try { await client.SendAsync(json, token); }
                    catch (OperationCanceledException) when (token.IsCancellationRequested) { return; }
                    catch (Exception ex) when (ex is WebSocketException or ObjectDisposedException or InvalidOperationException)
                    {
                        _clients.TryRemove(id, out _);
                        client.Dispose();
                    }
                }
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    private async Task FreshnessLoopAsync(CancellationToken token)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(1));
            while (await timer.WaitForNextTickAsync(token))
            {
                if (!TryGetFresh(out _, out _) &&
                    Interlocked.CompareExchange(ref _staleSent, 1, 0) == 0)
                    _outbox.Writer.TryWrite(StaleMessage());
            }
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
    }

    public async ValueTask DisposeAsync()
    {
        if (_lifetime is null) return;
        _subscription?.Dispose();
        await _lifetime.CancelAsync();
        _listener?.Stop();
        _listener?.Close();
        _outbox.Writer.TryComplete();
        foreach (var client in _clients.Values) client.Dispose();
        _clients.Clear();
        try { await Task.WhenAll(new[] { _acceptTask, _sendTask, _freshnessTask }.OfType<Task>()); }
        finally
        {
            _lifetime.Dispose();
            _lifetime = null;
        }
    }

    private sealed class Client(WebSocket socket) : IDisposable
    {
        private readonly SemaphoreSlim _writeGate = new(1, 1);
        public WebSocket Socket { get; } = socket;

        public async Task SendAsync(string json, CancellationToken token)
        {
            await _writeGate.WaitAsync(token);
            try
            {
                if (Socket.State != WebSocketState.Open) return;
                await Socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(json)),
                    WebSocketMessageType.Text, true, token);
            }
            finally { _writeGate.Release(); }
        }

        public void Dispose() => Socket.Dispose();
    }
}
