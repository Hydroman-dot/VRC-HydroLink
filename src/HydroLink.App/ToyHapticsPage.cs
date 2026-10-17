using System.Windows;
using System.Windows.Controls;
using Buttplug.Core.Messages;
using VrcHapticsBridge.Core;
using HydroLink.Core;

namespace HydroLink.App;
public partial class MainWindow
{
    private sealed class ToyView
    {
        public required ToyKind Kind;
        public required ComboBox Devices;
        public required Slider Vibration, Secondary;
        public required CheckBox Clockwise;
        public required CheckBox Manual;
        public required Button Activate;
        public required TextBlock Status, Channels;
        public string Signature = "unset";
        public string WizardRevision = "";
        public bool Updating, Busy;
    }
    private readonly List<ToyView> toyViews = [];
    private TextBlock? socketAutoStatus;
    private TabControl? toyTabs;
    private readonly WizardSyncReader wizardReader = new();
    private WizardSyncState wizardSync = new(false, null, "missing", "");
    private TextBlock? wizardSyncStatus;
    private DateTimeOffset wizardNextRead;
    private string wizardReadAvatar = "";
    private string wizardStartedRevision = "";
    private bool wizardStarting;
    private void BuildToyHaptics(StackPanel panel)
    {
        panel.Children.Add(new TextBlock { Text = "Toy aktivieren → alle SPS-Plugs und Sockets werden automatisch verbunden. Beide Toys können gleichzeitig laufen.", TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 12, 0, 0) });
        socketAutoStatus = new TextBlock { Text = "Warte auf VRChat · keine Quellenauswahl oder Übertragung nötig", Margin = new Thickness(0, 10, 0, 10), TextWrapping = TextWrapping.Wrap };
        panel.Children.Add(socketAutoStatus);
        wizardSyncStatus = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10), Foreground = Color("#93ADC5") };
        panel.Children.Add(wizardSyncStatus);
        toyTabs = new TabControl { Background = Color("#182638"), BorderBrush = Color("#294454"), Foreground = Color("#E5EDF7") }; panel.Children.Add(toyTabs);
        foreach (var kind in Enum.GetValues<ToyKind>())
        {
            string title = kind == ToyKind.Max2 ? "Max 2" : "Ridge";
            var content = new StackPanel { Margin = new Thickness(12) };
            var devices = new ComboBox { MinWidth = 300, HorizontalAlignment = HorizontalAlignment.Stretch };
            content.Children.Add(new TextBlock { Text = "Gerät · wird bei eindeutigem Namen automatisch ausgewählt" }); content.Children.Add(devices);
            var channels = new TextBlock { Margin = new Thickness(0, 8, 0, 8), TextWrapping = TextWrapping.Wrap }; content.Children.Add(channels);
            Slider Level(string caption)
            {
                var label = new TextBlock { Text = caption + " · 25 %" }; content.Children.Add(label);
                var slider = new Slider { Minimum = 0, Maximum = 1, Value = .25, Margin = new Thickness(0, 4, 0, 12) };
                slider.ValueChanged += (_, _) => label.Text = caption + $" · {slider.Value:P0}";
                content.Children.Add(slider); return slider;
            }
            var vibration = Level("Vibration maximal");
            var secondary = Level(kind == ToyKind.Max2 ? "Pump maximal" : "Rotation maximal");
            var clockwise = new CheckBox { Content = "Im Uhrzeigersinn", IsChecked = true, Visibility = kind == ToyKind.Ridge ? Visibility.Visible : Visibility.Collapsed };
            content.Children.Add(clockwise);
            var buttons = new WrapPanel { Margin = new Thickness(0, 4, 0, 8) }; content.Children.Insert(3, buttons);
            var activate = new Button { Content = title + " aktivieren" }; var stop = new Button { Content = title + " stoppen" };
            buttons.Children.Add(activate); buttons.Children.Add(stop);
            var status = new TextBlock { TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 0, 0, 10) }; content.Children.Insert(4, status);
            var manual = new CheckBox { Content = "Manuell ohne SPS", IsChecked = false, Margin = new Thickness(0, 4, 0, 10) };
            content.Children.Insert(5, manual);
            var view = new ToyView { Kind = kind, Devices = devices, Vibration = vibration, Secondary = secondary, Clockwise = clockwise, Manual = manual, Activate = activate, Status = status, Channels = channels };
            toyViews.Add(view);
            manual.Checked += (_, _) => { if (!view.Updating) host?.Haptics?.Toys[kind].SetSpsChannel("Manual", 1); };
            manual.Unchecked += (_, _) => { if (!view.Updating) host?.Haptics?.Toys[kind].SetSpsChannel("Manual", 0); };
            void Apply() { if (!view.Updating) host?.Haptics?.Toys[kind].SetActiveToyLevels(vibration.Value, secondary.Value, clockwise.IsChecked == true); }
            vibration.ValueChanged += (_, _) => Apply(); secondary.ValueChanged += (_, _) => Apply();
            clockwise.Checked += (_, _) => Apply(); clockwise.Unchecked += (_, _) => Apply();
            devices.SelectionChanged += async (_, _) => {
                if (view.Updating || host?.Haptics is not { } service) return;
                try { await service.StopToyAsync(kind); } catch (Exception ex) { status.Text = ex.Message; }
                RefreshToyHaptics();
            };
            activate.Click += async (_, _) => {
                if (view.Busy || host?.Haptics is not { } service || devices.SelectedItem is not ToyDevice device) return;
                view.Busy = true; RefreshToyHaptics();
                try { await service.ActivateToyAsync(kind, device.Id, vibration.Value, secondary.Value, clockwise.IsChecked == true); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, title); }
                finally { view.Busy = false; RefreshToyHaptics(); }
            };
            stop.Click += async (_, _) => {
                if (host?.Haptics is not { } service) return;
                try { await service.StopToyAsync(kind); } catch (Exception ex) { MessageBox.Show(this, ex.Message, title + " stoppen"); }
                RefreshToyHaptics();
            };
            content.Children.Add(new TextBlock { Text = "Erst im Radialmenü Haptik einschalten. SPS und Manuell haben getrennte Werte; hier gelten die Regler für den gewählten Modus. Nach Avatar- oder Verbindungswechsel erneut aktivieren.", TextWrapping = TextWrapping.Wrap, Foreground = Color("#93ADC5"), Margin = new Thickness(0, 12, 0, 0) });
            toyTabs.Items.Add(new TabItem { Header = title, Content = content });
        }
        RefreshToyHaptics();
    }
    private void RefreshToyHaptics()
    {
        if (socketAutoStatus == null) return;
        var avatar = host?.Osc?.Monitor.Avatar ?? "";
        if (DateTimeOffset.UtcNow >= wizardNextRead || avatar != wizardReadAvatar)
        {
            wizardNextRead = DateTimeOffset.UtcNow.AddSeconds(1);
            wizardReadAvatar = avatar;
            wizardSync = wizardReader.Read(avatar);
        }
        if (UiReady && wizardSync.Profile != null && wizardStartedRevision != wizardSync.Fingerprint && !wizardStarting
            && !Environment.GetCommandLineArgs().Contains("--render-preview")) _ = StartWizardDetection();
        var service = host?.Haptics;
        service?.ApplyWizardSync(wizardSync, avatar);
        var unityStatus = wizardSync.Profile is { } profile
            ? $"Unity-Einrichtung: übernommen · {profile.AvatarName} · {string.Join(" + ", profile.Toys)}"
            : wizardSync.Present ? "Unity-Einrichtung: " + wizardSync.Status
            : "Unity-Einrichtung: keine Übergabe vorhanden · manuelle Einrichtung verfügbar.";
        var avatarStatus = wizardSync.Profile is not { } expected ? string.IsNullOrEmpty(avatar) ? "VRChat-Avatar: noch nicht erkannt." : "VRChat-Avatar: erkannt · noch keine passende Einrichtung verfügbar."
            : expected.AvatarId.Length == 0 ? "VRChat-Avatar: Upload-ID fehlt. Nach dem ersten Upload im Wizard erneut übergeben."
            : string.IsNullOrEmpty(avatar) ? "VRChat-Avatar: noch nicht erkannt. VRChat starten und OSC einschalten."
            : expected.MatchesAvatar(avatar) ? "VRChat-Avatar: passt zur Unity-Einrichtung."
            : $"VRChat-Avatar: anderer Avatar erkannt. Bitte {expected.AvatarName} laden.";
        wizardSyncStatus!.Text = unityStatus + "\n" + avatarStatus;
        var sources = SpsSource.Discover((host?.Osc?.Monitor.Snapshot() ?? []).Select(p => p.Address));
        socketAutoStatus.Text = sources.Length > 0 ? $"{sources.Count(s => s.Kind == "Plug")} Plugs und {sources.Count(s => s.Kind == "Socket")} Sockets erkannt · stärkster Kontakt zählt" : "SPS-Automatik bereit · warte auf Plug- oder Socket-Signale aus VRChat";
        foreach (var view in toyViews)
        {
            if (view.WizardRevision != wizardSync.Fingerprint && wizardSync.Profile != null)
            {
                view.Updating = true; view.Vibration.Value = .25; view.Secondary.Value = .25; view.Clockwise.IsChecked = true;
                view.Updating = false; view.WizardRevision = wizardSync.Fingerprint;
            }
            var included = wizardSync.Profile?.Includes(view.Kind) ?? true;
            var tab = toyTabs!.Items.OfType<TabItem>().ElementAt((int)view.Kind);
            tab.Visibility = included ? Visibility.Visible : Visibility.Collapsed;
            if (!included && tab.IsSelected) toyTabs.SelectedItem = toyTabs.Items.OfType<TabItem>().FirstOrDefault(t => t != tab && t.Visibility == Visibility.Visible);
            var devices = service?.Toys.Devices(view.Kind) ?? [];
            var signature = wizardSync.Fingerprint + "|" + string.Join("|", devices.Select(d => $"{d.Id}:{d.Name}"));
            if (view.Signature != signature)
            {
                view.Updating = true;
                var old = view.Devices.SelectedItem as ToyDevice;
                view.Devices.ItemsSource = devices;
                var matches = devices.Where(d => ToyHaptics.Matches(view.Kind, d.Name)).ToArray();
                view.Devices.SelectedItem = (!wizardSync.Present ? devices.FirstOrDefault(d => d == old) : null) ?? (matches.Length == 1 ? matches[0] : null);
                view.Signature = signature; view.Updating = false;
            }
            var selected = view.Devices.SelectedItem as ToyDevice;
            var features = (service?.Backend.Features ?? []).Where(f => f.DeviceId == selected?.Id).ToArray();
            var vibration = features.Any(f => f.Supported && f.Type == OutputType.Vibrate);
            var secondary = features.Any(f => f.Supported && f.Type == (view.Kind == ToyKind.Max2 ? OutputType.Constrict : OutputType.Rotate));
            view.Channels.Text = selected == null ? "Gerätefunktionen: verfügbar, sobald ein Toy verbunden und ausgewählt ist."
                : $"Gerätefunktionen · Vibration: {(vibration ? "bereit" : "von Intiface nicht gemeldet")} · {(view.Kind == ToyKind.Max2 ? "Pump" : "Rotation")}: {(secondary ? "bereit" : "von Intiface nicht gemeldet")}";
            view.Vibration.IsEnabled = vibration; view.Secondary.IsEnabled = secondary;
            view.Clockwise.IsEnabled = features.Any(f => f.Reversible);
            if (!view.Clockwise.IsEnabled) view.Clockwise.IsChecked = true;
            var engine = service?.Toys[view.Kind]; var snapshot = engine?.Snapshot;
            view.Updating = true; view.Manual.IsChecked = engine?.ManualToy == true; view.Updating = false;
            view.Manual.IsEnabled = snapshot?.Armed == true && snapshot.AvatarEnabled;
            if (snapshot?.Armed == true)
            {
                var levels = engine!.ActiveToyLevels;
                view.Updating = true;
                view.Vibration.Value = levels.Vibration; view.Secondary.Value = levels.Secondary;
                view.Clockwise.IsChecked = levels.Clockwise;
                view.Updating = false;
            }
            view.Activate.IsEnabled = !view.Busy && service?.Backend.Connected == true && selected != null && snapshot?.Armed != true && service.WizardAllows(view.Kind);
            view.Devices.IsEnabled = !view.Busy;
            var connectionStatus = service == null ? "Gerät: Erkennung nicht gestartet. Tool starten."
                : !service.Backend.Connected ? "Gerät: warte auf Intiface-Verbindung."
                : selected == null ? "Gerät: Intiface verbunden, aber kein Toy zugeordnet. Toy verbinden oder bei mehreren Geräten auswählen."
                : $"Gerät: verbunden · {selected.Name}";
            var outputStatus = service != null && !service.WizardAllows(view.Kind)
                ? wizardSync.Profile == null ? "Ausgabe: gesperrt · Unity-Übergabe prüfen."
                : !wizardSync.Profile.Includes(view.Kind) ? "Ausgabe: Toy fehlt in der Unity-Einrichtung."
                : "Ausgabe: gesperrt · VRChat-Avatar-Abgleich steht noch aus (siehe oben)."
                : service?.Backend.Connected != true || selected == null ? "Ausgabe: aus · Geräteverbindung fehlt."
                : snapshot?.Armed == true ? !snapshot.AvatarEnabled ? "Ausgabe: freigegeben · Haptik im Radialmenü einschalten."
                : engine!.ManualToy ? "Ausgabe: manuell aktiv · ohne SPS-Kontakt."
                : $"Ausgabe: SPS aktiv · {engine.SourceCount} Quellen · Kontakt {snapshot.SourceLevel:P0}"
                : "Ausgabe: aus · zum Starten Toy aktivieren.";
            view.Status.Text = connectionStatus + "\n" + outputStatus;
        }
        if (toyTabs!.SelectedItem is not TabItem selectedTab || selectedTab.Visibility != Visibility.Visible)
            toyTabs.SelectedItem = toyTabs.Items.OfType<TabItem>().FirstOrDefault(t => t.Visibility == Visibility.Visible);
    }
    private async Task StartWizardDetection()
    {
        wizardStarting = true; wizardStartedRevision = wizardSync.Fingerprint;
        try
        {
            await EnsureHost(); await host!.SetModuleAsync("haptics", true);
            host.Haptics?.ApplyWizardSync(wizardSync, host.Osc?.Monitor.Avatar ?? "");
            Log("Unity-Einrichtung automatisch übernommen; Geräteerkennung gestartet, Ausgabe bleibt aus.");
        }
        catch (Exception ex) { Log("Unity-Übergabe: Erkennung konnte nicht starten: " + ex.Message); }
        finally { wizardStarting = false; }
    }
}
