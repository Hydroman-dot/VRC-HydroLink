using HydroLink.Sdk;

namespace HydroLink.Core;

public abstract class Worker(string id, ComponentKind kind, Action<string> log) : IComponent
{
    public string Id => id;
    public ComponentKind Kind => kind;
    protected Action<string> Log => log;
    private CancellationTokenSource? stop;
    private Task? task;
    public Task StartAsync(CancellationToken cancellationToken)
    {
        stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        task = Task.Run(() => RunGuarded(stop.Token), CancellationToken.None);
        return Task.CompletedTask;
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
        if (stop is null) return;
        await stop.CancelAsync();
        if (task is not null) await task;
        stop.Dispose(); stop = null;
    }
}
