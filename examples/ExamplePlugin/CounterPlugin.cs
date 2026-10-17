using HydroLink.Sdk;

namespace ExamplePlugin;
public sealed class CounterPlugin : IHydroLinkPlugin
{
    public string Id => "example.counter";
    public string Name => "Example Counter";
    public int ApiVersion => 1;
    public void Configure(IPluginContext context) => context.Register(new Counter(context.Bus));
    private sealed class Counter(IDataBus bus) : IComponent
    {
        public string Id => "example.counter.source";
        public ComponentKind Kind => ComponentKind.Source;
        private CancellationTokenSource? stop;
        private Task? task;
        public Task StartAsync(CancellationToken cancellationToken)
        {
            stop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            task = Run(stop.Token); return Task.CompletedTask;
        }
        private async Task Run(CancellationToken token)
        {
            int count = 0;
            try { while (!token.IsCancellationRequested) { bus.Publish("example/counter", new { count = count++ }, Id); await Task.Delay(1000, token); } }
            catch (OperationCanceledException) when (token.IsCancellationRequested) { }
        }
        public async ValueTask DisposeAsync() { if (stop is null) return; await stop.CancelAsync(); if (task is not null) await task; stop.Dispose(); }
    }
}
