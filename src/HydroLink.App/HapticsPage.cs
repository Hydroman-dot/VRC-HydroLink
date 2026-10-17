using System.Windows;
using System.Windows.Controls;
using HydroLink.Core;
using VrcHapticsBridge.Core;
namespace HydroLink.App;
public partial class MainWindow
{
    private ComboBox? hapticDevices;
    private TextBlock? hapticState;
    private string deviceSignature = "";
    private readonly List<Button> hapticActions = [];
    private bool hapticUiBusy;
    private void BuildHapticsControls(StackPanel panel)
    {
        BuildToyHaptics(panel);
        var advanced = new StackPanel();
        panel.Children.Add(new Expander { Header = "Erweitert · manuelle Zuordnung und SPS-Plugs", Content = advanced, Foreground = Color("#E5EDF7"), Margin = new Thickness(0, 20, 0, 0) });
        BuildUniversalHaptics(advanced);
        var legacy = new StackPanel();
        panel.Children.Add(new Expander { Header = "Bisherige Max2-Einzelgeräte-Steuerung", Content = legacy, Foreground = Color("#E5EDF7") });
        panel = legacy;
        var config = new StackPanel();
        var vibrate = new TextBox { Text = settings.HapticsOptions.VibrateAddress }; var pump = new TextBox { Text = settings.HapticsOptions.PumpAddress }; var timeout = new TextBox { Text = settings.HapticsOptions.IdleTimeoutSeconds.ToString() };
        foreach (var (label, input) in new[] { ("Vibration · OSC-Adresse", vibrate), ("Pump · OSC-Adresse", pump), ("Inaktivitätslimit Sekunden (0 = aus; VRChat sendet bei Änderungen)", timeout) }) { config.Children.Add(new TextBlock { Text = label }); config.Children.Add(input); }
        config.Children.Add(ActionButton("Haptics-Einstellungen speichern", () =>
        {
            try
            {
                string Address(string text) => text.StartsWith('/') ? text.Trim() : "/avatar/parameters/" + text.Trim();
                var options = settings.HapticsOptions with { VibrateAddress = Address(vibrate.Text), PumpAddress = Address(pump.Text), IdleTimeoutSeconds = int.Parse(timeout.Text) }; options.Validate();
                settings.HapticsOptions = options; settings.Save(); Log("Haptics-Adressen gespeichert");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Haptics-Einstellungen"); }
        }));
        launchEditors.Add(config); panel.Children.Add(new Expander { Header = "Kanal-Einstellungen", Content = config, Foreground = Color("#E5EDF7") });
        hapticDevices = new ComboBox { MinWidth = 350, HorizontalAlignment = HorizontalAlignment.Left }; panel.Children.Add(hapticDevices);
        hapticState = new TextBlock { Margin = new Thickness(0, 10, 0, 10), FontSize = 17 }; panel.Children.Add(hapticState);
        var actions = new WrapPanel(); panel.Children.Add(actions); Panel buttonDestination = actions;
        void Button(string title, Func<HapticsService, Task> action, object? tag = null)
        {
            var b = new Button { Content = title, Tag = tag, IsEnabled = false };
            b.Click += async (_, _) => { if (hapticUiBusy) return; hapticUiBusy = true; RefreshHaptics(); try { if (host?.Haptics is { } service) await action(service); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Haptics"); } finally { hapticUiBusy = false; RefreshHaptics(); } };
            buttonDestination.Children.Add(b); hapticActions.Add(b);
        }
        Button("Geräte suchen", s => s.Backend.ScanAsync(default));
        Button("Ausgewähltes Gerät übernehmen", async s => { if (hapticDevices.SelectedItem is DeviceInfo device) await s.SelectAsync(device.Id); });
        Button("OSC-Ausgabe aktivieren", s => s.ArmLegacyAsync());
        Button("Ausgabe stoppen", s => s.StopDevicesAsync());
        var tests = new WrapPanel(); panel.Children.Add(tests);
        foreach (var channel in new[] { Channel.Vibrate, Channel.Pump })
        {
            var channelPanel = new StackPanel { Width = 420, Margin = new Thickness(0, 12, 20, 12) }; tests.Children.Add(channelPanel); buttonDestination = channelPanel;
            var value = new Slider { Minimum = 0, Maximum = 1, Value = .1, Width = 330, HorizontalAlignment = HorizontalAlignment.Left };
            var label = new TextBlock { Text = $"{channel} · 10 %" }; value.ValueChanged += (_, _) => label.Text = $"{channel} · {value.Value:P0}";
            channelPanel.Children.Add(label); channelPanel.Children.Add(value); Button(channel + " · 3 Sekunden testen", s => s.TestLegacyAsync(channel, value.Value), channel);
        }
        panel.Children.Add(new TextBlock { Text = "Regler allein senden nichts. Pump nutzt Constrict, wenn das ausgewählte Gerät diese Fähigkeit anbietet. Avatar-Menü und Overrides liefern weiterhin MaxVibrate / MaxPump.", Foreground = Color("#93ADC5") });
        RefreshHaptics();
    }
    private void RefreshHaptics()
    {
        RefreshToyHaptics();
        RefreshUniversalHaptics();
        if (hapticDevices is null) return;
        var service = host?.Haptics; var devices = service?.Backend.Devices ?? [];
        var signature = string.Join("|", devices.Select(d => $"{d.Id}:{d.Name}:{d.Vibrate}:{d.Pump}"));
        if (signature != deviceSignature) { var selected = (hapticDevices.SelectedItem as DeviceInfo)?.Id; hapticDevices.ItemsSource = devices; hapticDevices.SelectedItem = devices.FirstOrDefault(d => d.Id == selected); deviceSignature = signature; }
        var device = service?.Backend.Selected; var snapshot = service?.Controller.Snapshot;
        foreach (var button in hapticActions) button.IsEnabled = !hapticUiBusy && service?.Backend.Connected == true && (button.Tag is not Channel channel || (channel == Channel.Vibrate ? device?.Vibrate : device?.Pump) == true);
        string Value(double? n) => n?.ToString("0.000") ?? "—";
        hapticState!.Text = service?.Backend.Connected != true ? "Intiface nicht verbunden · Ausgabe aus" : $"Gerät: {device?.Name ?? "bitte auswählen"} · Modus: {snapshot?.Mode}\nVibration: Eingang {Value(snapshot?.VibrateRaw)} / gesendet {Value(snapshot?.VibrateSent)}\nPump: Eingang {Value(snapshot?.PumpRaw)} / gesendet {Value(snapshot?.PumpSent)} · {(device?.Pump == true ? "Constrict verfügbar" : "Constrict nicht angeboten")}";
    }
}
