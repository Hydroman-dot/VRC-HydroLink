using System.Reflection;
using System.Runtime.Loader;
using HydroLink.Sdk;

namespace HydroLink.Core;

public sealed record PluginInfo(string Id, string Name, string Path, bool Enabled);
public sealed class PluginLoader(IDataBus bus, Action<string> log) : IPluginContext
{
    public IDataBus Bus => bus;
    private readonly List<PluginLoadContext> contexts = [];
    public void Unload() { Components.Clear(); FailedComponents.Clear(); Pages.Clear(); foreach(var context in contexts) context.Unload(); contexts.Clear(); LoadedIds.Clear(); }
    public HashSet<string> LoadedIds { get; } = [];
    public List<IComponent> FailedComponents { get; } = [];
    public List<IComponent> Components { get; } = [];
    public List<PluginPage> Pages { get; } = [];
    public List<PluginInfo> Plugins { get; } = [];
    public void Register(IComponent component)
    {
        if (Components.Any(x => x.Id == component.Id)) throw new InvalidOperationException("Duplicate component: " + component.Id);
        Components.Add(component);
    }
    public void RegisterPage(PluginPage page) => Pages.Add(page);
    public void Log(string message) => log(message);
    public void Load(string directory, IReadOnlyCollection<string> enabled, bool recursive = true)
    {
        directory = Path.GetFullPath(directory);
        if (!Directory.Exists(directory)) return;
        foreach (var manifest in Directory.EnumerateFiles(directory, "plugin.json", recursive ? SearchOption.AllDirectories : SearchOption.TopDirectoryOnly))
        {
            PluginLoader? staging = null;
            try
            {
                var folder = Path.GetDirectoryName(manifest)!;
                var metadata = PluginManifest.Read(folder);
                var id = metadata.Id;
                var name = metadata.Name;
                var path = Path.GetFullPath(Path.Combine(folder,metadata.Assembly));
                if (!path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Plugin assembly outside its folder");
                Plugins.Add(new(id, name, path, enabled.Contains(id)));
                if (!enabled.Contains(id)) continue;
                var context = new PluginLoadContext(path); contexts.Add(context);
                var assembly = context.LoadFromAssemblyPath(path);
                var entryType = metadata.EntryType;
                var pluginType = assembly.GetTypes().Single(t => typeof(IHydroLinkPlugin).IsAssignableFrom(t) && !t.IsAbstract && (entryType is null || t.FullName == entryType));
                var plugin = (IHydroLinkPlugin)Activator.CreateInstance(pluginType)!;
                if (plugin.ApiVersion != 1 || plugin.Id != id) throw new InvalidDataException("Incompatible plugin identity/API");
                staging = new PluginLoader(bus, log);
                plugin.Configure(staging);
                if (staging.Components.Any(c => Components.Any(existing => existing.Id == c.Id))) throw new InvalidDataException("Duplicate plugin component ID");
                Components.AddRange(staging.Components); Pages.AddRange(staging.Pages);
                LoadedIds.Add(id);
                Log("Plugin geladen: " + name);
            }
            catch (Exception ex) { if(staging is not null) FailedComponents.AddRange(staging.Components); Log("Plugin nicht geladen: " + Path.GetFileName(Path.GetDirectoryName(manifest)) + " · " + ex.Message); }
        }
    }
    private sealed class PluginLoadContext(string path) : AssemblyLoadContext(isCollectible: true)
    {
        private readonly AssemblyDependencyResolver resolver = new(path);
        protected override Assembly? Load(AssemblyName name)
        {
            if (name.Name == typeof(IHydroLinkPlugin).Assembly.GetName().Name) return typeof(IHydroLinkPlugin).Assembly;
            var dependency = resolver.ResolveAssemblyToPath(name);
            return dependency is null ? null : LoadFromAssemblyPath(dependency);
        }
        protected override nint LoadUnmanagedDll(string name)
        {
            var path = resolver.ResolveUnmanagedDllToPath(name);
            return path is null ? 0 : LoadUnmanagedDllFromPath(path);
        }
    }
}
