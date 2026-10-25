using VrcHapticsBridge.Core;
using VrcHapticsBridge.Tests;
internal static class BatteryChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        await using (var batteryServer = new FakeServer { BatteryDevice = true })
        {
            await batteryServer.StartAsync();
            var batteryBackend = new ButtplugBackend();
            using var batteryTimeout = new CancellationTokenSource(TimeSpan.FromSeconds(10));
            try
            {
                await batteryBackend.ConnectAsync(batteryServer.Url, batteryTimeout.Token);
                await batteryBackend.RefreshBatteriesAsync(batteryTimeout.Token);
                check(batteryBackend.Battery(7).Percent == 73, "battery reading reaches backend as correct percentage over WebSocket");
                check(batteryBackend.Battery(8).Percent == null && batteryBackend.Battery(8).Status.Contains("nicht gemeldet"), "unsupported battery remains unknown rather than zero");
                await batteryBackend.RefreshBatteriesAsync(batteryTimeout.Token);
                check(batteryServer.Messages.Count(m => m.TryGetProperty("InputCmd", out _)) == 1 && !batteryServer.Messages.Any(m => m.TryGetProperty("OutputCmd", out _)), "battery polling is throttled and never sends actuator output");
                await batteryBackend.DisconnectAsync();
                check(batteryBackend.Battery(7).Percent == null, "disconnection removes displayed battery reading");
            }
            finally { if (batteryBackend.Connected) await batteryBackend.DisconnectAsync(); batteryBackend.DisposeClient(); }
        }
    }
}
