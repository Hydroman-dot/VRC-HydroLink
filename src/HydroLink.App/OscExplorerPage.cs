using System.Globalization;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using HydroLink.Core;
namespace HydroLink.App;
public partial class MainWindow
{
    private DataGrid? oscGrid;
    private TextBox? oscFilter;
    private ComboBox? oscDirection;
    private TextBlock? oscExplorerStatus;
    private ListBox? routeList;
    private readonly ObservableCollection<OscParameter> oscRows = [];
    private void BuildOscExplorer(StackPanel panel)
    {
        oscExplorerStatus = new TextBlock { Margin = new Thickness(0, 10, 0, 10) }; panel.Children.Add(oscExplorerStatus);
        oscFilter = new TextBox { ToolTip = "Parametername suchen", Width = 420, Margin = new Thickness(0, 4, 12, 12) };
        panel.Children.Add(new TextBlock { Text = "Parameter suchen / Richtung filtern" }); var filters = new WrapPanel(); panel.Children.Add(filters); filters.Children.Add(oscFilter);
        oscDirection = new ComboBox { ItemsSource = new[] { "Alle Parameter", "Vom Avatar empfangen", "Zum Avatar gesendet" }, SelectedIndex = 0, Width = 300 }; filters.Children.Add(oscDirection);
        oscGrid = new DataGrid { AutoGenerateColumns = false, IsReadOnly = true, CanUserAddRows = false, Height = 300, SelectionMode = DataGridSelectionMode.Single, HeadersVisibility = DataGridHeadersVisibility.Column, Background = Color("#0E1724"), Foreground = Color("#E5EDF7"), RowBackground = Color("#172334"), AlternatingRowBackground = Color("#203248"), GridLinesVisibility = DataGridGridLinesVisibility.Horizontal };
        foreach (var (name, property, width) in new[] { ("Parameter", "Address", 320), ("Typ", "Type", 50), ("Zugriff", "Access", 120), ("OSCQuery", "QueryValue", 120), ("Empfangen", "Received", 120), ("Gesendet", "Sent", 120), ("Letztes UDP", "Updated", 110) })
            oscGrid.Columns.Add(new DataGridTextColumn { Header = name, Binding = new Binding(property), Width = width });
        panel.Children.Add(oscGrid);
        oscGrid.Height = 220;
        oscGrid.ItemsSource = oscRows;
        panel.Children.Add(new TextBlock { Text = "OSCQuery = abgefragter Wert. Gesendet = UDP-Ausgabe, keine Empfangsbestätigung. Letzte Werte bleiben bis zum Avatarwechsel sichtbar.", Foreground = Color("#93ADC5"), Margin = new Thickness(0, 8, 0, 8) });
        var actions = new WrapPanel();
        void AddOscCard(bool sent)
        {
            if (oscGrid.SelectedItem is not OscParameter row || settings.Dashboard.Count >= 60) return;
            settings.Dashboard.Add(new DashboardCard { Title = row.Address.Split('/').Last() + (sent ? " · gesendet" : ""), Topic = (sent ? "osc/sent-value" : "osc/value") + row.Address, Kind = "Wert" }); PersistLayout(); BuildDashboard();
        }
        actions.Children.Add(ActionButton("Empfang als Karte", () => AddOscCard(false)));
        actions.Children.Add(ActionButton("Ausgabe als Karte", () => AddOscCard(true)));
        actions.Children.Add(ActionButton("Parameter zuordnen", () => EditOscRoute(null, (oscGrid.SelectedItem as OscParameter)?.Address)));
        panel.Children.Insert(panel.Children.IndexOf(oscGrid), actions);
        panel.Children.Add(new TextBlock { Text = "Zuordnungen · Änderungen gelten sofort, Ziel-Tool separat starten", FontSize = 18, Margin = new Thickness(0, 14, 0, 8) });
        routeList = new ListBox { Height = 150, DisplayMemberPath = "Label" }; panel.Children.Add(routeList);
        var routeActions = new WrapPanel();
        routeActions.Children.Add(ActionButton("Neue Zuordnung", () => EditOscRoute(null)));
        routeActions.Children.Add(ActionButton("Bearbeiten", () => { if (routeList.SelectedItem is OscRoute route) EditOscRoute(route); }));
        routeActions.Children.Add(ActionButton("An / Aus", () => { if (routeList.SelectedItem is OscRoute route) SaveOscRoutes(settings.OscRoutes.Select(r => r.Id == route.Id ? r with { Enabled = !r.Enabled } : r).ToList()); }));
        routeActions.Children.Add(ActionButton("Entfernen", () => { if (routeList.SelectedItem is OscRoute route) SaveOscRoutes(settings.OscRoutes.Where(r => r.Id != route.Id).ToList()); }));
        panel.Children.Add(routeActions); RefreshRouteList();
    }
    private void RefreshRouteList() { if (routeList is not null) routeList.ItemsSource = settings.OscRoutes.ToArray(); }
    private bool SaveOscRoutes(List<OscRoute> routes)
    {
        try
        {
            OscRouting.ValidateRoutes(routes);
            var previous = settings.OscRoutes; settings.OscRoutes = routes;
            try { settings.Save(); } catch { settings.OscRoutes = previous; throw; }
            host?.Routing.Replace(routes); RefreshRouteList(); Log("OSC-Zuordnungen gespeichert; Haptics-Ausgabe bei Änderungen erneut freigeben.");
            return true;
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Zuordnung prüfen"); return false; }
    }
    private void EditOscRoute(OscRoute? existing, string? selectedSource = null)
    {
        var original = existing ?? new OscRoute { Source = selectedSource ?? "fitness/heart/bpm" };
        var panel = new StackPanel { Margin = new Thickness(20) };
        TextBox Field(string title, string value) { panel.Children.Add(new TextBlock { Text = title }); var input = new TextBox { Text = value }; panel.Children.Add(input); return input; }
        var source = Field("Quelle: OSC-Adresse oder HydroLink-Topic", original.Source);
        var targets = new ComboBox { ItemsSource = new[] { "haptics/input/vibrate", "haptics/input/pump", "windforce/input/mapped", "/avatar/parameters/" }, SelectedItem = original.Target }; panel.Children.Add(targets);
        var target = Field("Ziel: Avatar-Adresse oder oben gewählter Tool-Kanal", original.Target); targets.SelectionChanged += (_, _) => { if (targets.SelectedItem is string s) target.Text = s; };
        var profile = Field("Avatar-ID (* = alle Avatare)", original.Avatar);
        panel.Children.Add(ActionButton("Aktuellen Avatar übernehmen", () => { if (host?.Routing.Avatar is { Length: > 0 } id) profile.Text = id; }));
        var type = new ComboBox { ItemsSource = new[] { "f", "i", "T", "s" }, SelectedItem = original.Type }; panel.Children.Add(new TextBlock { Text = "Ausgabetyp: f = Float, i = Int, T = Bool, s = Text (ohne Skalierung)" }); panel.Children.Add(type);
        var scale = Field("Faktor", original.Scale.ToString(CultureInfo.InvariantCulture));
        var offset = Field("Offset", original.Offset.ToString(CultureInfo.InvariantCulture));
        var min = Field("Minimum", original.Min.ToString(CultureInfo.InvariantCulture));
        var max = Field("Maximum", original.Max.ToString(CultureInfo.InvariantCulture));
        var invert = new CheckBox { Content = "Eingang invertieren (1 − Wert)", IsChecked = original.Invert }; panel.Children.Add(invert);
        var enabled = new CheckBox { Content = "Zuordnung aktiv", IsChecked = original.Enabled }; panel.Children.Add(enabled);
        var dialog = new Window { Title = "OSC-Zuordnung", Owner = this, Width = 570, Height = 780, Background = Color("#172334"), Foreground = Color("#E5EDF7"), Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto }, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        panel.Children.Add(ActionButton("Speichern", () =>
        {
            try
            {
                double Num(TextBox input) => double.Parse(input.Text.Replace(',', '.'), CultureInfo.InvariantCulture);
                var route = original with { Source = source.Text.Trim(), Target = target.Text.Trim(), Avatar = profile.Text.Trim(), Type = (string)type.SelectedItem, Scale = Num(scale), Offset = Num(offset), Min = Num(min), Max = Num(max), Invert = invert.IsChecked == true, Enabled = enabled.IsChecked == true };
                var routes = settings.OscRoutes.Where(r => r.Id != route.Id).Append(route).ToList(); OscRouting.ValidateRoutes(routes); if (SaveOscRoutes(routes)) dialog.Close();
            }
            catch (Exception ex) { MessageBox.Show(dialog, ex.Message, "Eingaben prüfen"); }
        })); dialog.ShowDialog();
    }
    private void RefreshOscExplorer()
    {
        if (oscGrid is null) return;
        var osc = host?.Osc;
        oscExplorerStatus!.Text = osc is null ? "OSC Hub starten, um Avatar-Parameter zu sehen." : $"VRChat: {osc.Target} · Avatar: {host!.Routing.Avatar}\n{osc.QueryStatus}";
        var selection = (oscGrid.SelectedItem as OscParameter)?.Address;
        var rows = (osc?.Monitor.Snapshot() ?? []).Where(r => r.Address.Contains(oscFilter!.Text, StringComparison.OrdinalIgnoreCase) && (oscDirection!.SelectedIndex == 0 || (oscDirection.SelectedIndex == 1 ? r.Received != "—" : r.Sent != "—"))).ToArray();
        if (oscRows.Count != rows.Length || !oscRows.Select(r => r.Address).SequenceEqual(rows.Select(r => r.Address)))
        { oscRows.Clear(); foreach (var row in rows) oscRows.Add(row); }
        else for (var i = 0; i < rows.Length; i++) if (oscRows[i] != rows[i]) oscRows[i] = rows[i];
        if (selection is not null) oscGrid.SelectedItem = oscRows.FirstOrDefault(r => r.Address == selection);
    }
    private void PreviewOscFixture()
    {
        oscExplorerStatus!.Text = "Vorschau · simulierte Testdaten · keine Geräteausgabe";
        oscRows.Clear();
        oscRows.Add(new("/avatar/parameters/MaxVibrate", "f", "Lesen / Schreiben", "[0.4]", "[0.4]", "—", "12:00:01.123"));
        oscRows.Add(new("/avatar/parameters/MaxPump", "f", "Lesen / Schreiben", "[0.2]", "[0.2]", "—", "12:00:01.124"));
        oscRows.Add(new("/avatar/parameters/HeartRate", "i", "Lesen / Schreiben", "[92]", "[92]", "[92]", "12:00:01.125"));
        routeList!.ItemsSource = new[] { new OscRoute { Enabled = true, Source = "fitness/heart/bpm", Target = "/avatar/parameters/HeartRate", Type = "i", Max = 240 } };
    }
}
