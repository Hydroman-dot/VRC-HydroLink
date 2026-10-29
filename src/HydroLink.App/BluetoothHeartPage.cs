using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using HydroLink.Core;
namespace HydroLink.App;
public partial class MainWindow
{
    private ComboBox heartSensor = null!;
    private CheckBox heartCompatibility = null!, heartSmooth = null!, heartAverageSmooth = null!, heartPulse = null!;
    private TextBlock bluetoothStatus = null!;
    private readonly Dictionary<string, TextBox> heartFields = [];
    private CancellationTokenSource? heartScan;
    private TabItem BuildBluetoothHeartPage()
    {
        var cfg = settings.Heartrate;
        var panel = Page("Bluetooth-Puls", "Coospo H9Z direkt mit HydroLink verbinden. VRCOSC-Pulsparameter für vorhandene Avatar-Prefabs.");
        panel.MaxWidth = 850; panel.HorizontalAlignment = HorizontalAlignment.Left;
        bluetoothStatus = new TextBlock { Text = "Vorbereitet · Hardwaretest mit H9Z steht aus", TextWrapping = TextWrapping.Wrap };
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
        controls.Children.Add(ActionButton("Bluetooth als Fitness-Pulsquelle auswählen", () => { HeartSource.SelectedItem = "Bluetooth"; bluetoothStatus.Text = "Quelle ausgewählt. Jetzt speichern und anschließend VR Fitness starten."; }));
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
        var mapping = new StackPanel();
        foreach (var name in HeartrateOptions.Names)
        {
            mapping.Children.Add(new TextBlock { Text = name });
            var field = new TextBox { Text = cfg.Address(name) }; heartFields[name] = field; mapping.Children.Add(field);
        }
        controls.Children.Add(new Expander { Header = "Avatar-Parameteradressen (VRCOSC-Standard)", Content = mapping, Margin = new Thickness(0,12,0,12) });
        panel.Children.Add(controls); launchEditors.Add(controls);
        panel.Children.Add(ActionButton("Einstellungen speichern", () => Save_Click(this, new RoutedEventArgs())));
        panel.Children.Add(new TextBlock { Text = "Konfiguration ändern: erst unter Module ‚Alles stoppen‘. Danach speichern und VR Fitness starten. Bei aktivierter VRCOSC-Ausgabe ersetzt dieses Profil die einzelne HeartRate-Ausgabe unter OSCQuery. Beat wird aus dem Puls berechnet und ist kein zeitgenaues EKG-Signal. Bei fehlendem Signal werden Connected/Enabled und die Werte zurückgesetzt.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0,12,0,0) });
        Closing += (_, _) => heartScan?.Cancel();
        return new TabItem { Header = "Bluetooth-Puls", Content = new ScrollViewer { Content = panel, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } };
    }
    private HeartrateOptions ReadHeartrate()
    {
        int Number(string key) => int.TryParse(heartFields[key].Text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var number) ? number : throw new ArgumentException("Puls-Einstellung muss eine ganze Zahl sein: " + key);
        var device = heartSensor.SelectedItem as HeartSensor;
        var cfg = new HeartrateOptions { BluetoothAddress = device?.Address ?? 0, DeviceName = device?.Name ?? "Coospo H9Z", VrcOscParameters = heartCompatibility.IsChecked == true,
            SmoothValue = heartSmooth.IsChecked == true, SmoothAverage = heartAverageSmooth.IsChecked == true, PulseBeat = heartPulse.IsChecked == true,
            ValueSmoothingMs = Number("valueMs"), AverageSmoothingMs = Number("averageMs"), AveragePeriodMs = Number("period"), LowerBpm = Number("lower"), UpperBpm = Number("upper"),
            Parameters = HeartrateOptions.Names.ToDictionary(name => name, name => heartFields[name].Text.Trim()) };
        cfg.Validate();
        if ((Equals(HeartSource.SelectedItem, "Bluetooth") || Equals(WindHeartSource.SelectedItem, "Bluetooth")) && cfg.BluetoothAddress == 0) throw new ArgumentException("Bitte zuerst einen Bluetooth-Pulssensor suchen und auswählen.");
        return cfg;
    }
    private void RefreshBluetoothHeart()
    {
        if (bluetoothStatus != null && host?.Running.Contains("Bluetooth") == true && latest.TryGetValue("bluetooth/status", out var status)) bluetoothStatus.Text = status.Value.ToString();
    }
}
