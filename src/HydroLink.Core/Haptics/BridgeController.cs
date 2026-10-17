namespace VrcHapticsBridge.Core;

public enum OutputMode { Off, Osc, Manual }
public sealed record BridgeSnapshot(OutputMode Mode, double? VibrateRaw, double? PumpRaw,
    double VibrateSent, double PumpSent, DateTimeOffset? LastOsc);

/// <summary>Latest-value routing; one serialized output worker, no queued stream of stale samples.</summary>
public sealed class BridgeController(IHapticsBackend backend, TimeProvider? clock = null) : IAsyncDisposable
{
    private readonly TimeProvider time = clock ?? TimeProvider.System;
    private readonly object sync = new();
    private readonly SemaphoreSlim sendGate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private BridgeSettings settings = new();
    private OutputMode mode;
    private readonly double?[] raw = new double?[2];
    private readonly double[] target = new double[2];
    private readonly double[] sent = new double[2];
    private readonly DateTimeOffset[] receivedAt = new DateTimeOffset[2];
    private DateTimeOffset? lastOsc;
    private DateTimeOffset manualEnd;
    private DateTimeOffset nextSend;
    private int nextChannel;
    private Task? worker;
    private bool stopRequested;
    private long generation;
    public event Action<string>? Log;

    public BridgeSnapshot Snapshot
    {
        get { lock (sync) return new(mode, raw[0], raw[1], sent[0], sent[1], lastOsc); }
    }

    public void Start() => worker ??= RunAsync();

    public void Configure(BridgeSettings value)
    {
        value.Validate();
        lock (sync)
        {
            if (mode != OutputMode.Off) throw new InvalidOperationException("Vor Konfigurationsänderungen stoppen.");
            settings = value;
            Array.Clear(raw);
            lastOsc = null;
        }
    }

    public void Receive(IReadOnlyList<OscValue> values)
    {
        lock (sync)
        {
            // Avatar changes take priority over all other messages in this datagram.
            if (values.Any(v => v.Address == "/avatar/change"))
            {
                DisarmLocked();
                stopRequested = true;
                Array.Clear(raw);
                return;
            }
            foreach (var item in values)
            {
                var index = item.Address == settings.VibrateAddress ? 0 : item.Address == settings.PumpAddress ? 1 : -1;
                if (index < 0 || item.Value is not { } value || !double.IsFinite(value)) continue;
                raw[index] = value;
                lastOsc = time.GetUtcNow();
                if (mode != OutputMode.Osc) continue;
                receivedAt[index] = lastOsc.Value;
                target[index] = Math.Clamp(value, 0, 1);
            }
        }
    }

    public async Task ArmOscAsync()
    {
        var ticket = await StopCoreAsync().ConfigureAwait(false);
        lock (sync)
        {
            if (ticket != generation) return;
            EnsureDevice();
            mode = OutputMode.Osc;
        }
        Log?.Invoke("OSC-Ausgabe aktiv. Warte auf neue Werte je Kanal.");
    }

    public async Task TestAsync(Channel channel, double value)
    {
        if (!double.IsFinite(value) || value is < 0 or > 1) throw new ArgumentOutOfRangeException(nameof(value));
        var ticket = await StopCoreAsync().ConfigureAwait(false);
        lock (sync)
        {
            if (ticket != generation) return;
            EnsureDevice();
            var d = backend.Selected!;
            if (channel == Channel.Vibrate ? !d.Vibrate : !d.Pump) throw new NotSupportedException("Dieser Kanal wird vom Gerät nicht angeboten.");
            mode = OutputMode.Manual;
            target[(int)channel] = value;
            manualEnd = time.GetUtcNow().AddSeconds(3);
        }
        Log?.Invoke($"Manueller {channel}-Test: {value:P0}, maximal 3 Sekunden. OSC-Ausgabe aus.");
    }

    private void EnsureDevice()
    {
        if (!backend.Connected || backend.Selected is not { } d || (!d.Vibrate && !d.Pump))
            throw new InvalidOperationException("Zuerst ein Gerät mit Vibrate oder Constrict auswählen.");
    }

    public void Invalidate()
    {
        lock (sync) { DisarmLocked(); stopRequested = true; }
    }

    private void DisarmLocked()
    {
        generation++;
        mode = OutputMode.Off;
        Array.Clear(target);
        Array.Clear(receivedAt);
    }

    public async Task StopAsync() => await StopCoreAsync().ConfigureAwait(false);

    private async Task<long> StopCoreAsync()
    {
        long ticket;
        lock (sync) { DisarmLocked(); ticket = generation; }
        await sendGate.WaitAsync().ConfigureAwait(false);
        try { await SendStopAsync().ConfigureAwait(false); }
        finally { sendGate.Release(); }
        return ticket;
    }

    private async Task SendStopAsync()
    {
        if (backend.Connected)
        {
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            await backend.StopAsync(timeout.Token).ConfigureAwait(false);
        }
        lock (sync)
        {
            Array.Clear(sent);
            stopRequested = false;
            nextSend = time.GetUtcNow();
        }
    }

    private async Task RunAsync()
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(50), time);
        try
        {
            while (await timer.WaitForNextTickAsync(lifetime.Token).ConfigureAwait(false))
                await TickAsync().ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (lifetime.IsCancellationRequested) { }
    }

    // Public for deterministic tests and alternate application hosts.
    public async Task TickAsync()
    {
        await sendGate.WaitAsync().ConfigureAwait(false);
        try
        {
            bool stop;
            lock (sync)
            {
                if (mode == OutputMode.Manual && time.GetUtcNow() >= manualEnd)
                { DisarmLocked(); stopRequested = true; }
                if (mode == OutputMode.Osc && settings.IdleTimeoutSeconds > 0)
                    for (var i = 0; i < 2; i++)
                        if (time.GetUtcNow() - receivedAt[i] >= TimeSpan.FromSeconds(settings.IdleTimeoutSeconds)) target[i] = 0;
                stop = stopRequested;
            }
            if (stop) { await SendStopAsync().ConfigureAwait(false); return; }
            int first;
            lock (sync) first = nextChannel;
            for (var offset = 0; offset < 2; offset++)
            {
                var i = (first + offset) % 2;
                double value;
                lock (sync)
                {
                    if (mode == OutputMode.Off || !backend.Connected || backend.Selected is not { } device) return;
                    if (time.GetUtcNow() < nextSend) return;
                    if (i == 0 ? !device.Vibrate : !device.Pump) continue;
                    value = target[i];
                    if (value == sent[i]) continue;
                }
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                await backend.SetAsync((Channel)i, value, timeout.Token).ConfigureAwait(false);
                lock (sync)
                {
                    sent[i] = value;
                    nextChannel = (i + 1) % 2;
                    nextSend = time.GetUtcNow().AddMilliseconds(Math.Max(0, backend.Selected?.TimingGap ?? 0));
                }
            }
        }
        catch (Exception ex)
        {
            lock (sync) DisarmLocked();
            Log?.Invoke($"Ausgabefehler – deaktiviert: {ex.Message}");
            try { await SendStopAsync().ConfigureAwait(false); }
            catch (Exception stopError) { Log?.Invoke($"Stopp konnte nicht bestätigt werden: {stopError.Message}"); }
        }
        finally { sendGate.Release(); }
    }

    public async ValueTask DisposeAsync()
    {
        lifetime.Cancel();
        if (worker != null) await worker.ConfigureAwait(false);
        try { await StopAsync().ConfigureAwait(false); }
        finally { lifetime.Dispose(); sendGate.Dispose(); }
    }
}
