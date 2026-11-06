using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HydroLink.Core;
namespace HydroLink.App;
public partial class MainWindow
{
    private Button? routerStart;
    private TextBlock? routerState;
    private TextBox? routerHistory;
    private FrameworkElement? routerEditor;
    private TabItem BuildOscRouterPage()
    {
        var panel = Page("OSC Router", "Programme mit festen UDP-Ports verbinden. Der Router ist separat steuerbar; der OSC Hub übernimmt weiterhin die gemeinsame VRChat-Verbindung.");
        var row = new WrapPanel();
        routerStart = new Button { Content = "Router starten" };
        routerStart.Click += async (_, _) => { try { await EnsureHost(); await host!.SetModuleAsync("osc-router", !host.ActiveModules.Contains("osc-router")); } catch(Exception ex) { MessageBox.Show(this, ex.Message, "OSC Router"); } RefreshOscRouter(); };
        var restart = new Button { Content = "Router neu starten" };
        restart.Click += async (_, _) => { try { await EnsureHost(); await host!.RestartModuleAsync("osc-router"); } catch(Exception ex) { MessageBox.Show(this, ex.Message, "OSC Router"); } RefreshOscRouter(); };
        row.Children.Add(routerStart); row.Children.Add(restart); panel.Children.Add(row);
        routerState = new TextBlock { Text = "Gestoppt · keine Router-Weiterleitung", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,8,0,12) }; panel.Children.Add(routerState);
        panel.Children.Add(new TextBlock { Text = "Autostart unter Module → OSC Router auswählen und Einstellungen speichern. Änderungen hier nur bei gestopptem Router; die übrigen Module können weiterlaufen.", TextWrapping = TextWrapping.Wrap });
        var editor = new StackPanel(); routerEditor = editor; panel.Children.Add(editor);
        var profile = new TextBox { Text = settings.Router.Profile };
        var listen = new TextBox { Text = settings.Router.ListenAddress };
        editor.Children.Add(new TextBlock { Text = "Aktives Regelprofil" }); editor.Children.Add(profile);
        editor.Children.Add(new TextBlock { Text = "Empfangs-IP von HydroLink (127.0.0.1 = nur dieser PC)" }); editor.Children.Add(listen);
        var endpoints = new ObservableCollection<RouterEndpoint>(settings.Router.Copy().Endpoints);
        var rules = new ObservableCollection<RouterRule>(settings.Router.Copy().Rules);
        DataGrid Grid(object items)
        {
            return new DataGrid { ItemsSource = (System.Collections.IEnumerable)items, AutoGenerateColumns = false, CanUserAddRows = false, CanUserDeleteRows = true, Height = 180, Background = Color("#182638"), Foreground = Color("#E5EDF7"), RowBackground = Color("#182638"), AlternatingRowBackground = Color("#203248"), Margin = new Thickness(0,8,0,8), HorizontalScrollBarVisibility = ScrollBarVisibility.Auto };
        }
        void TextColumn(DataGrid grid, string title, string property) => grid.Columns.Add(new DataGridTextColumn { Header = title, Binding = new Binding(property) { Mode = BindingMode.TwoWay, UpdateSourceTrigger = UpdateSourceTrigger.LostFocus }, Width = DataGridLength.SizeToHeader });
        void CheckColumn(DataGrid grid, string title, string property) => grid.Columns.Add(new DataGridCheckBoxColumn { Header = title, Binding = new Binding(property) { Mode = BindingMode.TwoWay } });
        editor.Children.Add(new TextBlock { Text = "Programme / Endpunkte", FontSize = 19, Margin = new Thickness(0,16,0,0) });
        var endpointGrid = Grid(endpoints);
        TextColumn(endpointGrid,"Name","Name"); TextColumn(endpointGrid,"Programm-IP","Host"); TextColumn(endpointGrid,"HydroLink empfängt auf Port (0 = aus)","ReceivePort"); TextColumn(endpointGrid,"Programm empfängt auf Port","SendPort");
        editor.Children.Add(endpointGrid);
        editor.Children.Add(ActionButton("Endpunkt hinzufügen", () => endpoints.Add(new RouterEndpoint { Name = "Programm" + (endpoints.Count+1) })));
        editor.Children.Add(new TextBlock { Text = "Im anderen Programm den Sendeport auf den HydroLink-Empfangsport stellen. 9000/9001 werden nicht belegt. Hub steht für VRChat über die vorhandene OSC-Verbindung. Empfang akzeptiert nur Pakete von der eingetragenen Programm-IP.", TextWrapping = TextWrapping.Wrap });
        editor.Children.Add(new TextBlock { Text = "Weiterleitungsregeln", FontSize = 19, Margin = new Thickness(0,16,0,0) });
        var ruleGrid = Grid(rules);
        CheckColumn(ruleGrid,"An","Enabled"); TextColumn(ruleGrid,"Profil","Profile"); TextColumn(ruleGrid,"Avatar (* = alle)","Avatar"); TextColumn(ruleGrid,"Von","From"); TextColumn(ruleGrid,"Nach","To"); TextColumn(ruleGrid,"Adressfilter","Address"); TextColumn(ruleGrid,"Neue Adresse (leer = behalten)","Rename");
        CheckColumn(ruleGrid,"Umrechnen","ConvertValue"); CheckColumn(ruleGrid,"Invertieren","Invert"); TextColumn(ruleGrid,"Faktor","Scale"); TextColumn(ruleGrid,"Offset","Offset"); TextColumn(ruleGrid,"Minimum","Min"); TextColumn(ruleGrid,"Maximum","Max");
        editor.Children.Add(ruleGrid);
        editor.Children.Add(ActionButton("Regel hinzufügen", () => rules.Add(new RouterRule { Profile = profile.Text.Trim(), To = endpoints.FirstOrDefault()?.Name ?? "Programm" })));
        editor.Children.Add(new TextBlock { Text = "Eine Regel je Ziel anlegen. Filter: exakte OSC-Adresse oder Präfix mit * am Ende. Umrechnung gilt für den ersten Wert und gibt Float aus; ohne Umrechnung bleiben die Werte erhalten. Zeilen mit Entf entfernen. Echo-Schutz: gleiche Rückmeldungen vom Ziel werden 2 s unterdrückt; bei mehr als 500 Nachrichten/s pausiert der Router 5 s.", TextWrapping = TextWrapping.Wrap });
        editor.Children.Add(ActionButton("Router-Einstellungen speichern", () => {
            try {
                if (!endpointGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !endpointGrid.CommitEdit(DataGridEditingUnit.Row, true) || !ruleGrid.CommitEdit(DataGridEditingUnit.Cell, true) || !ruleGrid.CommitEdit(DataGridEditingUnit.Row, true)) throw new ArgumentException("Ungültige Tabellenwerte korrigieren.");
                var options = new OscRouterOptions { Profile = profile.Text.Trim(), ListenAddress = listen.Text.Trim(), Endpoints = endpoints.ToList(), Rules = rules.ToList() }; options.Validate();
                var previous = settings.Router;
                try { host?.SetRouterOptions(options); settings.Router = options.Copy(); settings.Save(); }
                catch { settings.Router = previous; host?.SetRouterOptions(previous); throw; }
                Log("Router-Einstellungen gespeichert.");
            } catch(Exception ex) { MessageBox.Show(this, ex.Message, "Router-Einstellungen prüfen"); }
        }));
        routerHistory = new TextBox { IsReadOnly = true, Height = 180, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,16,0,0) }; panel.Children.Add(routerHistory);
        return new TabItem { Header = "OSC Router", Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
    }
    private void RefreshOscRouter()
    {
        if (routerStart == null) return;
        var active = host?.ActiveModules.Contains("osc-router") == true;
        routerStart.Content = active ? "Router stoppen" : "Router starten";
        routerEditor!.IsEnabled = !active;
        var router = host?.Router;
        routerState!.Text = router == null ? "Gestoppt · keine Router-Weiterleitung" : $"{router.Health.State} · {router.Status}\nEmpfangen: {router.Received} · weitergegeben: {router.Sent} · verworfen: {router.Dropped}" + (router.Health.State == "Fehler" ? "\n" + router.Health.Detail : "");
        var text = router == null ? "Noch keine Weiterleitungen in dieser Sitzung." : string.Join("\n", router.History.Reverse());
        if (routerHistory!.Text != text) routerHistory.Text = text;
    }
}
