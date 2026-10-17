using System.Reflection;
using System.Runtime.Loader;
using HydroLink.Sdk;

namespace HydroLink.Core;

public sealed record PluginInfo(string Id, string Name, string Path, bool Enabled);
public sealed class PluginLoader(IDataBus bus, Action<string> log) : IPluginContext
{
    public IDataBus Bus => bus;
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
    public void Load(string directory, IReadOnlyCollection<string> enabled)
    {
        directory = Path.GetFullPath(directory);
        if (!Directory.Exists(directory)) return;
        foreach (var manifest in Directory.EnumerateFiles(directory, "plugin.json", SearchOption.AllDirectories))
        {
            try
            {
                using var json = System.Text.Json.JsonDocument.Parse(File.ReadAllText(manifest));
                var root = json.RootElement;
                var id = root.GetProperty("id").GetString()!;
                var name = root.GetProperty("name").GetString()!;
                var folder = Path.GetDirectoryName(manifest)!;
                var path = Path.GetFullPath(Path.Combine(folder, root.GetProperty("assembly").GetString()!));
                if (!path.StartsWith(folder + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Plugin assembly outside its folder");
                Plugins.Add(new(id, name, path, enabled.Contains(id)));
                if (!enabled.Contains(id)) continue;
                var assembly = new PluginLoadContext(path).LoadFromAssemblyPath(path);
                var pluginType = assembly.GetTypes().Single(t => typeof(IHydroLinkPlugin).IsAssignableFrom(t) && !t.IsAbstract);
                var plugin = (IHydroLinkPlugin)Activator.CreateInstance(pluginType)!;
                if (plugin.ApiVersion != 1 || plugin.Id != id) throw new InvalidDataException("Incompatible plugin identity/API");
                var staging = new PluginLoader(bus, log);
                plugin.Configure(staging);
                if (staging.Components.Any(c => Components.Any(existing => existing.Id == c.Id))) throw new InvalidDataException("Duplicate plugin component ID");
                Components.AddRange(staging.Components); Pages.AddRange(staging.Pages);
                Log("Plugin geladen: " + name);
            }
            catch (Exception ex) { Log("Plugin nicht geladen: " + Path.GetFileName(Path.GetDirectoryName(manifest)) + " · " + ex.Message); }
        }
    }
    private sealed class PluginLoadContext(string path) : AssemblyLoadContext
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
