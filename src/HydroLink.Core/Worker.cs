using HydroLink.Sdk;

namespace HydroLink.Core;

public sealed record WorkerHealth(string State, string Detail);

public abstract class Worker(string id, ComponentKind kind, Action<string> log) : IComponent
{
    public string Id => id;
    public ComponentKind Kind => kind;
    protected Action<string> Log => log;
    private CancellationTokenSource? stop;
    private Task? task;
    private WorkerHealth health = new("Aus", "Nicht gestartet");
    public WorkerHealth Health => Volatile.Read(ref health);
    private readonly SemaphoreSlim lifecycle = new(1, 1);
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await lifecycle.WaitAsync(cancellationToken);
        try
        {
            if (task is { IsCompleted: false }) return;
            stop?.Dispose();
            stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            var token = stop.Token;
            Volatile.Write(ref health, new("Startet", "Dienst wird gestartet"));
            task = Task.Run(() => RunGuarded(token), CancellationToken.None);
        }
        finally { lifecycle.Release(); }
    }
    private async Task RunGuarded(CancellationToken token)
    {
        try
        {
            Volatile.Write(ref health, new("Läuft", "Dienst läuft; Verbindungsstatus separat prüfen"));
            await Run(token);
            Volatile.Write(ref health, token.IsCancellationRequested ? new("Aus", "Beendet") : new("Fehler", "Dienst wurde unerwartet beendet. Modul neu starten."));
        }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { Volatile.Write(ref health, new("Aus", "Beendet")); }
        catch (Exception ex)
        {
            Volatile.Write(ref health, new("Fehler", ex.GetType().Name + ": " + ex.Message));
            log($"{id}: {ex.GetType().Name} – {ex.Message}");
        }
    }
    protected abstract Task Run(CancellationToken token);
    public virtual async ValueTask DisposeAsync()
    {
        await lifecycle.WaitAsync();
        try
        {
            if (stop is null) return;
            await stop.CancelAsync();
            if (task is not null) await task;
            stop.Dispose(); stop = null; task = null;
        }
        finally { lifecycle.Release(); }
    }
}
