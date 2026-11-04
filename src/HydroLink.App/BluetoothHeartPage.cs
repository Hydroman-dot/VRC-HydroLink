using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using HydroLink.Core;
namespace HydroLink.App;
public partial class MainWindow
{
    private ComboBox heartSensor = null!;
    private CheckBox heartCompatibility = null!, heartSmooth = null!, heartAverageSmooth = null!, heartPulse = null!;
    private TextBlock bluetoothStatus = null!, bluetoothLiveStatus = null!, bluetoothMeasurement = null!;
    private readonly Dictionary<string, TextBox> heartFields = [];
    private CancellationTokenSource? heartScan;
    private TabItem BuildBluetoothHeartPage()
    {
        var cfg = settings.Heartrate;
        var panel = Page("Puls & Avatar", "Bluetooth, Pulsoid und weitere Pulsquellen: gemeinsame, anpassbare VRCOSC-Avatarparameter.");
        panel.MaxWidth = 850; panel.HorizontalAlignment = HorizontalAlignment.Left;
        bluetoothLiveStatus = new TextBlock { Text = "Bluetooth-Puls nicht gestartet", TextWrapping = TextWrapping.Wrap };
        bluetoothMeasurement = new TextBlock { Text = "Puls: — bpm", FontSize = 26, Margin = new Thickness(0, 8, 0, 8) };
        panel.Children.Add(bluetoothLiveStatus);
        panel.Children.Add(bluetoothMeasurement);
        bluetoothStatus = new TextBlock { Text = "Sensor auswählen oder nach Sensoren suchen.", TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(bluetoothStatus);
        var controls = new StackPanel();
        heartSensor = new ComboBox { MinWidth = 450 };
        if (cfg.BluetoothAddress != 0) { var saved = new HeartSensor(cfg.BluetoothAddress, cfg.DeviceName); heartSensor.Items.Add(saved); heartSensor.SelectedItem = saved; }
        var scan = new Button { Content = "Pulssensoren suchen (8 Sekunden)" };
        scan.Click += async (_, _) =>
        {
            scan.IsEnabled = false; heartScan?.Dispose(); heartScan = new();
            bluetoothStatus.Text = "Suche · H9Z tragen und andere Sensor-Verbindungen für den Test beenden.";
            try
            {
                var found = await BluetoothHeartSource.ScanAsync(heartScan.Token);
                var selected = heartSensor.SelectedItem as HeartSensor;
                heartSensor.Items.Clear(); foreach (var device in found) heartSensor.Items.Add(device);
                if (selected != null)
                {
                    var match = found.FirstOrDefault(d => d.Address == selected.Address);
                    if (match == null) { heartSensor.Items.Add(selected); match = selected; }
                    heartSensor.SelectedItem = match;
                }
                bluetoothStatus.Text = found.Length == 0 ? "Keinen Pulssensor gefunden. H9Z tragen, Bluetooth prüfen und erneut suchen." : "Bitte deinen Sensor auswählen; es wird kein fremdes Gerät automatisch verbunden.";
            }
            catch (OperationCanceledException) { }
            catch (Exception ex) { bluetoothStatus.Text = "Suche fehlgeschlagen: " + ex.Message; }
            finally { scan.IsEnabled = true; }
        };
        controls.Children.Add(scan); controls.Children.Add(heartSensor);
        controls.Children.Add(ActionButton("Bluetooth als Pulsquelle auswählen", () => { HeartSource.SelectedItem = "Bluetooth"; bluetoothStatus.Text = "Quelle ausgewählt. Jetzt speichern und anschließend Heartrate starten."; }));
        heartCompatibility = new CheckBox { Content = "Alle neun VRCOSC-Pulsparameter an den Avatar senden", IsChecked = cfg.VrcOscParameters };
        heartSmooth = new CheckBox { Content = "Pulswert glätten", IsChecked = cfg.SmoothValue };
        heartAverageSmooth = new CheckBox { Content = "Mittelwert glätten", IsChecked = cfg.SmoothAverage };
        heartPulse = new CheckBox { Content = "Beat als kurzen Impuls senden (sonst wechselnd an/aus)", IsChecked = cfg.PulseBeat };
        controls.Children.Add(heartCompatibility); controls.Children.Add(heartSmooth);
        void Field(string key, string label, string value)
        {
            controls.Children.Add(new TextBlock { Text = label, Margin = new Thickness(0,8,0,0) });
            var field = new TextBox { Text = value }; heartFields[key] = field; controls.Children.Add(field);
        }
        Field("valueMs", "Puls-Glättung · ms", cfg.ValueSmoothingMs.ToString()); controls.Children.Add(heartAverageSmooth);
        Field("averageMs", "Mittelwert-Glättung · ms", cfg.AverageSmoothingMs.ToString());
        Field("period", "Mittelwert-Zeitraum · ms", cfg.AveragePeriodMs.ToString());
        Field("lower", "Normalisierung: unterer Pulswert", cfg.LowerBpm.ToString());
        Field("upper", "Normalisierung: oberer Pulswert", cfg.UpperBpm.ToString()); controls.Children.Add(heartPulse);
        Field("pulsoidTimeout", "Pulsoid: letzte Messung höchstens halten · Sekunden (5–60)", cfg.PulsoidSignalTimeoutSeconds.ToString());
        controls.Children.Add(ActionButton("Pulsoid verwenden und VRCOSC-Ausgabe aktivieren", () => { HeartSource.SelectedItem = "Pulsoid"; heartCompatibility.IsChecked = true; bluetoothStatus.Text = "Pulsoid ausgewählt. Zugangstoken unter Verbindungen eintragen, speichern und Heartrate neu starten. Eigene Parameteradressen bleiben erhalten."; }));
        controls.Children.Add(ActionButton("VRCOSC-Standardadressen einsetzen", () => { foreach (var name in HeartrateOptions.Names) heartFields[name].Text = "/avatar/parameters/VRCOSC/Heartrate/" + name; }));
        var mapping = new StackPanel();
        foreach (var name in HeartrateOptions.Names)
        {
            mapping.Children.Add(new TextBlock { Text = name });
            var field = new TextBox { Text = cfg.Address(name) }; heartFields[name] = field; mapping.Children.Add(field);
        }
        controls.Children.Add(new Expander { Header = "Avatar-Parameteradressen (VRCOSC-Standard)", Content = mapping, Margin = new Thickness(0,12,0,12) });
        panel.Children.Add(controls); launchEditors.Add(controls);
        panel.Children.Add(ActionButton("Einstellungen speichern", () => Save_Click(this, new RoutedEventArgs())));
        panel.Children.Add(new TextBlock { Text = "Konfiguration ändern: erst unter Module ‚Alles stoppen‘. Danach speichern und Heartrate starten. VR Fitness und WindForce können den Sensor zusätzlich nutzen. Bei aktivierter VRCOSC-Ausgabe ersetzt dieses Profil die einzelne HeartRate-Ausgabe unter OSCQuery. Beat wird aus dem Puls berechnet und ist kein zeitgenaues EKG-Signal. Bei fehlendem Signal werden Connected/Enabled und die Werte zurückgesetzt.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,0) });
        Closing += (_, _) => heartScan?.Cancel();
        return new TabItem { Header = "Puls & Avatar", Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
    }
    private HeartrateOptions ReadHeartrate()
    {
        int Number(string key) => int.TryParse(heartFields[key].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : throw new ArgumentException("Puls-Einstellung muss eine ganze Zahl sein: " + key);
        var device = heartSensor.SelectedItem as HeartSensor;
        var cfg = new HeartrateOptions { BluetoothAddress = device?.Address ?? 0, DeviceName = device?.Name ?? "Coospo H9Z", VrcOscParameters = heartCompatibility.IsChecked == true,
            SmoothValue = heartSmooth.IsChecked == true, SmoothAverage = heartAverageSmooth.IsChecked == true, PulseBeat = heartPulse.IsChecked == true,
            ValueSmoothingMs = Number("valueMs"), AverageSmoothingMs = Number("averageMs"), AveragePeriodMs = Number("period"), LowerBpm = Number("lower"), UpperBpm = Number("upper"),
            PulsoidSignalTimeoutSeconds = Number("pulsoidTimeout"),
            Parameters = HeartrateOptions.Names.ToDictionary(name => name, name => heartFields[name].Text.Trim()) };
        cfg.Validate();
        if ((Equals(HeartSource.SelectedItem, "Bluetooth") || Equals(WindHeartSource.SelectedItem, "Bluetooth")) && cfg.BluetoothAddress == 0) throw new ArgumentException("Bitte zuerst einen Bluetooth-Pulssensor suchen und auswählen.");
        return cfg;
    }
    private void RefreshBluetoothHeart()
    {
        if (bluetoothLiveStatus == null) return;
        var running = host?.Running.Contains("Bluetooth") == true;
        bluetoothLiveStatus.Text = running
            ? latest.TryGetValue("bluetooth/status", out var status) ? status.Value.ToString() : "Bluetooth · Verbindung wird vorbereitet …"
            : "Bluetooth-Puls nicht gestartet · Pulsquelle Bluetooth auswählen, speichern und Heartrate starten.";
        var hasMeasurement = latest.TryGetValue("sources/Bluetooth/heart/bpm", out var measurement);
        var age = hasMeasurement ? DateTimeOffset.UtcNow - measurement!.Timestamp : TimeSpan.MaxValue;
        var connected = latest.TryGetValue("provider/Bluetooth/status", out var provider) && provider.Value.ToString() == "connected";
        var fresh = running && connected && age < TimeSpan.FromSeconds(5);
        bluetoothMeasurement.Text = fresh
            ? $"Puls: {measurement!.Value} bpm · vor {Math.Max(0, (int)age.TotalSeconds)} s"
            : hasMeasurement ? $"Puls: — bpm · letzte Messung {measurement!.Timestamp.ToLocalTime():HH:mm:ss} (nicht aktuell)" : "Puls: — bpm · noch keine Messung empfangen";
    }
}
