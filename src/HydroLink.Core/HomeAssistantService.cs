using System.Collections.Concurrent;
using System.Text.Json;
using HydroLink.Sdk;
using MQTTnet;
using MQTTnet.Client;
using MQTTnet.Protocol;
namespace HydroLink.Core;

public sealed class HomeAssistantService : Worker
{
    private readonly Settings settings;
    private readonly IDataBus bus;
    private readonly HomeAssistantOutbox outbox;
    public HomeAssistantService(Settings settings, IDataBus bus, Action<string> log) : base("home-assistant", ComponentKind.Output, log)
    {
        settings.HomeAssistant.Validate(); this.settings = settings; this.bus = bus;
        outbox = HomeAssistantOutbox.For(settings); outbox.Recover();
    }
    private void Status(string value) => bus.Publish("home-assistant/status", value, "home-assistant");
    protected override async Task Run(CancellationToken token)
    {
        var live = new ConcurrentDictionary<string, DataEvent>();
        using var subscription = bus.Subscribe("fitness", e => live[e.Topic] = e);
        var cfg = settings.HomeAssistant;
        var options = new MqttClientOptionsBuilder().WithTcpServer(cfg.Host, cfg.Port)
            .WithClientId("hydrolink-" + Guid.NewGuid().ToString("N")).WithCredentials(cfg.Username, cfg.Password)
            .WithTimeout(TimeSpan.FromSeconds(8)).WithKeepAlivePeriod(TimeSpan.FromSeconds(30))
            .WithWillTopic("vr_fitness/v1/availability").WithWillPayload("offline").WithWillRetain()
            .WithWillQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce);
        if (cfg.Tls) options.WithTlsOptions(o => o.UseTls());
        using var client = new MqttFactory().CreateMqttClient();
        client.ApplicationMessageReceivedAsync += e =>
        {
            if (e.ApplicationMessage.Retain) return Task.CompletedTask;
            try
            {
                if (e.ApplicationMessage.Topic == "vr_fitness/v1/ack") outbox.Acknowledge(e.ApplicationMessage.ConvertPayloadToString());
                else Status("Home Assistant hat eine Session abgelehnt; der Puffer bleibt erhalten.");
            }
            catch (Exception ex) { Status("MQTT-Antwort konnte nicht verarbeitet werden: " + ex.GetType().Name); }
            return Task.CompletedTask;
        };
        async Task Publish(string topic, string payload, bool retain, CancellationToken cancellation)
        {
            var result = await client.PublishAsync(new MqttApplicationMessageBuilder().WithTopic(topic).WithPayload(payload)
                .WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce).WithRetainFlag(retain).Build(), cancellation);
            if ((int)result.ReasonCode >= 128) throw new IOException("MQTT-Veröffentlichung abgelehnt");
        }
        try
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    Status("Verbinde mit MQTT …");
                    var connected = await client.ConnectAsync(options.Build(), token);
                    if (connected.ResultCode != MqttClientConnectResultCode.Success) throw new IOException("MQTT-Anmeldung abgelehnt");
                    var subscribed = await client.SubscribeAsync(new MqttClientSubscribeOptionsBuilder()
                        .WithTopicFilter(f => f.WithTopic("vr_fitness/v1/ack").WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce))
                        .WithTopicFilter(f => f.WithTopic("vr_fitness/v1/error").WithQualityOfServiceLevel(MqttQualityOfServiceLevel.AtLeastOnce)).Build(), token);
                    if (subscribed.Items.Any(i => (int)i.ResultCode >= 128)) throw new IOException("MQTT-Abonnement abgelehnt");
                    await Publish("vr_fitness/v1/availability", "online", true, token);
                    foreach (var (key, name, unit, deviceClass) in new[] { ("heart_rate", "Puls", "bpm", (string?)null), ("speed", "Geschwindigkeit", "km/h", "speed"), ("distance_km", "Distanz gesamt", "km", "distance"), ("steps", "Schritte gesamt", "Schritte", (string?)null), ("active_seconds", "Aktive Zeit", "s", "duration") })
                        await Publish("homeassistant/sensor/hydrolink/" + key + "/config", JsonSerializer.Serialize(new
                        {
                            name, unique_id = "hydrolink_" + key, state_topic = "vr_fitness/v1/live",
                            value_template = "{{ value_json." + key + " }}", unit_of_measurement = unit,
                            device_class = deviceClass, availability_topic = "vr_fitness/v1/availability", expire_after = 15,
                            device = new { identifiers = new[] { "hydrolink_fitness" }, name = "HydroLink Fitness", manufacturer = "HydroLink" }
                        }, new JsonSerializerOptions { DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull }), true, token);
                    string? sent = null; var retryAt = DateTimeOffset.MinValue;
                    while (client.IsConnected && !token.IsCancellationRequested)
                    {
                        double? Number(string topic) => live.TryGetValue("fitness/" + topic, out var e) && DateTimeOffset.UtcNow - e.Timestamp < TimeSpan.FromSeconds(10)
                            && e.Value.ValueKind == JsonValueKind.Number && e.Value.TryGetDouble(out var n) && double.IsFinite(n) ? n : null;
                        await Publish("vr_fitness/v1/live", JsonSerializer.Serialize(new { timestamp = DateTimeOffset.UtcNow, heart_rate = Number("heart/bpm"), speed = Number("speed-kmh"), distance_km = Number("distance-km"), steps = Number("steps"), active_seconds = Number("active-seconds") }), false, token);
                        var entry = outbox.Next();
                        if (entry != null)
                        {
                            var key = entry.SessionId + ":" + entry.Sequence;
                            if (sent != key || DateTimeOffset.UtcNow >= retryAt)
                            {
                                await Publish("vr_fitness/v1/session", HomeAssistantOutbox.Serialize(entry), false, token);
                                sent = key; retryAt = DateTimeOffset.UtcNow.AddSeconds(15);
                            }
                        }
                        Status("MQTT verbunden · " + outbox.Count + " Session-Meldungen warten auf HA-Bestätigung");
                        await Task.Delay(2000, token);
                    }
                }
                catch (OperationCanceledException) when (token.IsCancellationRequested) { break; }
                catch (Exception ex)
                {
                    Status("MQTT nicht verbunden · " + ex.GetType().Name + " · erneuter Versuch in 5 s; Sessions bleiben gepuffert.");
                    if (client.IsConnected) try { await client.DisconnectAsync(new MqttClientDisconnectOptions(), CancellationToken.None); } catch { }
                    await Task.Delay(5000, token);
                }
            }
        }
        finally
        {
            if (client.IsConnected)
            {
                using var deadline = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await Publish("vr_fitness/v1/availability", "offline", true, deadline.Token); await client.DisconnectAsync(new MqttClientDisconnectOptions(), deadline.Token); } catch { }
            }
            Status("Aus · gespeicherte Sessions bleiben im Puffer");
        }
    }
}
