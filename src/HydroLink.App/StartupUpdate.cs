using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using HydroLink.Core;
namespace HydroLink.App;
public partial class MainWindow
{
    private ProcessStartInfo? updateOnExit;
    private bool closing;
    private async Task CheckStartupUpdate()
    {
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            http.DefaultRequestHeaders.UserAgent.ParseAdd("HydroLink-Updater/0.16.4");
            using var json = JsonDocument.Parse(await http.GetStringAsync("https://api.github.com/repos/Hydroman-dot/VRC-HydroLink/releases/latest"));
            var current = Assembly.GetExecutingAssembly().GetName().Version!;
            var version = ReleaseUpdate.NewerVersion(json.RootElement, current);
            if (version == null || closed || closing || !IsVisible) return;
            var script = Path.Combine(AppContext.BaseDirectory, "updater", "Update-HydroLink.ps1");
            if (!File.Exists(script)) { Log("Update verfügbar: " + version + " · Updater-Dateien fehlen im Programmordner."); return; }
            if (MessageBox.Show(this, $"HydroLink {version} ist verfügbar. Jetzt aktualisieren?\n\nHydroLink beendet dafür seine Module und startet nach dem Update neu.", "HydroLink aktualisieren", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            var start = new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "WindowsPowerShell", "v1.0", "powershell.exe")) { UseShellExecute = false, CreateNoWindow = true, WorkingDirectory = AppContext.BaseDirectory };
            foreach (var argument in new[] { "-NoProfile", "-ExecutionPolicy", "Bypass", "-File", script, "-Install", "-Launch", "-DesktopShortcut", "-WaitForPid", Environment.ProcessId.ToString(), "-ExpectedVersion", version }) start.ArgumentList.Add(argument);
            updateOnExit = start; Close();
        }
        catch (Exception ex) { if (!closed) Log("Update-Prüfung nicht möglich; HydroLink bleibt nutzbar: " + ex.Message); }
    }
}
