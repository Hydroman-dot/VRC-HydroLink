using System.Text.Json;

namespace HydroLink.Sdk;

public sealed record DataEvent(string Topic, JsonElement Value, string Source, DateTimeOffset Timestamp);
public interface IDataBus
{
    void Publish(string topic, object value, string source);
    IDisposable Subscribe(string topicPrefix, Action<DataEvent> handler);
}
public enum ComponentKind { Source, Processor, Output, Service }
public interface IComponent : IAsyncDisposable
{
    string Id { get; }
    ComponentKind Kind { get; }
    Task StartAsync(CancellationToken cancellationToken);
}
public sealed record PluginPage(string Title, Func<object> CreateView);
public interface IPluginContext
{
    IDataBus Bus { get; }
    void Register(IComponent component);
    void RegisterPage(PluginPage page);
    void Log(string message);
}
public interface IHydroLinkPlugin
{
    string Id { get; }
    string Name { get; }
    int ApiVersion { get; }
    void Configure(IPluginContext context);
}
