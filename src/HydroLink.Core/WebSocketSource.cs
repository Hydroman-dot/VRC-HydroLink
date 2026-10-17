using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using HydroLink.Sdk;

namespace HydroLink.Core;

public sealed class WebSocketSource(string name, Uri uri, Settings settings, IDataBus bus, Action<string> log) : Worker(name, ComponentKind.Source, log)
{
    private static Task Send(ClientWebSocket ws, object data, CancellationToken token) => ws.SendAsync(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(data)), WebSocketMessageType.Text, true, token);
    protected override async Task Run(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            using var ws = new ClientWebSocket();
            ws.Options.KeepAliveInterval = Id == "VRCOSC" ? Timeout.InfiniteTimeSpan : TimeSpan.FromSeconds(20);
            using var connection = CancellationTokenSource.CreateLinkedTokenSource(token);
            Task heartbeat = Task.CompletedTask;
            try
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromSeconds(10));
                await ws.ConnectAsync(uri, timeout.Token);
                Log(Id + " verbunden");
                bus.Publish("provider/" + Id + "/status", "connected", Id);
                if (Id == "VRTI")
                {
                    await Send(ws, new { command = "EventSubscribe", payload = JsonSerializer.Serialize(new[] { "TreadmillStateUpdated", "StatisticsUpdated", "AutoWalkSpeedUpdated", "AutoWalkSettingsUpdated" }) }, token);
                    await Send(ws, new { command = "GetTreadmillState" }, token);
                    await Send(ws, new { command = "GetStatistics" }, token);
                    await Send(ws, new { command = "GetAutoWalkSettings" }, token);
                }
                if (Id == "HypeRate")
                {
                    await Send(ws, new { topic = "hr:" + settings.HypeRateId, @event = "phx_join", payload = new { }, @ref = 0 }, token);
                    heartbeat = Heartbeat(ws, connection.Token);
                }
                var buffer = new byte[8192];
                while (!token.IsCancellationRequested && ws.State == WebSocketState.Open)
                {
                    using var message = new MemoryStream();
                    WebSocketReceiveResult result;
                    do
                    {
                        result = await ws.ReceiveAsync(buffer, token);
                        if (result.MessageType == WebSocketMessageType.Close) throw new WebSocketException("Connection closed");
                        message.Write(buffer, 0, result.Count);
                        if (message.Length > 1024 * 1024) throw new FormatException("Message exceeds 1 MiB");
                    } while (!result.EndOfMessage);
                    if (result.MessageType != WebSocketMessageType.Text) continue;
                    bus.Publish("provider/" + Id + "/status", "connected", Id);
                    try { Telemetry.Parse(Id, Encoding.UTF8.GetString(message.ToArray()), bus, Id == settings.MovementSource || Id == settings.HeartSource); }
                    catch (Exception ex) when (ex is JsonException or InvalidOperationException or FormatException) { Log(Id + ": ungültige Nachricht übersprungen"); }
                }
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
            catch (Exception ex) { Log(Id + ": getrennt (" + ex.GetType().Name + ") · neuer Versuch in 3 s"); }
            finally { bus.Publish("provider/" + Id + "/status", "disconnected", Id); await connection.CancelAsync(); try { await heartbeat; } catch (OperationCanceledException) { } catch (WebSocketException) { } }
            await Task.Delay(3000, token);
        }
    }
    private static async Task Heartbeat(ClientWebSocket ws, CancellationToken token)
    {
        var reference = 1;
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(10000, token);
            await Send(ws, new { topic = "phoenix", @event = "heartbeat", payload = new { }, @ref = reference++ }, token);
        }
    }
}

