using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace VrcHapticsBridge.Tests;

// Actual WebSocket server: exercises the shipped NuGet client, not a mock of its API.
internal sealed class FakeServer : IAsyncDisposable
{
    private readonly WebApplication app;
    private WebSocket? socket;
    private readonly SemaphoreSlim sendGate = new(1);
    public ConcurrentQueue<JsonElement> Messages { get; } = new();
    public string Url => app.Urls.Single().Replace("http://", "ws://");
    public bool RejectOutputs { get; set; }
    public bool IgnoreOutputs { get; set; }
    public int ProtocolMajor { get; set; } = 4;
    public bool FragmentReplies { get; set; }
    public bool UniversalDevices { get; set; }
    public bool BatteryDevice { get; set; }
    public Task DropAsync() => socket!.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "simulated loss", CancellationToken.None);

    public FakeServer()
    {
        var builder = WebApplication.CreateBuilder();
        builder.Logging.ClearProviders();
        builder.WebHost.ConfigureKestrel(k => k.Listen(IPAddress.Loopback, 0));
        app = builder.Build();
        app.UseWebSockets();
        app.Run(HandleAsync);
    }

    public Task StartAsync() => app.StartAsync();

    private async Task HandleAsync(HttpContext context)
    {
        if (!context.WebSockets.IsWebSocketRequest) { context.Response.StatusCode = 400; return; }
        using var ws = await context.WebSockets.AcceptWebSocketAsync();
        socket = ws;
        var buffer = new byte[65536];
        try
        {
            while (ws.State == WebSocketState.Open)
            {
                using var body = new MemoryStream();
                WebSocketReceiveResult received;
                do
                {
                    received = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), context.RequestAborted);
                    if (received.MessageType == WebSocketMessageType.Close)
                    {
                        await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "done", CancellationToken.None);
                        return;
                    }
                    body.Write(buffer, 0, received.Count);
                } while (!received.EndOfMessage);
                using var json = JsonDocument.Parse(body.ToArray());
                foreach (var item in json.RootElement.EnumerateArray())
                {
                    var property = item.EnumerateObject().Single();
                    Messages.Enqueue(item.Clone());
                    var id = property.Value.GetProperty("Id").GetInt32();
                    object response = property.Name switch
                    {
                        "RequestServerInfo" => new { ServerInfo = new { Id = id, ServerName = "Integration fixture", ProtocolVersionMajor = ProtocolMajor, ProtocolVersionMinor = 0, MaxPingTime = 0 } },
                        "RequestDeviceList" => DeviceList(id),
                        "InputCmd" => new { InputReading = new { Id = id, DeviceIndex = 7, FeatureIndex = 30, Reading = new { Battery = new { Value = 73.0 } } } },
                        "OutputCmd" when RejectOutputs => new { Error = new { Id = id, ErrorCode = 3, ErrorMessage = "Injected output failure" } },
                        _ => new { Ok = new { Id = id } }
                    };
                    if (property.Name == "OutputCmd" && IgnoreOutputs) continue;
                    await SendAsync(response);
                }
            }
        }
        catch (WebSocketException) { }
        catch (OperationCanceledException) { }
        finally { socket = null; }
    }

    private object DeviceList(int id) => new
    {
        DeviceList = new
        {
            Id = id,
            Devices = new Dictionary<string, object>
            {
                ["7"] = new { DeviceName = "Lovense Max (simulated)", DeviceIndex = 7, DeviceMessageTimingGap = 0, DeviceFeatures = Features(true) },
                ["8"] = new { DeviceName = "Vibration only (simulated)", DeviceIndex = 8, DeviceMessageTimingGap = 0, DeviceFeatures = Features(false) },
                ["9"] = new { DeviceName = "Multi motor and rotation (simulated)", DeviceIndex = 9, DeviceMessageTimingGap = 0, DeviceFeatures = UniversalDevices ? UniversalFeatures(9) : Features(false) },
                ["10"] = new { DeviceName = "Oscillation (simulated)", DeviceIndex = 10, DeviceMessageTimingGap = 0, DeviceFeatures = UniversalDevices ? UniversalFeatures(10) : Features(false) },
                ["11"] = new { DeviceName = "Linear (simulated)", DeviceIndex = 11, DeviceMessageTimingGap = 0, DeviceFeatures = UniversalDevices ? UniversalFeatures(11) : Features(false) },
                ["12"] = new { DeviceName = "Unsupported context (simulated)", DeviceIndex = 12, DeviceMessageTimingGap = 0, DeviceFeatures = UniversalDevices ? UniversalFeatures(12) : Features(false) }
            }
        }
    };

    private static Dictionary<string, object> UniversalFeatures(int device)
    {
        object Feature(int index, string type, int min, int max, bool duration = false) => new {
            FeatureIndex = index, FeatureDescription = type,
            Output = new Dictionary<string, object> { [type] = duration ? new { Value = new[] { min, max }, Duration = new[] { 100, 1000 } } : new { Value = new[] { min, max } } }
        };
        return device switch {
            9 => new() { ["2"] = Feature(2, "Vibrate", 0, 20), ["5"] = Feature(5, "Vibrate", 0, 10), ["15"] = Feature(15, "Rotate", -20, 20) },
            10 => new() { ["17"] = Feature(17, "Oscillate", 0, 100) },
            11 => new() { ["21"] = Feature(21, "HwPositionWithDuration", 0, 100, true) },
            _ => new() { ["23"] = Feature(23, "Temperature", 0, 10) }
        };
    }

    private Dictionary<string, object> Features(bool pump)
    {
        var features = new Dictionary<string, object>
        {
            ["2"] = new { FeatureIndex = 2, FeatureDescription = "Vibrator", Output = new { Vibrate = new { Value = new[] { 0, 20 } } } }
        };
        // Nonconsecutive feature indices catch hard-coded motor-index assumptions.
        if (pump) features["9"] = new { FeatureIndex = 9, FeatureDescription = "Air Pump", Output = new { Constrict = new { Value = new[] { 0, 3 } } } };
        if (pump && BatteryDevice) features["30"] = new { FeatureIndex = 30, FeatureDescription = "Battery", Input = new { Battery = new { Value = new[] { new[] { 0, 100 } }, Command = new[] { "Read" } } } };
        return features;
    }

    public async Task SendAsync(object message)
    {
        await sendGate.WaitAsync();
        try
        {
            var data = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(new[] { message }));
            if (FragmentReplies)
            {
                for (var offset = 0; offset < data.Length; offset += 17)
                {
                    var length = Math.Min(17, data.Length - offset);
                    await socket!.SendAsync(data.AsMemory(offset, length), WebSocketMessageType.Text, offset + length == data.Length, CancellationToken.None);
                }
            }
            else await socket!.SendAsync(data, WebSocketMessageType.Text, true, CancellationToken.None);
        }
        finally { sendGate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        try { socket?.Abort(); } catch (ObjectDisposedException) { }
        await app.StopAsync();
        await app.DisposeAsync();
        sendGate.Dispose();
    }
}
