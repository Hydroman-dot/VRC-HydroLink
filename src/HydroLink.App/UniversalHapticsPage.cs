using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using HydroLink.Core;
using Microsoft.Win32;
using VrcHapticsBridge.Core;

namespace HydroLink.App;
public partial class MainWindow
{
    private StackPanel? universalFeatures;
    private ListBox? universalSources;
    private ComboBox? universalMode, universalProfile;
    private TextBox? socketLength;
    private TextBlock? universalStatus, inventoryStatus;
    private readonly List<(HapticFeature Feature, CheckBox Selected, Slider Limit)> featureRows = [];
    private readonly List<Button> universalButtons = [];
    private readonly Dictionary<string, Slider> universalGains = [];
    private CheckBox? universalClockwise;
    private string featureSignature = "", sourceSignature = "";
    private HapticsService? universalService;
    private bool universalBusy;
    private void BuildUniversalHaptics(StackPanel panel)
    {
        panel.Children.Add(new TextBlock { Text = "Gemeinsam · Geräte nach Fähigkeiten", FontSize = 21, Margin = new Thickness(0, 12, 0, 8) });
        panel.Children.Add(new TextBlock { Text = "1. Tool starten und Funktionen wählen. 2. Quelle übernehmen. 3. Ausgabe freigeben und Werte im Avatar-Menü oder unten anwenden.",
            TextWrapping = TextWrapping.Wrap });
        inventoryStatus = new TextBlock { Text = "Wizard-Datei optional: dient zum Abgleich; echte Quellen kommen aus VRChat.", TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(ActionButton("Wizard-Datei importieren", () => {
            try {
                var dialog = new OpenFileDialog { Filter = "HydroLink Vertrag (*.json)|*.json" };
                if (dialog.ShowDialog(this) != true) return;
                var inventory = WizardInventory.Read(dialog.FileName);
                inventoryStatus.Text = $"{inventory.Avatar}: {inventory.Plugs} Plugs, {inventory.Sockets} Sockets. Kandidaten importiert; keine automatische Freigabe.";
            } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Wizard-Import"); }
        }));
        panel.Children.Add(inventoryStatus);
        universalFeatures = new StackPanel { Margin = new Thickness(0, 8, 0, 8) }; panel.Children.Add(universalFeatures);
        universalMode = new ComboBox { ItemsSource = new[] { "Avatar-Menü / manuelle Werte", "SPS-Plugs und Sockets", "Bisherige MaxVibrate / MaxPump" }, SelectedIndex = 0, MinWidth = 320, HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Add(new TextBlock { Text = "Gemeinsame Eingangsquelle" }); panel.Children.Add(universalMode);
        universalSources = new ListBox { SelectionMode = SelectionMode.Multiple, MaxHeight = 150, MinHeight = 45 };
        panel.Children.Add(universalSources);
        var lengthPanel = new WrapPanel();
        lengthPanel.Children.Add(new TextBlock { Text = "Kalibrierlänge für gewählte Sockets (cm): ", VerticalAlignment = VerticalAlignment.Center });
        socketLength = new TextBox { Text = "20", Width = 70 }; lengthPanel.Children.Add(socketLength); panel.Children.Add(lengthPanel);
        panel.Children.Add(new TextBlock { Text = "SPS: Quelle in VRChat aktivieren. Socket-Länge an das verwendete Gegenstück anpassen. Mehrere Quellen werden mit Maximum zusammengeführt.",
            TextWrapping = TextWrapping.Wrap, Foreground = Color("#93ADC5") });
        var buttons = new WrapPanel(); panel.Children.Add(buttons);
        void AddButton(string title, Func<HapticsService, Task> action)
        {
            var button = new Button { Content = title };
            button.Click += async (_, _) => {
                if (universalBusy || host?.Haptics is not { } service) return;
                universalBusy = true; RefreshUniversalHaptics();
                try { await action(service); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "Gemeinsame Geräteausgabe"); }
                finally { universalBusy = false; RefreshUniversalHaptics(); }
            };
            universalButtons.Add(button); buttons.Children.Add(button);
        }
        AddButton("Auswahl übernehmen", async service => {
            var length = double.Parse(socketLength.Text, CultureInfo.CurrentCulture) / 100;
            var selected = universalSources.SelectedItems.Cast<SpsSource>().Select(s => s with { LengthMeters = length }).ToArray();
            var mode = universalMode.SelectedIndex switch { 1 => UniversalSourceMode.Sps, 2 => UniversalSourceMode.Legacy, _ => UniversalSourceMode.Menu };
            if (mode == UniversalSourceMode.Sps && (host?.Osc?.Monitor.Avatar.Length ?? 0) == 0)
                throw new InvalidOperationException("Zuerst den Avatar über OSCQuery erkennen lassen.");
            await service.ConfigureUniversalAsync(featureRows.Where(r => r.Selected.IsChecked == true).Select(r => new FeatureRoute(r.Feature, r.Limit.Value)), mode, selected);
        });
        AddButton("Gemeinsame Ausgabe freigeben", service => service.ArmUniversalAsync());
        var stop = new Button { Content = "Alle ausgewählten Geräte stoppen" };
        stop.Click += async (_, _) => {
            if (host?.Haptics is not { } service) return;
            service.Universal.Invalidate("Stopp angefordert");
            try { await service.StopDevicesAsync(); }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Gerätestopp"); }
            RefreshUniversalHaptics();
        };
        buttons.Children.Add(stop);
        var local = new StackPanel();
        universalProfile = new ComboBox { ItemsSource = new[] { "Gemeinsam · alle gewählten Funktionen", "Max 2 · Vibration und Constrict", "Ridge · Vibration und Rotation" }, SelectedIndex = 0 };
        local.Children.Add(new TextBlock { Text = "Geräteprofil (filtert Funktionen; Geräte oben ausdrücklich auswählen)" });
        local.Children.Add(universalProfile);
        foreach (var (key, label) in new[] { ("Strength", "Gesamtstärke"), ("Vibration", "Vibration"), ("Pump", "Pump / Constrict"), ("Rotation", "Rotation"), ("Motion", "Bewegung / Oszillation") })
        {
            var row = new WrapPanel();
            var text = new TextBlock { Width = 220, Text = label + " · 0 %", VerticalAlignment = VerticalAlignment.Center };
            var slider = new Slider { Minimum = 0, Maximum = 1, Width = 260, Value = 0 };
            slider.ValueChanged += (_, _) => text.Text = label + $" · {slider.Value:P0}";
            row.Children.Add(text); row.Children.Add(slider); local.Children.Add(row); universalGains[key] = slider;
        }
        universalClockwise = new CheckBox { Content = "Rotation im Uhrzeigersinn (Gegenrichtung nur bei gemeldeter Unterstützung)", IsChecked = true };
        local.Children.Add(universalClockwise);
        var apply = new Button { Content = "Profil und Werte anwenden" };
        apply.Click += (_, _) => {
            if (host?.Haptics is not { } service || !service.Universal.Snapshot.Armed) return;
            var messages = universalGains.Select(kv => new OscValue("/avatar/parameters/HydroLink/" + kv.Key, kv.Value.Value)).ToList();
            messages.Add(new("/avatar/parameters/HydroLink/Mode", universalProfile.SelectedIndex switch { 1 => 1, 2 => 2, _ => 3 }));
            messages.Add(new("/avatar/parameters/HydroLink/Clockwise", universalClockwise.IsChecked == true ? 1 : 0));
            messages.Add(new("/avatar/parameters/HydroLink/Enabled", 1));
            service.Universal.Receive(messages);
        };
        local.Children.Add(apply);
        panel.Children.Add(new Expander { Header = "Werte am PC einstellen (Regler allein senden nichts)", Content = local, Foreground = Color("#E5EDF7") });
        universalStatus = new TextBlock { Text = "Ausgabe aus", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 10, 0, 14) };
        panel.Children.Add(universalStatus);
    }
    private void RefreshUniversalHaptics()
    {
        if (universalFeatures is null) return;
        var service = host?.Haptics;
        var features = service?.Backend.Features ?? [];
        var signature = string.Join("|", features.Select(f => f.ToString() + ":" + f.DeviceId + ":" + f.Minimum + ":" + f.Maximum));
        if (signature != featureSignature || !ReferenceEquals(service, universalService))
        {
            featureSignature = signature; universalService = service; featureRows.Clear(); universalFeatures.Children.Clear();
            foreach (var feature in features)
            {
                var box = new CheckBox { Content = feature.ToString(), IsEnabled = feature.Supported, IsChecked = false, MinWidth = 460 };
                var limit = new Slider { Minimum = 0, Maximum = 1, Value = .25, Width = 180, IsEnabled = feature.Supported };
                var percent = new TextBlock { Text = "Grenze 25 %", Width = 100, Margin = new Thickness(10, 0, 0, 0) };
                limit.ValueChanged += (_, _) => percent.Text = $"Grenze {limit.Value:P0}";
                var row = new WrapPanel(); row.Children.Add(box); row.Children.Add(limit); row.Children.Add(percent);
                universalFeatures.Children.Add(row); featureRows.Add((feature, box, limit));
            }
            if (features.Count == 0) universalFeatures.Children.Add(new TextBlock { Text = "Noch keine Gerätefunktionen. Intiface verbinden und Gerätesuche starten." });
        }
        var monitor = host?.Osc?.Monitor;
        var sources = SpsSource.Discover((monitor?.Snapshot() ?? []).Where(p => p.QueryValue != "—" && p.Type == "f" && p.Access.Contains("Lesen")).Select(p => p.Address));
        var sourceKey = (monitor?.Avatar ?? "") + "|" + string.Join("|", sources.Select(s => s.Prefix));
        if (sourceKey != sourceSignature) { universalSources!.ItemsSource = sources; sourceSignature = sourceKey; }
        foreach (var button in universalButtons) button.IsEnabled = !universalBusy && service?.Backend.Connected == true;
        var snapshot = service?.Universal.Snapshot;
        universalStatus!.Text = snapshot is null ? "Gemeinsame Ausgabe aus" :
            $"{snapshot.Status} · {snapshot.SelectedFeatures} Funktionen übernommen\nAvatar-Freigabe: {(snapshot.AvatarEnabled ? "an" : "aus")} · Gesamtstärke {snapshot.Strength:P0} · SPS {snapshot.SourceLevel:P0} · bestätigte Ausgänge {snapshot.Sent.Count}";
    }
}
