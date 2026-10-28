using System.Diagnostics;
using System.Windows;
using System.Windows.Controls;
using HydroLink.Core;
using Microsoft.Win32;
namespace HydroLink.App;
public partial class MainWindow
{
    private bool autoSessionBusy, steamVrSeen, waitForRest;
    private DateTimeOffset? movingSince, restingSince;
    private DateTimeOffset nextSteamCheck;
    private FrameworkElement BuildFitnessOptions()
    {
        var options = settings.FitnessOptions;
        var panel = new StackPanel();
        var expander = new Expander { Header = "Profil, automatische Sessions, Ziele und Health-Synchronisierung", Foreground = Color("#E4EDF7"), Margin = new Thickness(0, 10, 0, 14) };
        expander.IsExpanded = true;
        var folder = new TextBox { Text = options.DataDirectory, IsReadOnly = true };
        panel.Children.Add(new TextBlock { Text = "Session-Datenordner" }); panel.Children.Add(folder);
        panel.Children.Add(ActionButton("Datenordner auswählen", () => { var dialog = new OpenFolderDialog(); if (dialog.ShowDialog(this) == true) { options.DataDirectory = dialog.FolderName; folder.Text = dialog.FolderName; } }));
        var fields = new WrapPanel();
        foreach (var (label, get, set, min, max) in new (string, Func<double>, Action<double>, double, double)[]
        {
            ("Gewicht kg", () => options.WeightKg, v => options.WeightKg = v, 0, 300),
            ("Alter", () => options.Age, v => options.Age = (int)v, 0, 100),
            ("Max Pulssprung", () => options.MaxHeartJump, v => options.MaxHeartJump = v, 1, 300),
            ("Max km/h", () => options.MaxSpeedKmh, v => options.MaxSpeedKmh = v, .1, 100),
            ("Signalalter s", () => options.StaleSeconds, v => options.StaleSeconds = v, 1, 120),
            ("Autostart nach s", () => options.AutoStartSeconds, v => options.AutoStartSeconds = v, 1, 120),
            ("Autostop nach s", () => options.AutoStopSeconds, v => options.AutoStopSeconds = v, 5, 3600),
            ("Wochenziel km", () => options.WeeklyKm, v => options.WeeklyKm = v, 0, 10000),
            ("Wochenziel h", () => options.WeeklyHours, v => options.WeeklyHours = v, 0, 168),
            ("Wochenziel Schritte", () => options.WeeklySteps, v => options.WeeklySteps = (int)v, 0, 10000000)
        })
        {
            var group = new StackPanel { Width = 160, Margin = new Thickness(0, 0, 10, 0) }; group.Children.Add(new TextBlock { Text = label });
            var input = new TextBox { Text = get().ToString("0.##") };
            input.LostKeyboardFocus += (_, _) => { if (double.TryParse(input.Text, out var value) && double.IsFinite(value)) set(Math.Clamp(value, min, max)); input.Text = get().ToString("0.##"); }; group.Children.Add(input); fields.Children.Add(group);
        }
        panel.Children.Add(fields);
        var sex = new ComboBox { ItemsSource = new[] { "unspecified", "male", "female" }, SelectedItem = options.Sex, Width = 220, HorizontalAlignment = HorizontalAlignment.Left }; sex.SelectionChanged += (_, _) => options.Sex = (string)sex.SelectedItem;
        panel.Children.Add(new TextBlock { Text = "Formelprofil für die Kalorienschätzung" }); panel.Children.Add(sex);
        panel.Children.Add(new TextBlock { Text = "Keytel-Schätzung wie im Original. Ohne gültiges Alter, Gewicht und Formelprofil wird kein Kalorienwert ausgegeben.", Foreground = Color("#93ADC5") });
        foreach (var (label, initial, set) in new (string, bool, Action<bool>)[]
        {
            ("Sessions bei Bewegung automatisch starten und nach Ruhe stoppen", options.AutoSessions, v => options.AutoSessions = v),
            ("Session beim Beenden von SteamVR stoppen", options.StopWithSteamVr, v => options.StopWithSteamVr = v),
            ("Schritte an Health Companion schreiben", options.SendHealthSteps, v => options.SendHealthSteps = v),
            ("Geschätzte Kalorien an Health Companion schreiben", options.SendHealthCalories, v => options.SendHealthCalories = v)
        })
        {
            var check = new CheckBox { Content = label, IsChecked = initial }; check.Checked += (_, _) => set(true); check.Unchecked += (_, _) => set(false); panel.Children.Add(check);
        }
        panel.Children.Add(new TextBlock { Text = "Bei aktiviertem Companion werden beendete Sessions lokal vorgemerkt. Eine Datei verlässt die Warteschlange erst nach bestätigtem Empfang. Bei Verbindungsproblemen folgt ein neuer Versuch.", Foreground = Color("#93ADC5") });
        panel.Children.Add(ActionButton("Fitness-Einstellungen speichern", () => Save_Click(this, new RoutedEventArgs())));
        // Keep the header interactive while protecting settings used by running sessions.
        var content = new StackPanel();
        var hint = new TextBlock { Foreground = Color("#B5C8DB"), Margin = new Thickness(0, 8, 0, 12), Visibility = Visibility.Collapsed,
            Text = "Einstellungen sind während laufender Tools gesperrt. Zum Bearbeiten unter Module → Alles stoppen." };
        panel.IsEnabledChanged += (_, _) => hint.Visibility = panel.IsEnabled ? Visibility.Collapsed : Visibility.Visible;
        content.Children.Add(hint); content.Children.Add(panel); expander.Content = content;
        launchEditors.Add(panel); return expander;
    }
    private async Task AutoSessionTick()
    {
        if (autoSessionBusy || host?.ActiveModules.Contains("fitness") != true) return;
        autoSessionBusy = true;
        try
        {
            var now = DateTimeOffset.UtcNow; var options = settings.FitnessOptions;
            if (options.StopWithSteamVr && now >= nextSteamCheck)
            {
                nextSteamCheck = now.AddSeconds(5);
                var processes = Process.GetProcessesByName("vrserver"); bool steamRunning = processes.Length > 0; foreach (var process in processes) process.Dispose();
                if (steamVrSeen && !steamRunning && fitnessSession?.Running == true) { await StopRecording(); Log("Session mit SteamVR beendet"); }
                steamVrSeen = steamRunning;
            }
            // Once SteamVR has stopped, do not immediately restart from a lingering speed sample.
            if (options.StopWithSteamVr && !steamVrSeen) { movingSince = null; restingSince = null; return; }
            if (!options.AutoSessions) return;
            bool moving = latest.TryGetValue("fitness/speed-kmh", out var speed) && now - speed.Timestamp < TimeSpan.FromSeconds(options.StaleSeconds) && speed.Value.ValueKind == System.Text.Json.JsonValueKind.Number && speed.Value.GetDouble() > .05;
            if (moving)
            {
                if (waitForRest) return;
                movingSince ??= now; restingSince = null;
                if (fitnessSession is null && now - movingSince >= TimeSpan.FromSeconds(options.AutoStartSeconds))
                {
                    var recording = new FitnessSession(host.Bus, options.DataDirectory, settings); recording.Start(sessionName?.Text ?? "VR Training"); fitnessSession = recording;
                    if (recordButton is not null) recordButton.Content = "Session stoppen und speichern"; Log("Automatische Session gestartet");
                }
            }
            else
            {
                waitForRest = false;
                movingSince = null; restingSince ??= now;
                if (fitnessSession?.Running == true && now - restingSince >= TimeSpan.FromSeconds(options.AutoStopSeconds)) { await StopRecording(); Log("Automatische Session nach Ruhe beendet"); }
            }
        }
        catch (Exception ex) { movingSince = null; Log("Auto-Session: " + ex.Message); }
        finally { autoSessionBusy = false; }
    }
}
