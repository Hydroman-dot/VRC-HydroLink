using System.Collections.Concurrent;
using System.Text.Json;
using HydroLink.Sdk;

namespace HydroLink.Core;

public sealed class DataBus : IDataBus
{
    private readonly ConcurrentDictionary<long, (string Prefix, Action<DataEvent> Handler)> handlers = new();
    private long next;
    public event Action<Exception>? SubscriberError;
    public void Publish(string topic, object value, string source)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(topic);
        var item = new DataEvent(topic, JsonSerializer.SerializeToElement(value), source, DateTimeOffset.UtcNow);
        foreach (var (prefix, handler) in handlers.Values)
            if (topic == prefix || prefix == "" || topic.StartsWith(prefix + "/", StringComparison.Ordinal))
                try { handler(item); } catch (Exception ex) { SubscriberError?.Invoke(ex); }
    }
    public IDisposable Subscribe(string topicPrefix, Action<DataEvent> handler)
    {
        var id = Interlocked.Increment(ref next);
        handlers[id] = (topicPrefix.TrimEnd('/'), handler);
        return new Subscription(() => handlers.TryRemove(id, out _));
    }
    private sealed class Subscription(Action dispose) : IDisposable
    {
        private Action? action = dispose;
        public void Dispose() => Interlocked.Exchange(ref action, null)?.Invoke();
    }
}
