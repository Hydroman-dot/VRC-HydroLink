using System.Windows;
using System.Windows.Controls;
using HydroLink.Core;
using Microsoft.Win32;

namespace HydroLink.App;
public partial class MainWindow
{
    private readonly Dictionary<string, Button> moduleButtons = [];
    private readonly Dictionary<string, List<TextBlock>> metricViews = [];
    private readonly Dictionary<string, TextBlock> moduleStates = [];
    private readonly List<FrameworkElement> launchEditors = [];
    private ProgramLauncher? programLauncher;
    private CheckBox? startWithApp;
    private FitnessSession? fitnessSession;
    private TextBox? sessionName, sessionNote;
    private Button? recordButton;
    private Action? applyWindControls;
    private readonly SemaphoreSlim recordingStopGate = new(1, 1);
    private async Task StopRecording()
    {
        await recordingStopGate.WaitAsync();
        try
        {
            if (fitnessSession is null) return;
            waitForRest = true; movingSince = null;
            await fitnessSession.StopAsync(sessionNote?.Text ?? ""); Log("Session gespeichert: " + fitnessSession.CsvPath); fitnessSession = null;
            if (recordButton is not null) recordButton.Content = "Session aufzeichnen";
        }
        finally { recordingStopGate.Release(); }
    }
    private StackPanel Page(string title, string description)
    {
        var panel = new StackPanel { Margin = new Thickness(24) };
        panel.Children.Add(new TextBlock { Text = title, FontSize = 25, FontWeight = FontWeights.SemiBold });
        panel.Children.Add(new TextBlock { Text = description, Foreground = Color("#93ADC5"), Margin = new Thickness(0, 8, 0, 20) }); return panel;
    }
    private void AddModulePage(string id, string title, string description, (string Title, string Topic)[] metrics)
    {
        var panel = Page(title, description);
        var buttons = new WrapPanel();
        var start = new Button { Content = "Tool starten", Tag = id }; start.Click += async (_, _) => await ToggleModule(id); moduleButtons[id] = start; buttons.Children.Add(start);
        var state = new TextBlock { Text = "Gestoppt", Margin = new Thickness(12), Foreground = Color("#7CE8CF") }; moduleStates[id] = state; buttons.Children.Add(state); panel.Children.Add(buttons);
        if (id == "fitness")
        {
            var session = new StackPanel(); sessionName = new TextBox { Text = "VR Training" }; sessionNote = new TextBox { Text = "", ToolTip = "Session-Notiz" };
            session.Children.Add(new TextBlock { Text = "Sessionname" }); session.Children.Add(sessionName); session.Children.Add(new TextBlock { Text = "Notiz" }); session.Children.Add(sessionNote);
            recordButton = new Button { Content = "Session aufzeichnen" };
            recordButton.Click += async (_, _) =>
            {
                recordButton.IsEnabled = false;
                try
                {
                    if (fitnessSession?.Running == true) await StopRecording();
                    else
                    {
                        if (fitnessSession is not null) await StopRecording();
                        await EnsureHost(); await host!.SetModuleAsync("fitness", true);
                        fitnessSession = new FitnessSession(host.Bus, settings.FitnessOptions.DataDirectory, settings); fitnessSession.Start(sessionName.Text);
                        recordButton.Content = "Session stoppen und speichern";
                    }
                }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Aufzeichnung"); }
                finally { recordButton.IsEnabled = true; }
            };
            session.Children.Add(recordButton);
            session.Children.Add(ActionButton("Gespeicherte Sessions öffnen", () => { var folder = settings.FitnessOptions.DataDirectory; System.IO.Directory.CreateDirectory(folder); System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(folder) { UseShellExecute = true }); }));
            session.Children.Add(BuildFitnessOptions());
            panel.Children.Add(session);
        }
        if (id == "windforce")
        {
            var manual = new CheckBox { Content = "Manuelle Lüftersteuerung" };
            var slider = new Slider { Minimum = 0, Maximum = 100, TickFrequency = 5, Width = 400, HorizontalAlignment = HorizontalAlignment.Left, Margin = new Thickness(0, 10, 0, 10) };
            var label = new TextBlock { Text = "0 %" };
            slider.ValueChanged += (_, _) => { label.Text = $"{slider.Value:0} %"; host?.Wind?.SetManual(manual.IsChecked == true, slider.Value); };
            manual.Checked += (_, _) => host?.Wind?.SetManual(true, slider.Value); manual.Unchecked += (_, _) => host?.Wind?.SetManual(false, 0);
            panel.Children.Add(manual); panel.Children.Add(slider); panel.Children.Add(label);
            var pause = new CheckBox { Content = "Lüfter pausieren (Daten bleiben aktiv)" }; pause.Checked += (_, _) => host?.Wind?.Pause(true); pause.Unchecked += (_, _) => host?.Wind?.Pause(false); panel.Children.Add(pause);
            applyWindControls = () => { host?.Wind?.SetManual(manual.IsChecked == true, slider.Value); host?.Wind?.Pause(pause.IsChecked == true); };
            BuildWindConfiguration(panel);
        }
        if (id == "haptics")
        {
            BuildHapticsControls(panel);
        }
        if (id == "osc") BuildOscExplorer(panel);
        if (id == "fitness") metrics = metrics.Concat(new (string, string)[] { ("Session-Distanz · km", "session/distance-km"), ("Session-Schritte", "session/steps"), ("Session aktiv · s", "session/active-seconds"), ("Session Dauer · s", "session/elapsed-seconds"), ("Kalorien · geschätzt", "session/calories-estimate"), ("Gültiger Puls · %", "session/heart-quality") }).ToArray();
        var cards = new WrapPanel();
        foreach (var (name, topic) in metrics)
        {
            var cell = new StackPanel(); cell.Children.Add(new TextBlock { Text = name, Foreground = Color("#93ADC5") });
            var value = new TextBlock { Text = "—", FontSize = 20, Margin = new Thickness(0, 10, 0, 0) }; cell.Children.Add(value);
            if (!metricViews.TryGetValue(topic, out var list)) metricViews[topic] = list = []; list.Add(value);
            cards.Children.Add(new Border { Child = cell, Padding = new Thickness(16), Width = 280, MinHeight = 94, Margin = new Thickness(0, 0, 12, 12), Background = Color("#203248"), CornerRadius = new CornerRadius(8) });
        }
        panel.Children.Add(cards);
        Tabs.Items.Insert(2 + moduleButtons.Count - 1, new TabItem { Header = title, Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
    }
    private void BuildToolPages()
    {
        AddModulePage("fitness", "VR Fitness", "Eigene Live-Daten und Quellen. WindForce, OSC Hub und Haptics können unabhängig weiterlaufen.",
        [ ("Puls · bpm", "fitness/heart/bpm"), ("Geschwindigkeit · km/h", "fitness/speed-kmh"), ("Distanz gesamt · km", "fitness/distance-km"), ("Schritte gesamt", "fitness/steps"), ("Aktive Zeit · s", "fitness/active-seconds"), ("Zielgeschwindigkeit · km/h", "fitness/target-speed-kmh"), ("Geschwindigkeitslimit · km/h", "fitness/speed-limit-kmh"), ("AutoWalk", "fitness/autowalk-speed"), ("Laufband verbunden", "fitness/connected"), ("Laufband läuft", "fitness/running"), ("Health Schritte heute", "companion/steps_today"), ("SpO₂ · %", "companion/spo2_pct"), ("Gewicht · kg", "companion/weight_kg") ]);
        AddModulePage("windforce", "WindForce", "Quellenwahl und Prioritäten direkt einstellen. Steuerung, Gerätewerte und Eingänge auf einer Seite.",
        [ ("Lüfterausgabe · %", "windforce/output-percent"), ("Aktive Quelle", "windforce/source"), ("Zielwert · %", "windforce/target-percent"), ("ESP32", "windforce/device/ip"), ("Fan 1 · %", "windforce/device/fan1"), ("Fan 2 · %", "windforce/device/fan2"), ("Fan 3 · %", "windforce/device/fan3"), ("Drehzahl · rpm", "windforce/device/rpm"), ("WLAN · dBm", "windforce/device/rssi"), ("PWM", "windforce/device/pwm"), ("Laufband · km/h", "windforce/input/speed"), ("Puls · bpm", "windforce/input/heart") ]);
        AddModulePage("osc", "OSC Hub", "OSCQuery findet den tatsächlichen OSC-Port. Die letzten Nachrichten erscheinen hier.", [("Letzte OSC-Nachricht", "hydrolink/osc-last"), ("Nachrichten", "hydrolink/osc-count")]);
        AddModulePage("haptics", "Haptics", "Max 2 und Ridge · eigene Regler · SPS-Plugs und Sockets automatisch verbinden.", []);
        var programs = Page("Programme automatisch starten", "Nur aktivierte Programme starten, wenn ein Tool ihre Quelle benötigt. Bereits laufende EXEs werden nicht doppelt gestartet. Beim Stoppen eines Tools bleiben externe Programme geöffnet.");
        startWithApp = new CheckBox { Content = "Ausgewählte Module mit HydroLink starten", IsChecked = settings.StartModulesWithApp }; programs.Children.Add(startWithApp);
        foreach (var program in settings.Programs)
        {
            programs.Children.Add(new TextBlock { Text = program.Name, FontSize = 19, Margin = new Thickness(0, 20, 0, 6) });
            var enabled = new CheckBox { Content = "Bei Bedarf automatisch starten", IsChecked = program.AutoStart };
            enabled.Checked += (_, _) => program.AutoStart = true; enabled.Unchecked += (_, _) => program.AutoStart = false;
            var path = new TextBox { Text = program.Path }; path.TextChanged += (_, _) => program.Path = path.Text.Trim();
            var arguments = new TextBox { Text = program.Arguments }; arguments.TextChanged += (_, _) => program.Arguments = arguments.Text;
            programs.Children.Add(enabled); programs.Children.Add(new TextBlock { Text = "Programmdatei" }); programs.Children.Add(path);
            var row = new WrapPanel(); row.Children.Add(ActionButton("EXE auswählen", () => { var chooser = new OpenFileDialog { Filter = "Programme (*.exe)|*.exe" }; if (chooser.ShowDialog(this) == true) path.Text = chooser.FileName; }));
            if (program.Provider == "VRTI") row.Children.Add(ActionButton("VRTI über Steam", () => path.Text = "steam://rungameid/4411910"));
            row.Children.Add(ActionButton("Jetzt starten", () => { try { programLauncher!.Start(program); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } }));
            programs.Children.Add(row); programs.Children.Add(new TextBlock { Text = "Optionale Startargumente" }); programs.Children.Add(arguments);
            launchEditors.Add(enabled); launchEditors.Add(path); launchEditors.Add(arguments); launchEditors.Add(row);
        }
        programs.Children.Add(ActionButton("Einstellungen speichern", () => Save_Click(this, new RoutedEventArgs())));
        Tabs.Items.Insert(Tabs.Items.Count - 1, new TabItem { Header = "Programme", Content = new ScrollViewer { Content = programs, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
    }
    private async Task EnsureHost()
    {
        if (host is not null) return;
        settings = ReadSettings(); settings.Save();
        host = new HydroHost(settings, Log, programLauncher);
        subscription = host.Bus.Subscribe("", item =>
        {
            if (item.Topic == "osc/avatar-id")
                foreach (var key in latest.Keys.Where(k => k.StartsWith("osc/value/", StringComparison.Ordinal) || k.StartsWith("osc/sent", StringComparison.Ordinal) || k.StartsWith("osc/receive/", StringComparison.Ordinal))) latest.TryRemove(key, out _);
            if (item.Topic.StartsWith("osc/receive/"))
            {
                var total = Interlocked.Increment(ref oscMessages);
                latest["hydrolink/osc-count"] = item with { Value = System.Text.Json.JsonSerializer.SerializeToElement(total) };
                latest["hydrolink/osc-last"] = item;
            }
            if (latest.Count < 20000 || latest.ContainsKey(item.Topic)) latest[item.Topic] = item;
        });
        await host.StartPluginsAsync();
        PluginStatus.Text = string.Join("\n", host.Plugins!.Plugins.Select(p => p.Name + (p.Enabled ? " · aktiviert" : " · deaktiviert")));
        foreach (var page in host.Plugins.Pages)
            try { if (page.CreateView() is FrameworkElement element) Tabs.Items.Add(new TabItem { Header = page.Title, Content = element, Tag = "plugin" }); } catch (Exception ex) { Log("Plugin-Seite: " + ex.Message); }
        SettingsPanel.IsEnabled = OscSettings.IsEnabled = false;
        foreach (var editor in launchEditors) editor.IsEnabled = false;
        StopButton.IsEnabled = true;
    }
    private async Task ToggleModule(string id)
    {
        moduleButtons[id].IsEnabled = false;
        try { await EnsureHost(); if (id == "fitness" && host!.ActiveModules.Contains(id)) await StopRecording(); await host!.SetModuleAsync(id, !host.ActiveModules.Contains(id)); if (id == "windforce") applyWindControls?.Invoke(); }
        catch (Exception ex) { Log("Modul: " + ex.Message); MessageBox.Show(this, ex.Message, "Modul konnte nicht starten"); }
        finally { moduleButtons[id].IsEnabled = true; RefreshToolPages(); }
    }
    private void RefreshToolPages()
    {
        foreach (var (id, button) in moduleButtons)
        {
            bool active = host?.ActiveModules.Contains(id) == true;
            button.Content = active ? "Tool stoppen" : "Tool starten";
            moduleStates[id].Text = active ? "Aktiv · Verbindungsstatus in Aktivität" : "Gestoppt";
        }
        foreach (var (topic, controls) in metricViews)
            foreach (var control in controls)
            {
                if (!latest.TryGetValue(topic, out var data)) { control.Text = "—"; continue; }
                var stale = DateTimeOffset.UtcNow - data.Timestamp > TimeSpan.FromSeconds(10);
                control.Text = data.Value.ValueKind == System.Text.Json.JsonValueKind.Number ? data.Value.GetDouble().ToString("0.##") : data.Value.ToString();
                if (control.Text.Length > 300) control.Text = control.Text[..300] + "…";
                control.Opacity = stale ? .55 : 1; control.ToolTip = $"{data.Source} · {data.Timestamp.ToLocalTime():HH:mm:ss}" + (stale ? " · letzter Wert" : "");
            }
    }
}
