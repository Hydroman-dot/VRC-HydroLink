using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using VrcHapticsBridge.Core;
namespace HydroLink.Core;

public sealed record WizardProfile(int SchemaVersion, string Revision, string AvatarId, string AvatarName, string[] Toys, int Plugs, int Sockets)
{
    public bool Includes(ToyKind toy) => Toys.Contains(toy.ToString(), StringComparer.Ordinal);
    public bool MatchesAvatar(string avatar) => AvatarId.Length > 0 && string.Equals(AvatarId, avatar, StringComparison.Ordinal);
}
public sealed record WizardSyncState(bool Present, WizardProfile? Profile, string Fingerprint, string Status);
public sealed class WizardSyncReader(string? path = null)
{
    public static string DefaultPath => Path.Combine(Settings.Folder, "WizardSync", "active.json");
    private readonly string file = path ?? DefaultPath;
    public WizardSyncState Read(string avatar = "")
    {
        if (string.IsNullOrEmpty(avatar)) return ReadFile(file);
        if (!ValidAvatarId(avatar)) return new(true, null, "unmatched:" + avatar, "VRChat meldet keine gültige Avatar-ID.");
        var selected = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(file))!, "Avatars", avatar + ".json");
        if (File.Exists(selected))
        {
            var state = ReadFile(selected);
            return state.Profile != null && !state.Profile.MatchesAvatar(avatar)
                ? new(true, null, "invalid:identity:" + avatar, "Gespeicherte Einrichtung hat eine abweichende Avatar-ID.") : state;
        }
        var latest = ReadFile(file);
        if (latest.Profile?.MatchesAvatar(avatar) == true) return latest;
        var hasProfiles = Directory.Exists(Path.GetDirectoryName(selected));
        if (!latest.Present && !hasProfiles) return latest;
        return new(true, null, "unmatched:" + avatar, "Für den geladenen Avatar ist noch keine Unity-Einrichtung gespeichert. Diesen Avatar einmal im Wizard übergeben.");
    }
    public static bool ValidAvatarId(string id) => Regex.IsMatch(id, @"\Aavtr_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\z");
    private static WizardSyncState ReadFile(string file)
    {
        try
        {
            if (!File.Exists(file)) return new(false, null, "missing", "Noch keine Übergabe aus dem Unity-Wizard.");
            using var stream = new FileStream(file, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            if (stream.Length is <= 0 or > 65536) throw new InvalidDataException("Ungültige Größe der Wizard-Datei.");
            using var memory = new MemoryStream();
            var buffer = new byte[4096]; int read;
            while ((read = stream.Read(buffer, 0, buffer.Length)) > 0)
            { if (memory.Length + read > 65536) throw new InvalidDataException("Wizard-Datei zu groß."); memory.Write(buffer, 0, read); }
            var bytes = memory.ToArray();
            var profile = Parse(bytes);
            return new(true, profile, Convert.ToHexString(SHA256.HashData(bytes)), $"Aus Unity übernommen: {profile.AvatarName} · {string.Join(" + ", profile.Toys)}");
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException or JsonException or ArgumentException)
        { return new(true, null, "invalid:" + ex.Message, "Unity-Übergabe nicht nutzbar: " + ex.Message); }
    }
    public static WizardProfile Parse(byte[] json)
    {
        var p = JsonSerializer.Deserialize<WizardProfile>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true, MaxDepth = 12 })
            ?? throw new InvalidDataException("Leere Wizard-Datei.");
        if (p.SchemaVersion != 1 || !Guid.TryParse(p.Revision, out _) || string.IsNullOrWhiteSpace(p.AvatarName) || p.AvatarName.Length > 256 || p.AvatarName.Any(char.IsControl)
            || p.AvatarId == null || p.AvatarId.Length > 64 || p.AvatarId.Length > 0 && !Regex.IsMatch(p.AvatarId, @"\Aavtr_[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}\z")
            || p.Toys == null || p.Toys.Length > Enum.GetValues<ToyKind>().Length || p.Toys.Distinct().Count() != p.Toys.Length || p.Toys.Any(t => !Enum.TryParse<ToyKind>(t, out var kind) || !Enum.IsDefined(kind) || kind.ToString() != t)
            || p.Plugs is < 0 or > 10000 || p.Sockets is < 0 or > 10000)
            throw new InvalidDataException("Unbekanntes oder unvollständiges Wizard-Profil.");
        return p;
    }
}
