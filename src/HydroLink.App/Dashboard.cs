using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using HydroLink.Core;
using Microsoft.Win32;

namespace HydroLink.App;
public partial class MainWindow
{
    private readonly Dictionary<string, (TextBlock Value, TextBlock Age, ProgressBar? Bar, Polyline? Line)> cardViews = [];
    private readonly Dictionary<string, Queue<double>> chartHistory = [];
    private bool UiReady;
    private static Brush Color(string value) => (Brush)new BrushConverter().ConvertFromString(value)!;
    private static Button ActionButton(string label, Action action)
    {
        var button = new Button { Content = label }; button.Click += (_, _) => action(); return button;
    }
    private void DashboardEdit_Changed(object sender, RoutedEventArgs e) { if (UiReady) BuildDashboard(); }
    private void AddCard_Click(object sender, RoutedEventArgs e) => EditCard(null);
    private void EditCard(DashboardCard? existing)
    {
        var title = new TextBox { Text = existing?.Title ?? "Neue Karte" };
        var topic = new ComboBox { ItemsSource = DashboardTopics(), SelectedItem = existing?.Topic ?? "fitness/heart/bpm" };
        var customTopic = new TextBox { Text = existing?.Topic ?? "fitness/heart/bpm" };
        topic.SelectionChanged += (_, _) => { if (topic.SelectedItem is string text) customTopic.Text = text; };
        var unit = new TextBox { Text = existing?.Unit ?? "" };
        var kind = new ComboBox { ItemsSource = new[] { "Wert", "Verlauf", "Balken", "Text" }, SelectedItem = existing?.Kind ?? "Wert" };
        var width = new ComboBox { ItemsSource = new[] { 280, 420, 580, existing?.Width ?? 280 }.Distinct().Order().ToArray(), SelectedItem = existing?.Width ?? 280 };
        var content = new StackPanel { Margin = new Thickness(22) };
        foreach (var (label, control) in new (string, UIElement)[] { ("Titel", title), ("Vorhandene Daten", topic), ("Topic (auch eigene Plugin-Daten)", customTopic), ("Einheit", unit), ("Darstellung", kind), ("Breite", width) }) { content.Children.Add(new TextBlock { Text = label }); content.Children.Add(control); }
        var dialog = new Window { Title = "Dashboard-Karte", Owner = this, Width = 520, Height = 660, Background = Color("#172334"), Foreground = Color("#E5EDF7"), Content = content, WindowStartupLocation = WindowStartupLocation.CenterOwner };
        content.Children.Add(ActionButton("Karte speichern", () =>
        {
            if (string.IsNullOrWhiteSpace(title.Text) || string.IsNullOrWhiteSpace(customTopic.Text)) { MessageBox.Show(dialog, "Titel und Topic ausfüllen."); return; }
            if (existing is null && settings.Dashboard.Count >= 60) { MessageBox.Show(dialog, "Maximal 60 Karten pro Dashboard."); return; }
            var card = existing ?? new DashboardCard(); card.Title = title.Text.Trim(); card.Topic = customTopic.Text.Trim(); card.Unit = unit.Text; card.Kind = (string)kind.SelectedItem; card.Width = (int)width.SelectedItem;
            if (existing is null) settings.Dashboard.Add(card);
            PersistLayout(); BuildDashboard(); dialog.Close();
        }));
        dialog.ShowDialog();
    }
    private string[] DashboardTopics() => DashboardCard.Defaults().Select(c => c.Topic).Concat(new[] { "fitness/target-speed-kmh", "fitness/speed-limit-kmh", "fitness/autowalk-speed", "companion/steps_today", "companion/spo2_pct", "companion/weight_kg", "windforce/source", "windforce/device/rssi", "windforce/device/fan1", "windforce/device/fan2", "windforce/device/fan3", "windforce/device/rpm", "session/distance-km", "session/steps", "session/elapsed-seconds" }).Concat(latest.Keys).Distinct().Order().ToArray();
    private void PersistLayout() { try { settings.Save(); } catch (Exception ex) { MessageBox.Show(this, ex.Message, "Speichern fehlgeschlagen"); } }
    private void BuildDashboard()
    {
        DashboardCards.Children.Clear(); cardViews.Clear();
        foreach (var card in settings.Dashboard)
        {
            var panel = new StackPanel();
            var title = new TextBlock { Text = card.Title, Foreground = Color("#AAC3D9"), FontSize = 15 };
            panel.Children.Add(title);
            var value = new TextBlock { Text = DurationDisplay.IsDuration(card.Topic) ? "—" : "— " + card.Unit, FontSize = card.Kind == "Text" ? 15 : 30, Margin = new Thickness(0, 16, 0, 8), MaxHeight = 100 };
            panel.Children.Add(value);
            var age = new TextBlock { Text = "Noch keine Daten", Foreground = Color("#93ADC5"), FontSize = 11 }; panel.Children.Add(age);
            ProgressBar? bar = null; Polyline? line = null;
            if (card.Kind == "Balken") { bar = new ProgressBar { Minimum = 0, Maximum = 100, Height = 12, Margin = new Thickness(0, 16, 0, 0), Foreground = Color("#64DAC0"), Background = Color("#0B1320") }; panel.Children.Add(bar); }
            if (card.Kind == "Verlauf") { var canvas = new Canvas { Height = 65, Margin = new Thickness(0, 16, 0, 0), ClipToBounds = true }; line = new Polyline { Stroke = Color("#64DAC0"), StrokeThickness = 2 }; canvas.Children.Add(line); panel.Children.Add(canvas); }
            var border = new Border { Background = Color("#203248"), CornerRadius = new CornerRadius(10), Padding = new Thickness(18), Margin = new Thickness(0, 0, 14, 14), Width = card.Width, Child = panel, Tag = card };
            if (EditDashboard.IsChecked == true)
            {
                var row = new WrapPanel { Margin = new Thickness(0, 10, 0, 0) };
                row.Children.Add(ActionButton("Bearbeiten", () => EditCard(card)));
                row.Children.Add(ActionButton("←", () => MoveCard(card, -1))); row.Children.Add(ActionButton("→", () => MoveCard(card, 1)));
                row.Children.Add(ActionButton("Entfernen", () => { settings.Dashboard.Remove(card); PersistLayout(); BuildDashboard(); })); panel.Children.Add(row);
                title.Text += "  ⠿"; title.Cursor = Cursors.SizeAll;
                Point? start = null;
                title.MouseLeftButtonDown += (_, e) => start = e.GetPosition(title);
                title.MouseMove += (_, e) => { if (e.LeftButton == MouseButtonState.Pressed && start is { } point && (e.GetPosition(title) - point).Length > 8) { start = null; DragDrop.DoDragDrop(title, new DataObject("HydroLink.Card", card.Id), DragDropEffects.Move); } };
                border.AllowDrop = true;
                border.Drop += (_, e) =>
                {
                    if (e.Data.GetData("HydroLink.Card") is not string id) return;
                    var moving = settings.Dashboard.FirstOrDefault(c => c.Id == id); if (moving is null || moving == card) return;
                    var target = settings.Dashboard.IndexOf(card); settings.Dashboard.Remove(moving); settings.Dashboard.Insert(Math.Min(target, settings.Dashboard.Count), moving); PersistLayout(); BuildDashboard();
                };
            }
            var menu = new ContextMenu();
            var edit = new MenuItem { Header = "Karte bearbeiten" }; edit.Click += (_, _) => EditCard(card); menu.Items.Add(edit);
            var remove = new MenuItem { Header = "Karte entfernen" }; remove.Click += (_, _) => { settings.Dashboard.Remove(card); PersistLayout(); BuildDashboard(); }; menu.Items.Add(remove); border.ContextMenu = menu;
            cardViews[card.Id] = (value, age, bar, line); DashboardCards.Children.Add(border);
        }
    }
    private void MoveCard(DashboardCard card, int delta)
    {
        int position = settings.Dashboard.IndexOf(card), target = Math.Clamp(position + delta, 0, settings.Dashboard.Count - 1);
        settings.Dashboard.RemoveAt(position); settings.Dashboard.Insert(target, card); PersistLayout(); BuildDashboard();
    }
    private void RefreshDashboard()
    {
        foreach (var card in settings.Dashboard)
        {
            if (!cardViews.TryGetValue(card.Id, out var view)) continue;
            if (!latest.TryGetValue(card.Topic, out var data)) { view.Value.Text = DurationDisplay.IsDuration(card.Topic) ? "—" : "— " + card.Unit; view.Age.Text = "Noch keine Daten"; if (view.Bar is { } b) b.Value = 0; continue; }
            var age = DateTimeOffset.UtcNow - data.Timestamp;
            bool numeric = data.Value.ValueKind == JsonValueKind.Number && data.Value.TryGetDouble(out _);
            double number = numeric ? data.Value.GetDouble() : 0;
            view.Value.Text = numeric && DurationDisplay.IsDuration(card.Topic) ? DurationDisplay.Format(number) : (numeric ? number.ToString("0.##") : data.Value.ToString()) + " " + card.Unit;
            view.Age.Text = $"{data.Source} · vor {age.TotalSeconds:0} s" + (age.TotalSeconds > 10 ? " · letzter Wert" : "");
            view.Value.Opacity = age.TotalSeconds > 10 ? .55 : 1;
            if (view.Bar is { } bar) bar.Value = numeric && age.TotalSeconds <= 10 ? Math.Clamp(number, 0, 100) : 0;
            if (view.Line is { } line && chartHistory.TryGetValue(card.Topic, out var history) && history.Count > 1)
            {
                var points = history.ToArray(); double min = points.Min(), range = Math.Max(1, points.Max() - min);
                line.Points = new PointCollection(points.Select((v, i) => new Point(i * (card.Width - 40d) / (points.Length - 1), 60 - (v - min) / range * 55)));
            }
        }
    }
    private void ExportDashboard_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "Dashboard (*.json)|*.json", FileName = "HydroLink-Dashboard.json" };
        if (dialog.ShowDialog(this) == true) try { File.WriteAllText(dialog.FileName, JsonSerializer.Serialize(settings.Dashboard, new JsonSerializerOptions { WriteIndented = true })); } catch (Exception ex) { MessageBox.Show(this, ex.Message); }
    }
    private void ImportDashboard_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "Dashboard (*.json)|*.json" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            if (new FileInfo(dialog.FileName).Length > 1024 * 1024) throw new InvalidDataException("Dashboard-Datei zu groß");
            var cards = JsonSerializer.Deserialize<List<DashboardCard>>(File.ReadAllText(dialog.FileName)) ?? throw new InvalidDataException("Keine Karten");
            if (cards.Count > 60 || cards.Any(c => c is null || string.IsNullOrWhiteSpace(c.Topic) || string.IsNullOrWhiteSpace(c.Title) || c.Width is < 200 or > 800 || c.Kind is not ("Wert" or "Verlauf" or "Balken" or "Text"))) throw new InvalidDataException("Ungültiges Dashboard");
            foreach (var card in cards) card.Id = Guid.NewGuid().ToString("N");
            settings.Dashboard = cards; PersistLayout(); BuildDashboard();
        }
        catch (Exception ex) { MessageBox.Show(this, ex.Message, "Import fehlgeschlagen"); }
    }
}
