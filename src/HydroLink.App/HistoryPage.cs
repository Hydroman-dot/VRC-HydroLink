using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Shapes;
using HydroLink.Core;
using Microsoft.Win32;

namespace HydroLink.App;
public partial class MainWindow
{
    private void BuildHistoryPage()
    {
        var page = Page("Sessions und Auswertung", "Neue HydroLink-Sessions und vorhandene CSV-Dateien aus VR Fitness ansehen. Originaldateien werden beim Öffnen nicht verändert.");
        var history = new ComboBox { MinWidth = 360, MaxWidth = 800, HorizontalAlignment = HorizontalAlignment.Left, DisplayMemberPath = "Name" };
        var actions = new WrapPanel();
        var reportView = new TextBox { IsReadOnly = true, TextWrapping = TextWrapping.Wrap, FontFamily = new FontFamily("Consolas"), MinHeight = 160, MaxHeight = 430, VerticalScrollBarVisibility = ScrollBarVisibility.Auto };
        var overview = new StackPanel(); var chart = new StackPanel(); var note = new TextBox { AcceptsReturn = true, MinHeight = 60 };
        SessionReport? report = null;
        var goals = new TextBlock { Margin = new Thickness(0, 12, 0, 16), Foreground = Color("#7CE8CF") };
        void RefreshHistory()
        {
            try
            {
            var folder = settings.FitnessOptions.DataDirectory;
            if (!Directory.Exists(folder))
            {
                history.ItemsSource = Array.Empty<FileInfo>();
                goals.Text = "Noch keine Sessions. Der Datenordner wird bei der ersten Aufzeichnung angelegt.";
                return;
            }
            history.ItemsSource = new DirectoryInfo(folder).EnumerateFiles("*.csv", SearchOption.AllDirectories).OrderByDescending(f => f.LastWriteTimeUtc).Take(300).ToArray();
            var monday = DateTime.Today.AddDays(-((7 + (int)DateTime.Today.DayOfWeek - 1) % 7));
            double distance = 0, active = 0, steps = 0;
            foreach (var path in Directory.EnumerateFiles(folder, "*.json", SearchOption.AllDirectories)
                .Where(p => File.Exists(System.IO.Path.ChangeExtension(p, ".csv")) || File.Exists(System.IO.Path.Combine(System.IO.Path.GetDirectoryName(p)!, System.IO.Path.GetFileNameWithoutExtension(p).Replace("_summary", "") + ".csv")))
                .GroupBy(p => System.IO.Path.GetFileNameWithoutExtension(p).Replace("_summary", "")).Select(g => g.First()))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllText(path)); var r = doc.RootElement;
                    if (!r.TryGetProperty("start", out var started) || !DateTimeOffset.TryParse(started.GetString(), out var when) || when.LocalDateTime < monday || when.LocalDateTime >= monday.AddDays(7)) continue;
                    double Num(string key) => r.TryGetProperty(key, out var value) && value.ValueKind == JsonValueKind.Number && value.TryGetDouble(out var n) ? n : 0;
                    distance += Num("distance_km"); active += Num("active_s"); steps += Num("steps");
                }
                catch (Exception ex) when (ex is JsonException or IOException or InvalidOperationException) { }
            }
            goals.Text = $"Diese Woche: {distance:0.##} / {settings.FitnessOptions.WeeklyKm:0.##} km  ·  {active / 3600:0.##} / {settings.FitnessOptions.WeeklyHours:0.##} h aktiv  ·  {steps:0} / {settings.FitnessOptions.WeeklySteps} Schritte";
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or System.Security.SecurityException)
            {
                history.ItemsSource = Array.Empty<FileInfo>();
                goals.Text = "Session-Ordner nicht lesbar. Bitte unter VR Fitness einen zugänglichen Datenordner auswählen.";
                Log("Session-Historie: " + ex.Message);
            }
        }
        void Load(string path)
        {
            try
            {
                report = SessionAnalysis.Read(path); reportView.Text = report.Text;
                note.Text = File.Exists(path + ".hydrolink-note.txt") ? File.ReadAllText(path + ".hydrolink-note.txt") : report.Summary["note"]?.ToString() ?? "";
                overview.Children.Clear(); overview.Children.Add(SessionReportView.Overview(report));
                chart.Children.Clear();
                chart.Children.Add(SessionReportView.Chart(report, "clean_bpm", "Herzfrequenz · bpm"));
                chart.Children.Add(SessionReportView.Chart(report, "clean_speed_kmh", "Geschwindigkeit · km/h"));
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Analyse fehlgeschlagen"); }
        }
        history.SelectionChanged += (_, _) => { if (history.SelectedItem is FileInfo file) Load(file.FullName); };
        actions.Children.Add(ActionButton("Liste aktualisieren", RefreshHistory));
        actions.Children.Add(ActionButton("VR-Fitness-CSV öffnen", () => { var choose = new OpenFileDialog { Filter = "Fitness CSV (*.csv)|*.csv" }; if (choose.ShowDialog(this) == true) Load(choose.FileName); }));
        actions.Children.Add(ActionButton("Daten sichern", () =>
        {
            var dialog = new SaveFileDialog { Filter = "Backup (*.zip)|*.zip", FileName = $"HydroLink-Sessions-{DateTime.Now:yyyyMMdd-HHmmss}.zip" };
            if (dialog.ShowDialog(this) != true) return;
            try
            {
                var root = System.IO.Path.GetFullPath(settings.FitnessOptions.DataDirectory).TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar;
                if (System.IO.Path.GetFullPath(dialog.FileName).StartsWith(root, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Backup bitte außerhalb des Session-Ordners speichern.");
                using var stream = new FileStream(dialog.FileName, FileMode.Create, FileAccess.Write, FileShare.None);
                using var archive = new ZipArchive(stream, ZipArchiveMode.Create);
                foreach (var file in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories).Where(p => System.IO.Path.GetExtension(p) is ".csv" or ".json" or ".txt")) archive.CreateEntryFromFile(file, System.IO.Path.GetRelativePath(root, file));
                Log("Session-Backup erstellt");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Backup fehlgeschlagen"); }
        }));
        actions.Children.Add(ActionButton("Backup importieren", () =>
        {
            var dialog = new OpenFileDialog { Filter = "Backup (*.zip)|*.zip" }; if (dialog.ShowDialog(this) != true) return;
            try
            {
                var target = System.IO.Path.Combine(settings.FitnessOptions.DataDirectory, "import-" + Guid.NewGuid().ToString("N"));
                using var archive = ZipFile.OpenRead(dialog.FileName);
                if (archive.Entries.Sum(e => e.Length) > 1024L * 1024 * 1024) throw new InvalidDataException("Backup größer als 1 GB");
                foreach (var entry in archive.Entries)
                {
                    if (System.IO.Path.GetExtension(entry.Name).ToLowerInvariant() is not (".csv" or ".json" or ".txt")) continue;
                    var destination = System.IO.Path.GetFullPath(System.IO.Path.Combine(target, entry.FullName));
                    if (!destination.StartsWith(System.IO.Path.GetFullPath(target) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("Ungültiger Archivpfad");
                    Directory.CreateDirectory(System.IO.Path.GetDirectoryName(destination)!); entry.ExtractToFile(destination);
                }
                RefreshHistory(); Log("Backup in eigenen Importordner eingelesen; bestehende Sessions bleiben erhalten");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Import fehlgeschlagen"); }
        }));
        actions.Children.Add(ActionButton("Bericht als Text exportieren", () =>
        {
            if (report is null) return; var save = new SaveFileDialog { Filter = "Text (*.txt)|*.txt", FileName = "Fitness-Bericht.txt" };
            if (save.ShowDialog(this) == true) try { File.WriteAllText(save.FileName, report.Text + Environment.NewLine + note.Text); } catch (Exception ex) { MessageBox.Show(this, ex.Message); }
        }));
        actions.Children.Add(ActionButton("Bericht drucken / PDF", () =>
        {
            if (report is null) return;
            try
            {
                var print = new PrintDialog(); if (print.ShowDialog() != true) return;
                var document = SessionReportView.Print(report, note.Text, print.PrintableAreaWidth, print.PrintableAreaHeight);
                print.PrintDocument(((IDocumentPaginatorSource)document).DocumentPaginator, "VRC HydroLink Fitness");
            }
            catch (Exception ex) { MessageBox.Show(this, ex.Message, "Drucken fehlgeschlagen"); }
        }));
        page.Children.Add(history); page.Children.Add(goals); page.Children.Add(actions); page.Children.Add(overview); page.Children.Add(chart); page.Children.Add(new Expander { Header = "Technische Details", Content = reportView }); page.Children.Add(new TextBlock { Text = "Session-Notiz (separate HydroLink-Datei)" }); page.Children.Add(note);
        page.Children.Add(ActionButton("Notiz speichern", () => { if (report is null) return; try { File.WriteAllText(report.FilePath + ".hydrolink-note.txt", note.Text); } catch (Exception ex) { MessageBox.Show(this, ex.Message); } }));
        refreshSessionHistory = RefreshHistory;
        page.IsVisibleChanged += (_, _) => { if (page.IsVisible) RefreshHistory(); };
        RefreshHistory();
        var args = Environment.GetCommandLineArgs(); var preview = Array.IndexOf(args, "--preview-session");
        if (args.Contains("--render-preview") && preview >= 0 && preview + 1 < args.Length) Load(args[preview + 1]);
        Tabs.Items.Insert(4, new TabItem { Header = "Sessions", Content = new ScrollViewer { Content = page, VerticalScrollBarVisibility = ScrollBarVisibility.Auto } });
    }
}
