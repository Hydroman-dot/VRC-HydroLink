using System.Diagnostics;
namespace HydroLink.Core;
public sealed class DashboardCard
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Title { get; set; } = "Neue Karte";
    public string Topic { get; set; } = "fitness/heart/bpm";
    public string Unit { get; set; } = "";
    public string Kind { get; set; } = "Wert";
    public int Width { get; set; } = 280;
    public static List<DashboardCard> Defaults() =>
    [
        new() { Title = "Herzfrequenz", Topic = "fitness/heart/bpm", Unit = "bpm", Kind = "Verlauf" },
        new() { Title = "Geschwindigkeit", Topic = "fitness/speed-kmh", Unit = "km/h", Kind = "Verlauf" },
        new() { Title = "Distanz gesamt", Topic = "fitness/distance-km", Unit = "km" },
        new() { Title = "Schritte gesamt", Topic = "fitness/steps" },
        new() { Title = "Lüfterleistung", Topic = "windforce/output-percent", Unit = "%", Kind = "Balken" },
        new() { Title = "Aktive Zeit", Topic = "fitness/active-seconds", Unit = "s" }
    ];
}
public sealed class LaunchProgram
{
    public string Provider { get; set; } = "VRTI";
    public string Name { get; set; } = "VRTI";
    public string Path { get; set; } = "";
    public string Arguments { get; set; } = "";
    public bool AutoStart { get; set; }
    public static List<LaunchProgram> Defaults() => new[] { "VRTI", "FitOSC", "VRCOSC", "intiface" }.Select(id => new LaunchProgram { Provider = id, Name = id }).ToList();
}
public interface IProgramLauncher { void Start(LaunchProgram program); }
public sealed class ProgramLauncher(Action<string> log) : IProgramLauncher
{
    private readonly HashSet<string> launchedUris = new(StringComparer.OrdinalIgnoreCase);
    public void Start(LaunchProgram program)
    {
        if (program.Path == "steam://rungameid/4411910")
        {
            if (!launchedUris.Add(program.Path)) { log(program.Name + ": Steam-Start bereits angefordert"); return; }
            try { Process.Start(new ProcessStartInfo(program.Path) { UseShellExecute = true, WindowStyle = ProcessWindowStyle.Hidden }); }
            catch { launchedUris.Remove(program.Path); throw; }
            log(program.Name + ": Steam-Start angefordert"); return;
        }
        var path = System.IO.Path.GetFullPath(Environment.ExpandEnvironmentVariables(program.Path));
        if (!File.Exists(path) || !string.Equals(System.IO.Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException(program.Name + ": bitte eine vorhandene EXE auswählen.");
        foreach (var process in Process.GetProcessesByName(System.IO.Path.GetFileNameWithoutExtension(path)))
            using (process)
                try { if (string.Equals(process.MainModule?.FileName, path, StringComparison.OrdinalIgnoreCase)) { log(program.Name + " läuft bereits"); return; } }
                catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException) { }
        using var started = Process.Start(new ProcessStartInfo(path, program.Arguments) { UseShellExecute = false, WorkingDirectory = System.IO.Path.GetDirectoryName(path), CreateNoWindow = true, WindowStyle = ProcessWindowStyle.Hidden });
        log(program.Name + ": Start angefordert");
    }
}
