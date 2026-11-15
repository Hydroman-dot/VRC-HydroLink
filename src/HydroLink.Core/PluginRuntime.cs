using HydroLink.Sdk;
namespace HydroLink.Core;

public sealed class PluginRuntime(IDataBus bus, Action<string> log) : IAsyncDisposable
{
    private sealed record Active(PluginLoader Loader, ScopedBus Bus);
    private readonly Dictionary<string,Active> active = [];
    public IReadOnlyCollection<string> Running => active.Keys.ToArray();
    public IReadOnlyList<PluginPage> Pages => active.Values.SelectMany(a=>a.Loader.Pages).ToArray();
    public IReadOnlyList<IComponent> Components => active.Values.SelectMany(a=>a.Loader.Components).ToArray();
    public IReadOnlyList<PluginPage> PagesFor(string id) => active.TryGetValue(id,out var plugin) ? plugin.Loader.Pages.ToArray() : [];
    public (long Count,string Topic) OscReception(string id) => active.TryGetValue(id,out var plugin) ? (Interlocked.Read(ref plugin.Bus.OscCount),Volatile.Read(ref plugin.Bus.LastOsc)) : (0,"");
    public bool RequiresOsc { get; private set; }
    private readonly HashSet<string> oscUsers = [];
    public async Task StartAsync(PluginEntry entry,CancellationToken token)
    {
        var id=entry.Manifest.Id;
        if(active.ContainsKey(id)) return;
        var scoped=new ScopedBus(bus);
        var loader=new PluginLoader(scoped,log);
        try {
            loader.Load(entry.Folder,[id],recursive:false);
            if(loader.LoadedIds.Count!=1 || !loader.LoadedIds.Contains(id)) throw new InvalidOperationException("Plugin konnte nicht geladen werden.");
            if(loader.Components.Any(c=>!(c.Id == id || c.Id.StartsWith(id+".",StringComparison.Ordinal)))) throw new InvalidOperationException("Komponenten-IDs müssen mit der Plugin-ID beginnen.");
            if(loader.Components.Any(c=>Components.Any(p=>p.Id==c.Id))) throw new InvalidOperationException("Plugin-Komponenten-ID bereits belegt.");
            foreach(var component in loader.Components) await component.StartAsync(token);
            active.Add(id,new(loader,scoped));
            if(entry.Manifest.RequiresOsc) oscUsers.Add(id);
            RequiresOsc=oscUsers.Count>0;
        } catch {
            scoped.DetachSubscriptions();
            foreach(var component in loader.Components.Concat(loader.FailedComponents).AsEnumerable().Reverse()) try { await component.DisposeAsync(); } catch(Exception ex) { log("Plugin-Aufräumen: "+ex.Message); }
            scoped.Dispose();
            loader.Unload();
            throw;
        }
    }
    public async Task StopAsync(string id)
    {
        if(!active.Remove(id,out var plugin)) return;
        plugin.Bus.DetachSubscriptions();
        foreach(var component in plugin.Loader.Components.AsEnumerable().Reverse()) try { await component.DisposeAsync(); } catch(Exception ex) { log("Plugin-Stopp: "+ex.Message); }
        plugin.Bus.Dispose();
        plugin.Loader.Unload();
        oscUsers.Remove(id); RequiresOsc=oscUsers.Count>0;
    }
    public async ValueTask DisposeAsync() { foreach(var id in active.Keys.ToArray()) await StopAsync(id); }
    // Release subscriptions and suppress late output even when plugin teardown fails.
    private sealed class ScopedBus(IDataBus inner) : IDataBus, IDisposable
    {
        public long OscCount; public string LastOsc="";
        readonly object gate=new(); readonly List<IDisposable> subscriptions=[]; int stopped; int listening=1;
        public void Publish(string topic,object value,string source) { if(Volatile.Read(ref stopped)==0) inner.Publish(topic,value,source); }
        public IDisposable Subscribe(string prefix,Action<DataEvent> handler) { lock(gate) { if(listening==0) throw new ObjectDisposedException(nameof(ScopedBus)); var subscription=inner.Subscribe(prefix,e=>{ if(Volatile.Read(ref listening)==1) { if(e.Topic.StartsWith("osc/value/",StringComparison.Ordinal)) { Interlocked.Increment(ref OscCount); Volatile.Write(ref LastOsc,e.Topic); } handler(e); } }); subscriptions.Add(subscription); return subscription; } }
        public void DetachSubscriptions() { lock(gate) { Interlocked.Exchange(ref listening,0); foreach(var s in subscriptions) s.Dispose(); subscriptions.Clear(); } }
        public void Dispose() { Interlocked.Exchange(ref stopped,1); DetachSubscriptions(); }
    }
}
