using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using HydroLink.Core;
using MQTTnet;
using MQTTnet.Server;
using MQTTnet.Client;
using MQTTnet.Protocol;
internal static class HomeAssistantChecks
{
    public static async Task Run(Action<bool, string> check)
    {
        var folder = Path.Combine(Path.GetTempPath(), "hydrolink-mqtt-tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        try
        {
            var path = Path.Combine(folder, "queue.json");
            var box = new HomeAssistantOutbox(path, "broker:1883");
            var now = DateTimeOffset.Now;
            box.Append("test", "start", new(0, 0, 0, 0, 0, null), now);
            box.Append("test", "update", new(10, .02, 2, 3, 15, 90), now.AddSeconds(15));
            var restart = new HomeAssistantOutbox(path, "broker:1883"); restart.Recover();
            check(restart.Count == 3, "MQTT outbox survives restart and closes interrupted session at last sample");
            check(!restart.Acknowledge("{\"status\":\"stored\",\"test\":true,\"session_id\":\"test\",\"sequence\":0}"), "MQTT test acknowledgment never removes real data");
            check(!restart.Acknowledge("{\"status\":\"stored\",\"test\":false,\"session_id\":\"other\",\"sequence\":0}"), "MQTT acknowledgment must match session identity");
            check(restart.Acknowledge("{\"status\":\"stored\",\"test\":false,\"session_id\":\"test\",\"sequence\":0}") && restart.Next()!.Sequence == 1, "MQTT exact acknowledgment advances durable queue");
            var blocked = false; try { _ = new HomeAssistantOutbox(path, "other:1883").Count; } catch (InvalidOperationException) { blocked = true; }
            check(blocked, "pending fitness data cannot silently move to another broker");
            restart.Acknowledge("{\"status\":\"stored\",\"test\":false,\"session_id\":\"test\",\"sequence\":1}");
            var end = restart.Next()!;
            check(end.EventType == "end" && end.Timestamp == now.AddSeconds(15) && end.StepsToday == 10 && end.SessionDuration == 15, "crash recovery invents neither elapsed time nor daily totals");
            var sample = JsonDocument.Parse(HomeAssistantOutbox.Serialize(end));
            check(sample.RootElement.GetProperty("schema_version").GetInt32() == 1 && sample.RootElement.GetProperty("session_id").GetString() == "test", "MQTT session wire format retains VR Fitness v1 field names");
            var probe = new TcpListener(IPAddress.Loopback, 0); probe.Start(); var port = ((IPEndPoint)probe.LocalEndpoint).Port; probe.Stop();
            var factory = new MqttFactory();
            using var broker = factory.CreateMqttServer(new MqttServerOptionsBuilder().WithDefaultEndpoint().WithDefaultEndpointPort(port).WithDefaultEndpointBoundIPAddress(IPAddress.Loopback).Build());
            await broker.StartAsync();
            var cfg = new Settings { HomeAssistant = new() { Enabled = true, Host = "127.0.0.1", Port = port }, FitnessOptions = new() { DataDirectory = Path.Combine(folder, "sessions") } };
            var bus = new DataBus();
            var received = new System.Collections.Concurrent.ConcurrentQueue<(string Topic, string Payload)>();
            using var observer = factory.CreateMqttClient();
            observer.ApplicationMessageReceivedAsync += e => { received.Enqueue((e.ApplicationMessage.Topic, e.ApplicationMessage.ConvertPayloadToString())); return Task.CompletedTask; };
            var clientOptions = new MqttClientOptionsBuilder().WithTcpServer("127.0.0.1", port).Build();
            await observer.ConnectAsync(clientOptions);
            await observer.SubscribeAsync("#", MqttQualityOfServiceLevel.AtLeastOnce);
            using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(25));
            async Task Until(Func<bool> condition) { while (!condition()) await Task.Delay(50, deadline.Token); }
            await using var service = new HomeAssistantService(cfg, bus, _ => { });
            await service.StartAsync(deadline.Token);
            bus.Publish("fitness/heart/bpm", 85, "fixture");
            await Until(() => received.Any(e => e.Topic == "homeassistant/sensor/hydrolink/heart_rate/config"));
            bus.Publish("fitness/heart/bpm", 85, "fixture");
            await Until(() => received.Any(e => e.Topic == "vr_fitness/v1/live" && e.Payload.Contains("85")));
            check(true, "real MQTT broker receives discovery and live fitness values");
            var queue = HomeAssistantOutbox.For(cfg); queue.Append("wire", "start", new(0,0,0,0,0,null), now);
            await Until(() => received.Any(e => e.Topic == "vr_fitness/v1/session"));
            check(queue.Count == 1, "MQTT broker delivery alone does not discard unconfirmed session");
            await observer.PublishStringAsync("vr_fitness/v1/ack", "{\"status\":\"stored\",\"test\":false,\"session_id\":\"wire\",\"sequence\":0}", MqttQualityOfServiceLevel.AtLeastOnce);
            await Until(() => queue.Count == 0);
            check(true, "real MQTT application acknowledgment clears session buffer");
            await broker.StopAsync();
            queue.Append("wire", "end", new(3,.01,1,0,3,80), now.AddSeconds(3));
            check(queue.Count == 1, "broker outage keeps completed session queued");
            await broker.StartAsync();
            await observer.ConnectAsync(clientOptions);
            await observer.SubscribeAsync("vr_fitness/v1/session", MqttQualityOfServiceLevel.AtLeastOnce);
            await Until(() => received.Any(e => e.Topic == "vr_fitness/v1/session" && e.Payload.Contains("\"event_type\":\"end\"")));
            check(true, "MQTT reconnect delivers buffered session without restarting HydroLink");
            await service.DisposeAsync(); await observer.DisconnectAsync(); await broker.StopAsync();
            var recordingFolder = Path.Combine(folder, "recording");
            var recordingCfg = new Settings { HomeAssistant = new() { Enabled = true, Host = "fixture" }, FitnessOptions = new() { DataDirectory = recordingFolder } };
            await using (var recording = new FitnessSession(bus, recordingFolder, recordingCfg))
            {
                recording.Start("MQTT lifecycle");
                await Task.Delay(1100);
                await recording.StopAsync();
                var lifecycleQueue = HomeAssistantOutbox.For(recordingCfg);
                check(recording.RecordingError == null && lifecycleQueue.Count == 2 && lifecycleQueue.Next()!.EventType == "start", "real fitness recording queues start and end without disturbing CSV");
            }
            var secretPath = Path.Combine(folder, "settings.json"); cfg.HomeAssistant.Password = "fixture-only"; cfg.Save(secretPath);
            check(!File.ReadAllText(secretPath).Contains("fixture-only") && Settings.Load(secretPath).HomeAssistant.Password == "fixture-only", "MQTT password uses Windows-protected storage");
        }
        finally { Directory.Delete(folder, true); }
    }
}
