using System.Globalization;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using HydroLink.Core;
namespace HydroLink.App;
public partial class MainWindow
{
    private void BuildWindConfiguration(StackPanel page)
    {
        var draft = WindConfiguration.From(settings);
        var panel = new StackPanel { Margin = new Thickness(0, 12, 0, 18) };
        page.Children.Insert(3, panel);
        panel.Children.Add(new TextBlock { Text = "Quellen und Prioritäten", FontSize = 21 });
        panel.Children.Add(new TextBlock { Text = "Oben = höhere Priorität. Häkchen aktiviert eine Regel. Änderungen mit Speichern und anwenden übernehmen; andere Tools laufen weiter.", Foreground = Color("#93ADC5"), Margin = new Thickness(0, 8, 0, 8) });
        var choices = new WrapPanel(); panel.Children.Add(choices);
        ComboBox Choice(string label, string[] values, string selected)
        {
            var group = new StackPanel { Width = 240, Margin = new Thickness(0, 0, 12, 0) }; group.Children.Add(new TextBlock { Text = label });
            var combo = new ComboBox { ItemsSource = values, SelectedItem = selected }; group.Children.Add(combo); choices.Children.Add(group); return combo;
        }
        var movement = Choice("Bewegungsdaten von", MovementSources, draft.Movement);
        var heart = Choice("Pulsdaten von", HeartSources, draft.Heart);
        var mode = Choice("Pulsmodus", ["OFF", "SOURCE", "MINIMUM"], draft.HeartMode);
        WindMovementSource.SelectionChanged += (_, _) => { if (WindMovementSource.SelectedItem is string value) movement.SelectedItem = value; };
        WindHeartSource.SelectionChanged += (_, _) => { if (WindHeartSource.SelectedItem is string value) heart.SelectedItem = value; };
        var multi = new CheckBox { Content = "FANS-Protokoll (drei PWM-Kanäle)", IsChecked = draft.MultiPwm }; panel.Children.Add(multi);
        panel.Children.Add(new TextBlock { Text = "Treadmill braucht VRTI oder FitOSC, AutoWalk VRTI, FitOSC Walking FitOSC und Heart Rate eine Pulsquelle. Direct OSC, PhysBone und VRChat Velocity verwenden OSCQuery. Adressen und Zugangsdaten unter Verbindungen.", Foreground = Color("#93ADC5") });
        var rows = new StackPanel(); panel.Children.Add(rows);
        var commits = new List<Action>();
        void BuildRows()
        {
            rows.Children.Clear(); commits.Clear();
            foreach (var rule in draft.Rules)
            {
                var container = new StackPanel { Margin = new Thickness(0, 6, 0, 0) }; var line = new WrapPanel(); container.Children.Add(line);
                var number = draft.Rules.IndexOf(rule) + 1;
                line.Children.Add(new TextBlock { Text = number + ".", Width = 30, VerticalAlignment = VerticalAlignment.Center });
                var enabled = new CheckBox { Content = rule.Source, Width = 180, IsChecked = rule.Enabled }; enabled.Checked += (_, _) => rule.Enabled = true; enabled.Unchecked += (_, _) => rule.Enabled = false; line.Children.Add(enabled);
                void Move(int offset)
                {
                    try { foreach (var commit in commits) commit(); }
                    catch (Exception ex) { MessageBox.Show(this, ex.Message, "Kalibrierung prüfen"); return; }
                    var index = draft.Rules.IndexOf(rule); var next = index + offset; if (next < 0 || next >= draft.Rules.Count) return;
                    draft.Rules.RemoveAt(index); draft.Rules.Insert(next, rule); BuildRows();
                }
                var up = ActionButton("↑ Höher", () => Move(-1)); up.IsEnabled = number > 1; line.Children.Add(up);
                var down = ActionButton("↓ Niedriger", () => Move(1)); down.IsEnabled = number < draft.Rules.Count; line.Children.Add(down);
                var calibration = new WrapPanel();
                var details = new Expander { Header = "Kalibrierung", Foreground = Color("#E5EDF7"), Content = calibration };
                container.Children.Add(details);
                foreach (var (label, get, set) in new (string, Func<double>, Action<double>)[] { ("Start", () => rule.Start, v => rule.Start = v), ("50 %", () => rule.Mid, v => rule.Mid = v), ("100 %", () => rule.Full, v => rule.Full = v), ("Max PWM", () => rule.MaxPwm, v => rule.MaxPwm = v), ("Timeout s", () => rule.TimeoutSeconds, v => rule.TimeoutSeconds = v), ("Ausklang ms", () => rule.FadeMs, v => rule.FadeMs = checked((int)v)) })
                {
                    var group = new StackPanel { Width = 105, Margin = new Thickness(4) }; group.Children.Add(new TextBlock { Text = label });
                    var input = new TextBox { Text = get().ToString("0.###", CultureInfo.InvariantCulture) }; group.Children.Add(input); calibration.Children.Add(group);
                    commits.Add(() => { if (!double.TryParse(input.Text.Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value)) throw new ArgumentException(rule.Source + ": " + label + " muss eine Zahl sein."); set(value); });
                }
                var curve = new ComboBox { ItemsSource = new[] { "INSTANT", "LINEAR", "SMOOTH", "EXPONENTIAL" }, SelectedItem = rule.Curve, Width = 155 }; curve.SelectionChanged += (_, _) => rule.Curve = (string)curve.SelectedItem; calibration.Children.Add(curve);
                rows.Children.Add(container);
            }
        }
        BuildRows();
        var status = new TextBlock { Text = "Manuell und Pause haben weiterhin Vorrang. Erste aktivierte, frische Quelle gewinnt.", Foreground = Color("#93ADC5") };
        var apply = new Button { Content = "Speichern und anwenden", HorizontalAlignment = HorizontalAlignment.Left };
        panel.Children.Insert(4, apply); panel.Children.Add(status);
        apply.Click += async (_, _) =>
        {
            panel.IsEnabled = false;
            try
            {
                foreach (var commit in commits) commit();
                draft.Movement = (string)movement.SelectedItem; draft.Heart = (string)heart.SelectedItem; draft.HeartMode = (string)mode.SelectedItem; draft.MultiPwm = multi.IsChecked == true; draft.Validate();
                var previous = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!;
                var candidate = JsonSerializer.Deserialize<Settings>(JsonSerializer.Serialize(settings))!; draft.Apply(candidate); candidate.Save();
                try { if (host is not null) await host.ConfigureWindAsync(draft); }
                catch { previous.Save(); throw; }
                draft.Apply(settings); WindMovementSource.SelectedItem = draft.Movement; WindHeartSource.SelectedItem = draft.Heart;
                applyWindControls?.Invoke(); status.Text = "Gespeichert und angewendet. Andere Tools laufen unverändert weiter."; Log("WindForce-Quellen und Prioritäten angewendet");
            }
            catch (Exception ex) { status.Text = "Nicht übernommen: " + ex.Message; Log(status.Text); }
            finally { panel.IsEnabled = true; RefreshToolPages(); }
        };
    }
}
