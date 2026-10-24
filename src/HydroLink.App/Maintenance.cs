using System.Windows;
using System.Windows.Controls;
using HydroLink.Core;

namespace HydroLink.App;
public partial class MainWindow
{
    private bool maintenanceBusy;
    private async void RestartModule_Click(object sender, RoutedEventArgs e)
    {
        if (maintenanceBusy || sender is not Button button || button.Tag is not string id) return;
        maintenanceBusy = true; button.IsEnabled = false;
        try
        {
            await EnsureHost();
            await host!.RestartModuleAsync(id);
            RefreshToolPages(); Refresh();
        }
        catch (Exception ex) { Log("Neustart fehlgeschlagen: " + ex.Message); MessageBox.Show(this, ex.Message, "Modul neu starten"); }
        finally { maintenanceBusy = false; button.IsEnabled = true; }
    }
    private async void ClearOscCache_Click(object sender, RoutedEventArgs e)
    {
        if (maintenanceBusy || sender is not Button button) return;
        maintenanceBusy = true; button.IsEnabled = false;
        try
        {
            var root = VrchatOscCache.DefaultRoot;
            var files = VrchatOscCache.Inspect(root);
            if (files.Length == 0) { MessageBox.Show(this, "Keine OSC-Avatar-Dateien gefunden.\n" + root, "OSC-Cache"); return; }
            var prompt = $"{files.Length} OSC-Avatar-Konfigurationen werden gesichert und aus folgendem Ordner entfernt:\n{root}\n\n"
                + string.Join("\n", files.Take(8).Select(System.IO.Path.GetFileName)) + (files.Length > 8 ? "\n…" : "")
                + "\n\nEigene OSC-Zuordnungen in diesen Dateien werden dabei zurückgesetzt. Anschließend den Avatar in VRChat neu laden. Fortfahren?";
            if (MessageBox.Show(this, prompt, "OSC-Cache sichern und leeren", MessageBoxButton.YesNo, MessageBoxImage.Question, MessageBoxResult.No) != MessageBoxResult.Yes) return;
            if (host?.Haptics is { } haptics) await haptics.StopDevicesAsync();
            var backup = await Task.Run(() => VrchatOscCache.Clear(root, System.IO.Path.Combine(Settings.Folder, "OscCacheBackups"), files));
            host?.Bus.Publish("routing/reset", true, "cache-reset");
            Log("OSC-Cache geleert. Sicherung: " + backup);
            MessageBox.Show(this, "OSC-Cache geleert. Jetzt den Avatar in VRChat neu laden und Haptics erneut aktivieren.\n\nSicherung (gleiche Unterordnerstruktur zum Zurückkopieren):\n" + backup, "OSC-Cache");
        }
        catch (Exception ex) { Log("OSC-Cache: " + ex.Message); MessageBox.Show(this, ex.Message, "OSC-Cache konnte nicht vollständig geleert werden"); }
        finally { maintenanceBusy = false; button.IsEnabled = true; }
    }
}
