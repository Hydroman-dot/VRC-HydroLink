namespace HydroLink.Core;

public static class VrchatOscCache
{
    public static string DefaultRoot => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "AppData", "LocalLow", "VRChat", "VRChat", "OSC");
    private static bool Identity(string name, string prefix) => name.StartsWith(prefix, StringComparison.Ordinal) && Guid.TryParseExact(name[prefix.Length..], "D", out _);
    private static void NoLinks(string path)
    {
        for (var current = new DirectoryInfo(Path.GetDirectoryName(Path.GetFullPath(path))!); current != null; current = current.Parent)
            if (current.Exists && current.Attributes.HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Verknüpfte Cache-Pfade werden nicht verändert.");
        if (File.Exists(path) && File.GetAttributes(path).HasFlag(FileAttributes.ReparsePoint)) throw new IOException("Verknüpfte Cache-Datei wird nicht verändert.");
    }
    public static string[] Inspect(string root)
    {
        root = Path.GetFullPath(root);
        NoLinks(Path.Combine(root, "probe"));
        if (!Directory.Exists(root)) return [];
        var result = new List<string>();
        foreach (var user in Directory.EnumerateDirectories(root).Where(p => Identity(Path.GetFileName(p), "usr_")))
        {
            var avatars = Path.Combine(user, "Avatars");
            NoLinks(Path.Combine(avatars, "probe"));
            if (!Directory.Exists(avatars)) continue;
            foreach (var file in Directory.EnumerateFiles(avatars, "*.json").Where(p => Identity(Path.GetFileNameWithoutExtension(p), "avtr_")))
            { NoLinks(file); result.Add(file); }
        }
        return result.OrderBy(p => p).ToArray();
    }
    public static string Clear(string root, string backupRoot, IReadOnlyList<string> approvedFiles)
    {
        root = Path.GetFullPath(root);
        var allowed = Inspect(root).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var files = approvedFiles.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        if (files.Length == 0) throw new InvalidOperationException("Keine OSC-Avatar-Dateien ausgewählt.");
        if (files.Any(f => !allowed.Contains(f))) throw new IOException("Cache-Dateien haben sich geändert. Bitte erneut prüfen.");
        var backup = Path.Combine(Path.GetFullPath(backupRoot), DateTime.UtcNow.ToString("yyyyMMdd-HHmmss") + "-" + Guid.NewGuid().ToString("N"));
        if (backup.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new IOException("Sicherung muss außerhalb des OSC-Ordners liegen.");
        NoLinks(Path.Combine(backup, "probe"));
        Directory.CreateDirectory(backup);
        // All files are backed up before any original is removed. Locked originals remain intact.
        foreach (var file in files)
        {
            NoLinks(file);
            var target = Path.Combine(backup, Path.GetRelativePath(root, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, false);
        }
        foreach (var file in files)
        {
            NoLinks(file);
            var saved = Path.Combine(backup, Path.GetRelativePath(root, file));
            if (!File.ReadAllBytes(file).AsSpan().SequenceEqual(File.ReadAllBytes(saved)))
                throw new IOException("Cache wurde während der Sicherung geändert. Sicherung: " + backup);
        }
        try { foreach (var file in files) { NoLinks(file); File.Delete(file); } }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        { throw new IOException("Cache nur teilweise geleert. Sicherung aller ausgewählten Dateien: " + backup, ex); }
        return backup;
    }
}
