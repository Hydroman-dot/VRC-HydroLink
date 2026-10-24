using HydroLink.Core;
using HydroLink.Sdk;

internal static class RobustnessChecks
{
    public static async Task Run(Action<bool,string> check)
    {
        await using var worker = new ProbeWorker();
        await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => worker.StartAsync(CancellationToken.None)));
        await worker.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        check(worker.Starts == 1, "concurrent worker starts create only one running service");
        await Task.WhenAll(worker.DisposeAsync().AsTask(), worker.DisposeAsync().AsTask());
        check(worker.Exits == 1, "concurrent disposal waits for one clean worker shutdown");
        worker.Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        await worker.StartAsync(CancellationToken.None);
        await worker.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        check(worker.Starts == 2, "stopped worker can restart with a fresh cancellation token");
        foreach (var value in new[] { float.NaN, float.PositiveInfinity, float.NegativeInfinity })
        {
            bool rejected = false;
            try { OscCodec.Decode(OscCodec.Encode("/bad", value)); } catch (FormatException) { rejected = true; }
            check(rejected, "non-finite OSC input rejected: " + value);
        }
        var cacheRoot = Path.Combine(Path.GetTempPath(), "HydroLink-cache-test-" + Guid.NewGuid().ToString("N"));
        var avatarDir = Path.Combine(cacheRoot, "OSC", "usr_" + Guid.NewGuid(), "Avatars");
        Directory.CreateDirectory(avatarDir);
        var avatarFile = Path.Combine(avatarDir, "avtr_" + Guid.NewGuid() + ".json");
        var otherFile = Path.Combine(avatarDir, "custom.json");
        File.WriteAllText(avatarFile, "original"); File.WriteAllText(otherFile, "keep");
        var selected = VrchatOscCache.Inspect(Path.Combine(cacheRoot, "OSC"));
        check(selected.SequenceEqual(new[] { avatarFile }), "cache reset selects only standard avatar JSON files");
        bool rejectedPath = false;
        try { VrchatOscCache.Clear(Path.Combine(cacheRoot, "OSC"), Path.Combine(cacheRoot, "backup"), [otherFile]); }
        catch (IOException) { rejectedPath = true; }
        check(rejectedPath && File.Exists(avatarFile), "cache reset rejects unapproved files before changes");
        var backup = VrchatOscCache.Clear(Path.Combine(cacheRoot, "OSC"), Path.Combine(cacheRoot, "backup"), selected);
        var saved = Path.Combine(backup, Path.GetRelativePath(Path.Combine(cacheRoot, "OSC"), avatarFile));
        check(!File.Exists(avatarFile) && File.ReadAllText(saved) == "original" && File.ReadAllText(otherFile) == "keep", "cache reset preserves backup and unrelated files");
        File.Delete(saved); File.Delete(otherFile);
        foreach (var directory in Directory.EnumerateDirectories(cacheRoot, "*", SearchOption.AllDirectories).OrderByDescending(p => p.Length)) Directory.Delete(directory);
        Directory.Delete(cacheRoot);
        var folder = Path.Combine(Path.GetTempPath(), "HydroLink-robustness-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "settings.json");
        try
        {
            new Settings { WindPort = 4222 }.Save(path);
            new Settings { WindPort = 4333 }.Save(path);
            check(Settings.Load(path).WindPort == 4333 && Settings.Load(path + ".bak").WindPort == 4222, "atomic save keeps previous valid settings backup");
            File.WriteAllText(path, "{broken");
            bool warned = false;
            var restored = Settings.Load(path, _ => warned = true);
            check(restored.WindPort == 4222 && warned && File.ReadAllText(path) == "{broken", "damaged settings recover with warning without overwriting evidence");
            restored.Save(path);
            check(Settings.Load(path + ".bak").WindPort == 4222 && Settings.Load(path).WindPort == 4222, "saving recovered settings preserves valid backup");
            await Task.WhenAll(Enumerable.Range(0, 8).Select(i => Task.Run(() => new Settings { WindPort = 4400 + i }.Save(path))));
            check(Settings.Load(path).WindPort is >= 4400 and <= 4407 && !Directory.EnumerateFiles(folder, "*.tmp").Any(), "concurrent saves produce valid settings and no temporary leftovers");
        }
        finally { foreach (var file in Directory.EnumerateFiles(folder)) File.Delete(file); Directory.Delete(folder); }
    }
    private sealed class ProbeWorker() : Worker("probe", ComponentKind.Service, _ => { })
    {
        public int Starts, Exits;
        public TaskCompletionSource<bool> Entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        protected override async Task Run(CancellationToken token)
        {
            Interlocked.Increment(ref Starts); Entered.TrySetResult(true);
            try { await Task.Delay(Timeout.Infinite, token); }
            finally { Interlocked.Increment(ref Exits); }
        }
    }
}
