using System.Collections.Concurrent;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using HydroLink.Core;
using HydroLink.Sdk;

namespace HydroLink.App;

public partial class MainWindow : Window
{
    public string[] MovementSources { get; } = ["None", "VRTI", "FitOSC"];
    public string[] HeartSources { get; } = ["None", "VRCOSC", "Pulsoid", "HypeRate"];
    private Settings settings;
    private HydroHost? host;
    private IDisposable? subscription;
    private readonly ConcurrentDictionary<string, DataEvent> latest = new();
    private readonly ConcurrentQueue<string> logs = new();
    private readonly DispatcherTimer timer;
    private long oscMessages;
    private bool closed;
    public MainWindow()
    {
        InitializeComponent(); DataContext = this;
        try { settings = Settings.Load(warning: Log); } catch (Exception ex) { settings = new(); Log("Einstellungen konnten nicht geladen werden: " + ex.Message); }
        Fitness.IsChecked = settings.Fitness; Wind.IsChecked = settings.WindForce; Osc.IsChecked = settings.OscHub; Haptics.IsChecked = settings.Haptics;
        WindMovementSource.SelectedItem = settings.WindMovementSource; WindHeartSource.SelectedItem = settings.WindHeartSource;
        MovementSource.SelectedItem = settings.MovementSource; HeartSource.SelectedItem = settings.HeartSource;
        VrtiUrl.Text = settings.VrtiUrl; FitUrl.Text = settings.FitOscUrl; HeartUrl.Text = settings.HeartUrl;
        PulsoidToken.Password = settings.PulsoidToken; HypeId.Text = settings.HypeRateId; HypeKey.Password = settings.HypeRateKey;
        WindHost.Text = settings.WindHost; WindPort.Text = settings.WindPort.ToString(); WindAddress.Text = settings.WindOscAddress;
        IntifaceUrl.Text = settings.IntifaceUrl; Companion.IsChecked = settings.Companion; CompanionUrl.Text = settings.CompanionUrl; CompanionCode.Password = settings.CompanionCode;
        ManualOsc.IsChecked = settings.ManualOsc; ManualHost.Text = settings.ManualOscHost; ManualPort.Text = settings.ManualOscPort.ToString();
        SendHeart.IsChecked = settings.SendHeartToOsc; HeartAddress.Text = settings.HeartOscAddress; PluginIds.Text = string.Join(", ", settings.EnabledPlugins);
        programLauncher = new ProgramLauncher(Log);
        BuildToolPages(); BuildHistoryPage(); OrganizeSettings(); UiReady = true; BuildDashboard();
        timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(250) }; timer.Tick += (_, _) => Refresh(); timer.Start();
        Closing += OnClosing;
        var commandArgs = Environment.GetCommandLineArgs();
        var renderIndex = Array.IndexOf(commandArgs, "--render-preview");
        if (renderIndex < 0) Loaded += async (_, _) => { await CheckStartupUpdate(); if (!closing && !closed && settings.StartModulesWithApp) Start_Click(this, new RoutedEventArgs()); };
        if (renderIndex >= 0 && commandArgs.Length > renderIndex + 1)
            Loaded += (_, _) => Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
            {
                var pageIndex = Array.IndexOf(commandArgs, "--preview-page");
                if (pageIndex >= 0 && commandArgs.Length > pageIndex + 1)
                    SelectPage(commandArgs[pageIndex + 1]);
                if (commandArgs.Contains("--preview-edit")) EditDashboard.IsChecked = true;
                if (commandArgs.Contains("--preview-osc-fixture")) PreviewOscFixture();
                if (commandArgs.Contains("--preview-ridge") && toyTabs != null) toyTabs.SelectedIndex = 1;
                if (commandArgs.Contains("--preview-solace") && toyTabs != null) toyTabs.SelectedIndex = 2;
                var toyIndex = Array.IndexOf(commandArgs, "--preview-toy"); if (toyIndex >= 0 && toyIndex + 1 < commandArgs.Length && Enum.TryParse<VrcHapticsBridge.Core.ToyKind>(commandArgs[toyIndex + 1], out var previewToy) && toyTabs != null) toyTabs.SelectedIndex = (int)previewToy;
                if (commandArgs.Contains("--verify-expanders")) VerifyExpandableSections();
                UpdateLayout();
                if (commandArgs.Contains("--preview-scroll-end") && Tabs.SelectedItem is TabItem selectedPage && selectedPage.Content is ScrollViewer scroller) { scroller.ScrollToEnd(); UpdateLayout(); }
                var bitmap = new RenderTargetBitmap((int)ActualWidth, (int)ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(this);
                var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                using (var file = File.Create(commandArgs[renderIndex + 1])) encoder.Save(file);
                if (commandArgs.Contains("--preview-dropdown"))
                {
                    SelectPage("Verbindungen"); UpdateLayout();
                    MovementSource.IsDropDownOpen = true;
                    _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() =>
                    {
                        var popup = (System.Windows.Controls.Primitives.Popup)MovementSource.Template.FindName("PART_Popup", MovementSource);
                        RenderElement((FrameworkElement)popup.Child, commandArgs[renderIndex + 1] + ".dropdown.png");
                        MovementSource.IsDropDownOpen = false; Close();
                    }));
                    return;
                }
                if (commandArgs.Contains("--preview-context"))
                {
                    var card = (Border)DashboardCards.Children[0]; var menu = card.ContextMenu!; menu.PlacementTarget = card; menu.IsOpen = true;
                    _ = Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(() => { RenderElement(menu, commandArgs[renderIndex + 1] + ".menu.png"); menu.IsOpen = false; Close(); }));
                    return;
                }
                Close();
            }));
    }
    private static void RenderElement(FrameworkElement element, string path)
    {
        element.UpdateLayout();
        var bitmap = new RenderTargetBitmap(Math.Max(1, (int)Math.Ceiling(element.ActualWidth)), Math.Max(1, (int)Math.Ceiling(element.ActualHeight)), 96, 96, PixelFormats.Pbgra32); bitmap.Render(element);
        var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap)); using var file = File.Create(path); encoder.Save(file);
    }
    private void Log(string message) { logs.Enqueue($"{DateTime.Now:HH:mm:ss}  {message}"); while (logs.Count > 300) logs.TryDequeue(out _); }
    private Settings ReadSettings()
    {
        static int Port(string text) => int.TryParse(text, out var p) && p is >= 1 and <= 65535 ? p : throw new InvalidOperationException("Port muss zwischen 1 und 65535 liegen.");
        static string Url(string text, params string[] schemes) => Uri.TryCreate(text.Trim(), UriKind.Absolute, out var u) && schemes.Contains(u.Scheme) ? u.ToString() : throw new InvalidOperationException("Ungültige Verbindungsadresse.");
        if (!WindAddress.Text.StartsWith('/') || !HeartAddress.Text.StartsWith('/')) throw new InvalidOperationException("OSC-Adressen müssen mit / beginnen.");
        return new Settings
        {
            Fitness = Fitness.IsChecked == true, WindForce = Wind.IsChecked == true, OscHub = Osc.IsChecked == true, Haptics = Haptics.IsChecked == true,
            MovementSource = (string?)MovementSource.SelectedItem ?? "None", HeartSource = (string?)HeartSource.SelectedItem ?? "None",
            VrtiUrl = Url(VrtiUrl.Text, "ws", "wss"), FitOscUrl = Url(FitUrl.Text, "ws", "wss"), HeartUrl = Url(HeartUrl.Text, "ws", "wss"),
            PulsoidToken = PulsoidToken.Password, HypeRateId = HypeId.Text.Trim(), HypeRateKey = HypeKey.Password,
            WindHost = WindHost.Text.Trim(), WindPort = Port(WindPort.Text), WindOscAddress = WindAddress.Text.Trim(),
            IntifaceUrl = Url(IntifaceUrl.Text, "ws", "wss"), Companion = Companion.IsChecked == true, CompanionUrl = Url(CompanionUrl.Text, "http", "https"), CompanionCode = CompanionCode.Password,
            ManualOsc = ManualOsc.IsChecked == true, ManualOscHost = ManualHost.Text.Trim(), ManualOscPort = Port(ManualPort.Text),
            SendHeartToOsc = SendHeart.IsChecked == true, HeartOscAddress = HeartAddress.Text.Trim(),
            HideInactiveModules = settings.HideInactiveModules, Dashboard = settings.Dashboard, Programs = settings.Programs, StartModulesWithApp = startWithApp?.IsChecked == true,
            WindRules = settings.WindRules, WindRiseMs = settings.WindRiseMs, WindMultiPwm = settings.WindMultiPwm, WindHeartMode = settings.WindHeartMode,
            FitnessOptions = settings.FitnessOptions, HomeAssistant = ReadHomeAssistant(),
            HapticsOptions = settings.HapticsOptions, OscRoutes = settings.OscRoutes,
            WindMovementSource = (string?)WindMovementSource.SelectedItem ?? "None", WindHeartSource = (string?)WindHeartSource.SelectedItem ?? "None",
            EnabledPlugins = PluginIds.Text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList()
        };
    }
    private void Save_Click(object sender, RoutedEventArgs e)
    {
        try { settings = ReadSettings(); settings.Save(); Log("Einstellungen gespeichert"); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Einstellungen prüfen"); }
    }
    private async void Start_Click(object sender, RoutedEventArgs e)
    {
        StartButton.IsEnabled = false;
        try
        {
            await EnsureHost();
            foreach (var (id, enabled) in new[] { ("fitness", Fitness.IsChecked == true), ("windforce", Wind.IsChecked == true), ("osc", Osc.IsChecked == true), ("haptics", Haptics.IsChecked == true) })
                if (enabled)
                    try { await host!.SetModuleAsync(id, true); if (id == "windforce") applyWindControls?.Invoke(); } catch (Exception ex) { Log(id + ": " + ex.Message); }
            RefreshToolPages();
        }
        catch (Exception ex) { Log("Start fehlgeschlagen: " + ex.Message); }
        finally { StartButton.IsEnabled = true; }
    }
    private async void Stop_Click(object sender, RoutedEventArgs e) => await Stop();
    private async Task Stop()
    {
        StopButton.IsEnabled = false;
        try { await StopRecording(); } catch (Exception ex) { Log("Session speichern: " + ex.Message); }
        if (host is not null) await host.DisposeAsync(); host = null; subscription?.Dispose(); subscription = null;
        foreach (var tab in Tabs.Items.OfType<TabItem>().Where(t => Equals(t.Tag, "plugin")).ToArray()) Tabs.Items.Remove(tab);
        latest.Clear();
        StartButton.IsEnabled = true; SettingsPanel.IsEnabled = OscSettings.IsEnabled = true;
        Fitness.IsEnabled = Wind.IsEnabled = Osc.IsEnabled = Haptics.IsEnabled = true;
        foreach (var editor in launchEditors) editor.IsEnabled = true;
        Log("Alle Module gestoppt"); Refresh();
    }
    private void Refresh()
    {
        if (closing || closed) return;
        string Metric(string topic, string unit) => latest.TryGetValue(topic, out var e) && DateTimeOffset.UtcNow - e.Timestamp < TimeSpan.FromSeconds(10) ? $"{e.Value.GetDouble():0.#} {unit}" : $"— {unit}";
        HeartValue.Text = Metric("fitness/heart/bpm", "bpm"); SpeedValue.Text = Metric("fitness/speed-kmh", "km/h"); WindValue.Text = Metric("windforce/output-percent", "%");
        OscCount.Text = Interlocked.Read(ref oscMessages).ToString();
        DeviceCount.Text = latest.TryGetValue("haptics/devices", out var devices) && DateTimeOffset.UtcNow - devices.Timestamp < TimeSpan.FromSeconds(5) ? devices.Value.GetArrayLength().ToString() : "—";
        Connection.Text = host is null ? "Gestoppt · keine Quellen aktiv" : host.Osc is { } osc ? $"VRChat: {osc.Target}   |   Empfang UDP {osc.ReceivePort}   |   OSCQuery HTTP {osc.QueryPort}" : "Host aktiv · OSC nicht angefordert";
        ActiveSources.Text = host is null ? "" : "Angeforderte Quellen: " + string.Join(" · ", host.Running);
        foreach (var card in settings.Dashboard.Where(c => c.Kind == "Verlauf"))
            if (latest.TryGetValue(card.Topic, out var datum) && datum.Value.ValueKind == System.Text.Json.JsonValueKind.Number && DateTimeOffset.UtcNow - datum.Timestamp < TimeSpan.FromSeconds(10))
            {
                if (!chartHistory.TryGetValue(card.Topic, out var history)) chartHistory[card.Topic] = history = new();
                history.Enqueue(datum.Value.GetDouble()); while (history.Count > 240) history.Dequeue();
            }
        RefreshDashboard(); RefreshToolPages();
        RefreshOscExplorer(); RefreshHaptics();
        _ = AutoSessionTick();
        if (!logs.IsEmpty) { while (logs.TryDequeue(out var line)) LogBox.AppendText(line + Environment.NewLine); if (LogBox.Text.Length > 24000) LogBox.Text = LogBox.Text[^16000..]; LogBox.ScrollToEnd(); }
    }
    private async void OnClosing(object? sender, CancelEventArgs e)
    {
        if (closed) return;
        e.Cancel = true;
        if (closing) return;
        closing = true; IsEnabled = false;
        try {
            await Stop();
            if (updateOnExit != null) System.Diagnostics.Process.Start(updateOnExit);
            timer.Stop(); closed = true;
        } catch (Exception ex) {
            updateOnExit = null; closing = false; IsEnabled = true;
            MessageBox.Show(this, "Beenden oder Update konnte nicht abgeschlossen werden: " + ex.Message, "HydroLink");
            return;
        }
        _ = Dispatcher.BeginInvoke(new Action(Close));
    }
}
