using HydroLink.Sdk;

namespace HydroLink.Core;

public abstract class Worker(string id, ComponentKind kind, Action<string> log) : IComponent
{
    public string Id => id;
    public ComponentKind Kind => kind;
    protected Action<string> Log => log;
    private CancellationTokenSource? stop;
    private Task? task;
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
            task = Task.Run(() => RunGuarded(token), CancellationToken.None);
        }
        finally { lifecycle.Release(); }
    }
    private async Task RunGuarded(CancellationToken token)
    {
        try { await Run(token); }
        catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        catch (Exception ex) { log($"{id}: {ex.GetType().Name} – {ex.Message}"); }
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
