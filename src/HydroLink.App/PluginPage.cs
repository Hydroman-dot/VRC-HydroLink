using System.IO;
using System.Windows;
using System.Windows.Controls;
using HydroLink.Core;
namespace HydroLink.App;
public partial class MainWindow
{
    private readonly Dictionary<string,PluginUpdate> pluginUpdates = [];
    private bool pluginRestartRequired;
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
        PluginRows.Children.Clear(); PluginViews.Items.Clear(); PluginViews.Visibility=Visibility.Collapsed;
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
            var enabled=new CheckBox { Content="Plugin freigeben", IsChecked=settings.EnabledPlugins.Contains(id),IsEnabled=!active && !pluginRestartRequired };
            enabled.Click+=(_,_)=>{ settings.EnabledPlugins.RemoveAll(x=>x==id); if(enabled.IsChecked==true) settings.EnabledPlugins.Add(id); PluginIds.Text=string.Join(", ",settings.EnabledPlugins); settings.Save(); RefreshPluginRows(); };
            panel.Children.Add(enabled);
            var auto=new CheckBox { Content="Mit HydroLink starten", IsChecked=settings.AutoStartPlugins.Contains(id), IsEnabled=enabled.IsChecked==true };
            auto.Click+=(_,_)=>{ settings.AutoStartPlugins.RemoveAll(x=>x==id); if(auto.IsChecked==true) settings.AutoStartPlugins.Add(id); settings.Save(); };
            panel.Children.Add(auto);
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
        if(host?.Plugins is {} runtime) foreach(var page in runtime.Pages)
            try { if(page.CreateView() is FrameworkElement view) { PluginViews.Items.Add(new TabItem { Header=page.Title,Content=view }); PluginViews.Visibility=Visibility.Visible; } } catch(Exception ex) { Log("Plugin-Seite: "+ex.Message); }
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
        var failures=new List<string>();
        try {
            using var http=new System.Net.Http.HttpClient { Timeout=TimeSpan.FromSeconds(15) }; var updater=new PluginUpdates(http);
            using var deadline=new CancellationTokenSource(TimeSpan.FromSeconds(45));
            foreach(var entry in Catalog.Discover(Path.Combine(AppContext.BaseDirectory,"plugins")))
                try { var update=await updater.CheckAsync(entry,deadline.Token); if(update is not null) pluginUpdates[entry.Manifest.Id]=update; }
                catch(Exception ex) { failures.Add(entry.Manifest.Name+": "+ex.Message); }
            PluginStatus.Text=(pluginRestartRequired?"Neue Versionsauswahl wird nach Neustart wirksam.\n":"")+$"{pluginUpdates.Count} Plugin-Update(s) verfügbar."+(failures.Count>0?"\nNicht geprüft: "+string.Join("; ",failures):"");
        } catch(Exception ex) { PluginStatus.Text="Update-Prüfung fehlgeschlagen: "+ex.Message; }
        finally { PluginPanel.IsEnabled=true; RefreshPluginRows(); }
    }
    private async Task InstallPluginUpdate(PluginUpdate update)
    {
        if(MessageBox.Show(this,$"{update.Installed.Manifest.Name} auf {update.Version} aktualisieren?\nQuelle: github.com/{update.Installed.Manifest.GitHubRepository}\nWirksam nach HydroLink-Neustart. Die vorherige Version bleibt erhalten.","Plugin-Update",MessageBoxButton.YesNo)!=MessageBoxResult.Yes) return;
        await PluginAction(async()=>{
            var temporary=Path.GetTempFileName();
            try {
                using var http=new System.Net.Http.HttpClient { Timeout=TimeSpan.FromMinutes(2) };
                using var deadline=new CancellationTokenSource(TimeSpan.FromMinutes(2));
                await new PluginUpdates(http).DownloadAsync(update,temporary,deadline.Token);
                Catalog.Install(temporary,update.Installed.Manifest.Id,update.Version,update.Installed.Manifest.GitHubRepository);
                pluginUpdates.Remove(update.Installed.Manifest.Id); RequirePluginRestart();
            } finally { File.Delete(temporary); }
        });
    }
}
