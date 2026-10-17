using System.Globalization;
using System.Text.Json;
using HydroLink.Sdk;

namespace HydroLink.Core;

public static class Telemetry
{
    public static double? Heart(JsonElement value)
    {
        if (value.ValueKind is JsonValueKind.Number or JsonValueKind.String && double.TryParse(value.ToString(), CultureInfo.InvariantCulture, out var n)) return n is >= 20 and <= 300 ? n : null;
        if (value.ValueKind == JsonValueKind.Object)
        {
            foreach (var key in new[] { "bpm", "BPM", "heartRate", "heartrate", "heart_rate", "heart-rate", "hr", "HR", "pulse", "Pulse", "HeartRate", "data", "payload", "value" })
                if (value.TryGetProperty(key, out var child) && Heart(child) is { } heart) return heart;
        }
        if (value.ValueKind == JsonValueKind.Array) foreach (var item in value.EnumerateArray()) if (Heart(item) is { } heart) return heart;
        return null;
    }
    public static void Parse(string source, string raw, IDataBus bus, bool publishFitness = true)
    {
        void Emit(string topic, object value)
        {
            bus.Publish("sources/" + source + "/" + topic, value, source);
            if (publishFitness) bus.Publish("fitness/" + topic, value, source);
        }
        using var doc = JsonDocument.Parse(raw);
        var root = doc.RootElement;
        bus.Publish("provider/" + source + "/raw", root, source);
        if (source is "VRCOSC" or "Pulsoid" or "HypeRate")
        {
            if (source == "HypeRate" && (!root.TryGetProperty("event", out var e) || e.GetString() != "hr_update")) return;
            if (Heart(root) is { } bpm) Emit("heart/bpm", bpm);
            return;
        }
        if (source == "VRTI" && root.TryGetProperty("payload", out var payload))
        {
            using var nested = payload.ValueKind == JsonValueKind.String ? JsonDocument.Parse(payload.GetString()!) : null;
            var p = nested?.RootElement ?? payload;
            if (p.ValueKind != JsonValueKind.Object) return;
            foreach (var (key, topic, factor) in new[] { ("currentSpeed", "speed-kmh", 1d), ("targetSpeed", "target-speed-kmh", 1d), ("userSpeedLimit", "speed-limit-kmh", 1d), ("speed", "autowalk-speed", 1d), ("distance", "distance-km", .001), ("steps", "steps", 1d), ("time", "active-seconds", 1d) })
                if (p.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number && v.TryGetDouble(out var number) && double.IsFinite(number)) Emit(topic, number * factor);
            foreach (var key in new[] { "connected", "running", "autoWalkEnabled" })
                if (p.TryGetProperty(key, out var state)) Emit(key, state);
            if (root.TryGetProperty("event", out var evt) && evt.GetString() == "AutoWalkSettingsUpdated" && p.TryGetProperty("enabled", out var enabled)) Emit("autowalk-enabled", enabled);
        }
        if (source == "FitOSC" && root.TryGetProperty("type", out var type) && type.GetString() is "state" or "telemetry" && root.TryGetProperty("data", out var data) && data.TryGetProperty("metrics", out var metrics))
        {
            foreach (var (key, topic) in new[] { ("speed", "speed-kmh"), ("distance", "distance-km"), ("stepCount", "steps"), ("elapsedTime", "active-seconds") })
            {
                if (!metrics.TryGetProperty(key, out var m)) continue;
                var value = m.ValueKind == JsonValueKind.Object && m.TryGetProperty("value", out var v) ? v : m;
                if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var number) || !double.IsFinite(number)) continue;
                if (m.ValueKind == JsonValueKind.Object && m.TryGetProperty("unit", out var unit) && unit.GetString()?.ToUpperInvariant() is "MPH" or "MILES") number *= 1.609344;
                Emit(topic, number);
            }
            if (data.TryGetProperty("connections", out var connections)) Emit("connections", connections);
            if (data.TryGetProperty("configuration", out var configuration)) Emit("configuration", configuration);
            if (data.TryGetProperty("walking", out var walking))
            {
                if (walking.TryGetProperty("velocity", out var velocity)) Emit("autowalk-speed", velocity);
                if (walking.TryGetProperty("mode", out var mode))
                {
                    Emit("walking-mode", mode);
                    Emit("autowalk-enabled", mode.GetString()?.ToLowerInvariant() != "disabled" && (!walking.TryGetProperty("isManualOverride", out var manual) || manual.ValueKind != JsonValueKind.True));
                }
            }
            if (data.TryGetProperty("deviceName", out var device)) Emit("device-name", device);
            if (data.TryGetProperty("treadmillState", out var treadmill)) Emit("running", treadmill.GetString()?.ToLowerInvariant() == "running");
        }
    }
}
