using System.Text;
using System.Windows;
using System.Windows.Controls;
using HydroLink.Core;
using Microsoft.Win32;

namespace HydroLink.App;
public partial class MainWindow
{
    private TextBox? diagnosticsText;
    private string ModuleDiagnosis(string id)
    {
        if (host == null || !host.ActiveModules.Contains(id) && !(id == "osc" && host.Running.Contains("osc"))) return "Aus";
        var dependencies = host.ModuleSources(id);
        var services = host.ServiceDiagnostics;
        foreach (var source in dependencies)
        {
            var service = services.FirstOrDefault(s => s.Id == source);
            if (service.Id == null) return "Fehler · Dienst fehlt: " + source + ". Modul neu starten.";
            if (service.State == "Fehler" || service.State == "Aus") return "Fehler · " + source + ": " + service.Detail;
            if (service.State == "Startet") return "Startet · " + source;
            if (latest.TryGetValue("provider/" + source + "/status", out var status) && status.Value.ToString() == "disconnected") return "Verbindung verloren · " + source + " verbindet erneut";
        }
        if (dependencies.Contains("osc") && host.Osc?.HasTarget != true) return "Wartet auf Verbindung · VRChat starten und OSC einschalten";
        if (id == "haptics")
        {
            if (host.Haptics?.Backend.Connected != true) return "Wartet auf Verbindung · Intiface starten";
            if (host.Haptics.Backend.Devices.Count == 0) return "Wartet auf Gerät · Toy mit Intiface verbinden";
            return "Bereit · Gerät verbunden; Ausgabe im Toy-Reiter prüfen";
        }
        var prefix = id switch { "fitness" => "fitness/", "windforce" => "windforce/", _ => "osc/receive/" };
        var data = latest.Values.Where(x => x.Topic.StartsWith(prefix, StringComparison.Ordinal)).OrderByDescending(x => x.Timestamp).FirstOrDefault();
        if (data == null) return "Wartet auf Daten · bisher keine Werte empfangen";
        var age = DateTimeOffset.UtcNow - data.Timestamp;
        return age > TimeSpan.FromSeconds(10) ? $"Wartet auf Daten · letzter Wert vor {age.TotalSeconds:F0} s" : "Bereit · aktuelle Daten vorhanden";
    }
    private void BuildDiagnosticsPage()
    {
        var panel = Page("Diagnose", "Modulzustand, Verbindungen und Datenalter an einer Stelle. Bereit bedeutet nicht, dass ein Gerät eine Bewegung bestätigt hat.");
        var actions = new WrapPanel();
        foreach (var (id, label) in new[] { ("fitness", "Fitness neu starten"), ("windforce", "WindForce neu starten"), ("osc", "OSC neu starten"), ("haptics", "Haptics neu starten") })
        {
            var button = new Button { Content = label, Tag = id }; button.Click += RestartModule_Click; actions.Children.Add(button);
        }
        var export = new Button { Content = "Diagnosebericht speichern" };
        export.Click += (_, _) =>
        {
            var dialog = new SaveFileDialog { Filter = "Textdatei|*.txt", FileName = "HydroLink-Diagnose.txt" };
            if (dialog.ShowDialog(this) != true) return;
            try { System.IO.File.WriteAllText(dialog.FileName, DiagnosisReport()); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Diagnosebericht"); }
        };
        actions.Children.Add(export); panel.Children.Add(actions);
        diagnosticsText = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MinHeight = 330, Margin = new Thickness(0, 16, 0, 0) };
        diagnosticsText.Text = DiagnosisReport();
        panel.Children.Add(diagnosticsText);
        panel.Children.Add(new TextBlock { Text = "Der Bericht enthält Modul- und Dienstzustände, jedoch keine gespeicherten Zugangsschlüssel oder vollständigen Einstellungen. Fehlermeldungen vor dem Weitergeben auf persönliche Angaben prüfen.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) });
        Tabs.Items.Add(new TabItem { Header = "Diagnose", Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
    }
    private string DiagnosisReport()
    {
        var report = new StringBuilder("HydroLink 0.14.0 · " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz") + "\n\n");
        foreach (var (id, title) in new[] { ("fitness", "VR Fitness"), ("windforce", "WindForce"), ("osc", "OSC Hub"), ("haptics", "Haptics") }) report.AppendLine(title + ": " + ModuleDiagnosis(id));
        report.AppendLine("\nHintergrunddienste:");
        if (host == null || host.Running.Count == 0) report.AppendLine("Keine Dienste angefordert.");
        else foreach (var service in host.ServiceDiagnostics) report.AppendLine(service.Id + ": " + service.State + " · " + service.Detail);
        if (host?.Osc is { } osc) report.AppendLine("\nOSC-Parameterabfrage: " + osc.QueryStatus);
        return report.ToString();
    }
    private void RefreshDiagnostics()
    {
        if (diagnosticsText != null) diagnosticsText.Text = DiagnosisReport();
    }
}
