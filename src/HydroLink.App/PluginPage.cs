using System.IO;
using System.Windows;
using System.Windows.Controls;
using HydroLink.Core;
namespace HydroLink.App;
public partial class MainWindow
{
    private readonly Dictionary<string,PluginUpdate> pluginUpdates = [];
    private bool pluginRestartRequired;
    private readonly Dictionary<string,(TabItem Tab,IReadOnlyList<HydroLink.Sdk.PluginPage> Pages)> pluginViewCache=[];
    private readonly Dictionary<string,TextBlock> pluginReceptionLabels=[];
    private PluginCatalog Catalog {
        get {
            var args=Environment.GetCommandLineArgs(); var index=Array.IndexOf(args,"--preview-plugin-root");
            return new(args.Contains("--render-preview") && index>=0 && index+1<args.Length ? args[index+1] : PluginCatalog.DefaultRoot,Log);
        }
    }
    private void InitializePlugins()
    {
        if(!Environment.GetCommandLineArgs().Contains("--render-preview"))
            try { PluginCarryover.Recover(AppContext.BaseDirectory,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"VRC HydroLink Updates"),Log); }
            catch(Exception ex) { Log("Plugin-Übernahme nicht möglich; bisherige Dateien bleiben erhalten: "+ex.Message); }
        PluginUpdateCheck.IsChecked=settings.CheckPluginUpdatesOnStartup;
        PluginUpdateCheck.Click+=(_,_)=>{ settings.CheckPluginUpdatesOnStartup=PluginUpdateCheck.IsChecked==true; settings.Save(); };
        RefreshPluginRows();
    }
    private async Task PluginStartup()
    {
        if(closing || closed) return;
        if(settings.AutoStartPlugins.Intersect(settings.EnabledPlugins).Any())
            try { await EnsureHost(); } catch(Exception ex) { Log("Plugin-Autostart: "+ex.Message); }
    }
    private void RefreshPlugins_Click(object sender,RoutedEventArgs e)=>RefreshPluginRows();
    private void RefreshPluginRows()
    {
        PluginRows.Children.Clear();
        var selectedPlugin=(PluginTabs.SelectedItem as TabItem)?.Tag as string;
        pluginReceptionLabels.Clear();
        IReadOnlyList<PluginEntry> entries;
        try { entries=Catalog.Discover(Path.Combine(AppContext.BaseDirectory,"plugins")); }
        catch(Exception ex) { PluginStatus.Text="Plugin-Verzeichnis nicht lesbar: "+ex.Message; return; }
        if(entries.Count==0) PluginRows.Children.Add(new TextBlock { Text="Noch keine Plugins installiert. Ein ZIP-Paket kann über den Button hinzugefügt werden.", TextWrapping=TextWrapping.Wrap, Margin=new Thickness(0,16,0,16) });
        foreach(var entry in entries)
        {
            var id=entry.Manifest.Id; var active=host?.Plugins?.Running.Contains(id)==true;
            var panel=new StackPanel { Margin=new Thickness(12) };
            panel.Children.Add(new TextBlock { Text=entry.Manifest.Name+" · "+entry.Manifest.Version+" · "+(active?"Gestartet":"Gestoppt"),FontSize=18 });
            panel.Children.Add(new TextBlock { Text=id+" · "+(entry.Manifest.GitHubRepository ?? "Keine GitHub-Updatequelle")+(entry.Manifest.RequiresOsc?" · nutzt OSC Hub":""),TextWrapping=TextWrapping.Wrap });
            var reception=new TextBlock { TextWrapping=TextWrapping.Wrap,Margin=new Thickness(0,6,0,6) }; panel.Children.Add(reception); pluginReceptionLabels[id]=reception;
            var enabled=new CheckBox { Content="Plugin freigeben", IsChecked=settings.EnabledPlugins.Contains(id),IsEnabled=!active && !pluginRestartRequired };
            enabled.Click+=(_,_)=>{ settings.EnabledPlugins.RemoveAll(x=>x==id); if(enabled.IsChecked==true) settings.EnabledPlugins.Add(id); PluginIds.Text=string.Join(", ",settings.EnabledPlugins); settings.Save(); RefreshPluginRows(); };
            panel.Children.Add(enabled);
            var auto=new CheckBox { Content="Mit HydroLink starten", IsChecked=settings.AutoStartPlugins.Contains(id), IsEnabled=enabled.IsChecked==true };
            auto.Click+=(_,_)=>{ settings.AutoStartPlugins.RemoveAll(x=>x==id); if(auto.IsChecked==true) settings.AutoStartPlugins.Add(id); settings.Save(); };
            panel.Children.Add(auto);
            var autoUpdate=new CheckBox { Content="Updates automatisch herunterladen (wirksam nach App-Neustart)", IsChecked=settings.AutoUpdatePlugins.Contains(id), IsEnabled=entry.Manifest.GitHubRepository is not null };
            autoUpdate.Click+=(_,_)=>{ settings.AutoUpdatePlugins.RemoveAll(x=>x==id); if(autoUpdate.IsChecked==true) { settings.AutoUpdatePlugins.Add(id); settings.CheckPluginUpdatesOnStartup=true; PluginUpdateCheck.IsChecked=true; } settings.Save(); };
            panel.Children.Add(autoUpdate);
            var row=new StackPanel { Orientation=Orientation.Horizontal };
            var toggle=new Button { Content=active?"Stoppen":"Starten", IsEnabled=enabled.IsChecked==true && !pluginRestartRequired };
            toggle.Click+=async(_,_)=>await PluginAction(async()=>{ await EnsureHost(); await host!.SetPluginAsync(entry,!active); }); row.Children.Add(toggle);
            var restart=new Button { Content="Neu starten",IsEnabled=active && !pluginRestartRequired };
            restart.Click+=async(_,_)=>await PluginAction(async()=>{ await host!.SetPluginAsync(entry,false); await host.SetPluginAsync(entry,true); }); row.Children.Add(restart);
            if(pluginUpdates.TryGetValue(id,out var update)) {
                var install=new Button { Content="Update "+update.Version };
                install.Click+=async(_,_)=>await InstallPluginUpdate(update); row.Children.Add(install);
            }
            if(entry.Managed) {
                var rollback=new Button { Content="Vorherige Version" };
                rollback.Click+=async(_,_)=>await PluginAction(()=>{ Catalog.Rollback(id); RequirePluginRestart(); return Task.CompletedTask; }); row.Children.Add(rollback);
            }
            panel.Children.Add(row); PluginRows.Children.Add(new Border { Child=panel,Background=Color("#203248"),CornerRadius=new CornerRadius(8),Margin=new Thickness(0,8,0,8) });
        }
        foreach(var entry in entries) {
            var id=entry.Manifest.Id;
            var pages=host?.Plugins?.PagesFor(id) ?? [];
            if(!pluginViewCache.TryGetValue(id,out var cached) || !cached.Pages.SequenceEqual(pages)) {
                if(cached.Tab is not null) PluginTabs.Items.Remove(cached.Tab);
                var content=new StackPanel { Margin=new Thickness(16) };
                if(pages.Count==0) content.Children.Add(new TextBlock { Text="Plugin in Verwaltung starten. Seine Bedienoberfläche erscheint anschließend hier.",TextWrapping=TextWrapping.Wrap });
                foreach(var page in pages)
                    try { if(page.CreateView() is FrameworkElement view) { content.Children.Add(new TextBlock { Text=page.Title,FontSize=18 }); content.Children.Add(view); } } catch(Exception ex) { content.Children.Add(new TextBlock { Text="Plugin-Seite konnte nicht geöffnet werden: "+ex.Message,TextWrapping=TextWrapping.Wrap }); }
                cached=(new TabItem { Header=entry.Manifest.Name,Tag=id,Content=new ScrollViewer { Content=content,VerticalScrollBarVisibility=ScrollBarVisibility.Auto } },pages);
                pluginViewCache[id]=cached;
            }
            if(!PluginTabs.Items.Contains(cached.Tab)) PluginTabs.Items.Add(cached.Tab);
            if(id==selectedPlugin) PluginTabs.SelectedItem=cached.Tab;
        }
        RefreshPluginReception();
        foreach(var id in pluginViewCache.Keys.Except(entries.Select(e=>e.Manifest.Id)).ToArray()) { PluginTabs.Items.Remove(pluginViewCache[id].Tab); pluginViewCache.Remove(id); }
    }
    private void RefreshPluginReception()
    {
        foreach(var pair in pluginReceptionLabels) {
            var rx=host?.Plugins?.OscReception(pair.Key) ?? (0L,"");
            pair.Value.Text=rx.Item1==0 ? "OSC im Plugin: noch keine Live-Parameter empfangen" : $"OSC im Plugin: {rx.Item1:N0} Parameter empfangen · zuletzt {rx.Item2.Replace("osc/value","")}";
        }
    }
    private async Task PluginAction(Func<Task> action)
    {
        PluginPanel.IsEnabled=false;
        try { await action(); } catch(Exception ex) { PluginStatus.Text=ex.Message; Log("Plugins: "+ex.Message); }
        finally { PluginPanel.IsEnabled=true; RefreshPluginRows(); }
    }
    private void RequirePluginRestart() { pluginRestartRequired=true; PluginStatus.Text="Installation vorbereitet. HydroLink neu starten, um die ausgewählte Version zu verwenden. Laufende Plugins bleiben bis dahin unverändert."; }
    private async void ImportPlugin_Click(object sender,RoutedEventArgs e)
    {
        var dialog=new Microsoft.Win32.OpenFileDialog { Filter="HydroLink Plugin (*.zip)|*.zip" };
        if(dialog.ShowDialog(this)!=true) return;
        if(MessageBox.Show(this,"Dieses Paket installieren? Plugins führen Code mit deinen Benutzerrechten aus. Aktiviere es anschließend nur, wenn du dem Anbieter vertraust.","Plugin hinzufügen",MessageBoxButton.YesNo)!=MessageBoxResult.Yes) return;
        await PluginAction(()=>{ Catalog.Install(dialog.FileName); RequirePluginRestart(); return Task.CompletedTask; });
    }
    private async void CheckPlugins_Click(object sender,RoutedEventArgs e)=>await CheckPlugins();
    private async Task CheckPlugins()
    {
        PluginPanel.IsEnabled=false; PluginStatus.Text="GitHub-Updates werden geprüft …"; pluginUpdates.Clear();
        var failures=new List<string>(); var installed=0;
        try {
            using var http=new System.Net.Http.HttpClient { Timeout=TimeSpan.FromSeconds(15) }; var updater=new PluginUpdates(http);
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(45));
            foreach(var entry in Catalog.Discover(Path.Combine(AppContext.BaseDirectory,"plugins")))
                try { var update=await updater.CheckAsync(entry,deadline.Token); if(update is not null) {
                    if(settings.AutoUpdatePlugins.Contains(entry.Manifest.Id)) { await StagePlugin(update); installed++; }
                    else pluginUpdates[entry.Manifest.Id]=update;
                } }
                catch(Exception ex) { failures.Add(entry.Manifest.Name+": "+ex.Message); }
            PluginStatus.Text=(pluginRestartRequired?"Neue Versionsauswahl wird nach Neustart wirksam.\n":"")+$"{installed} Update(s) vorbereitet · {pluginUpdates.Count} weitere verfügbar."+(failures.Count>0?"\nNicht geprüft: "+string.Join("; ",failures):"");
        } catch(Exception ex) { PluginStatus.Text="Update-Prüfung fehlgeschlagen: "+ex.Message; }
        finally { PluginPanel.IsEnabled=true; RefreshPluginRows(); }
    }
    private async Task InstallPluginUpdate(PluginUpdate update)
    {
        if(MessageBox.Show(this,$"{update.Installed.Manifest.Name} auf {update.Version} aktualisieren?\nQuelle: github.com/{update.Installed.Manifest.GitHubRepository}\nWirksam nach HydroLink-Neustart. Die vorherige Version bleibt erhalten.","Plugin-Update",MessageBoxButton.YesNo)!=MessageBoxResult.Yes) return;
        await PluginAction(()=>StagePlugin(update));
    }
    private async Task StagePlugin(PluginUpdate update)
    {
        var temporary=Path.GetTempFileName();
        try {
            using var http=new System.Net.Http.HttpClient { Timeout=TimeSpan.FromMinutes(2) };
            using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(2));
            await new PluginUpdates(http).DownloadAsync(update,temporary,deadline.Token);
            Catalog.Install(temporary,update.Installed.Manifest.Id,update.Version,update.Installed.Manifest.GitHubRepository);
            pluginUpdates.Remove(update.Installed.Manifest.Id); RequirePluginRestart();
        } finally { File.Delete(temporary); }
    }
    private async void InstallRepository_Click(object sender,RoutedEventArgs e)
    {
        var input=PluginRepositoryLink.Text;
        await PluginAction(async()=>{
            using var http=new System.Net.Http.HttpClient { Timeout=TimeSpan.FromSeconds(30) };
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(30));
            PluginStatus.Text="Plugin-Release wird gesucht …";
            var update=await new PluginUpdates(http).FindRepositoryAsync(input,deadline.Token);
            var existing=Catalog.Discover(Path.Combine(AppContext.BaseDirectory,"plugins")).FirstOrDefault(p=>p.Manifest.Id==update.Installed.Manifest.Id);
            if(existing is not null && (!string.Equals(existing.Manifest.GitHubRepository,update.Installed.Manifest.GitHubRepository,StringComparison.Ordinal) || Version.Parse(existing.Manifest.Version)>=Version.Parse(update.Version)))
                throw new InvalidOperationException("Plugin ist bereits gleich/neuer installiert oder verwendet eine andere Updatequelle.");
            if(MessageBox.Show(this,$"{update.Installed.Manifest.Id} · {update.Version} installieren?\nQuelle: github.com/{update.Installed.Manifest.GitHubRepository}\nKünftige Updates werden beim App-Start automatisch vorbereitet und nach Neustart verwendet. Dies lässt sich pro Plugin ausschalten.\nPlugins führen Code mit deinen Benutzerrechten aus. Neue Plugins bleiben zunächst deaktiviert.","Plugin von GitHub",MessageBoxButton.YesNo)!=MessageBoxResult.Yes) { PluginStatus.Text="Installation abgebrochen."; return; }
            await StagePlugin(update);
            var id=update.Installed.Manifest.Id;
            if(!settings.AutoUpdatePlugins.Contains(id)) settings.AutoUpdatePlugins.Add(id);
            settings.CheckPluginUpdatesOnStartup=true; PluginUpdateCheck.IsChecked=true; settings.Save();
            PluginRepositoryLink.Clear();
        });
    }
}
