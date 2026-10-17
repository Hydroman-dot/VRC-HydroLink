using System.Text.Json;
namespace HydroLink.Core;
public sealed record OscParameter(string Address, string Type, string Access, string QueryValue, string Received, string Sent, string Updated);
public sealed class OscMonitor
{
    private readonly object gate = new();
    private readonly Dictionary<string, OscParameter> rows = [];
    public string Avatar { get; private set; } = "";
    public bool SetAvatar(string id)
    {
        lock (gate) { if (Avatar == id) return false; Avatar = id; rows.Clear(); return true; }
    }
    public OscParameter[] Snapshot() { lock (gate) return rows.Values.OrderBy(r => r.Address).ToArray(); }
    public void Message(string address, object[] arguments, bool sent)
    {
        lock (gate)
        {
            if (!rows.ContainsKey(address) && rows.Count >= 8192) return;
            var row = rows.GetValueOrDefault(address) ?? new(address, string.Join("", arguments.Select(a => a switch { bool b => b ? "T" : "F", int => "i", float or double => "f", string => "s", _ => "?" })), "unbekannt", "—", "—", "—", "");
            var value = JsonSerializer.Serialize(arguments);
            rows[address] = row with { Received = sent ? row.Received : value, Sent = sent ? value : row.Sent, Updated = DateTimeOffset.Now.ToString("HH:mm:ss.fff") };
        }
    }
    public static IEnumerable<(string Address, string Type, int Access, string Value)> ParseTree(JsonElement node, int depth = 0)
    {
        if (depth > 32 || node.ValueKind != JsonValueKind.Object) yield break;
        if (node.TryGetProperty("FULL_PATH", out var path) && path.GetString() is { } address && address.StartsWith("/avatar/parameters/"))
            yield return (address, node.TryGetProperty("TYPE", out var type) ? type.GetString() ?? "?" : "?", node.TryGetProperty("ACCESS", out var access) && access.TryGetInt32(out var a) ? a : 0, node.TryGetProperty("VALUE", out var value) ? value.GetRawText() : "—");
        if (node.TryGetProperty("CONTENTS", out var contents) && contents.ValueKind == JsonValueKind.Object)
            foreach (var child in contents.EnumerateObject()) foreach (var item in ParseTree(child.Value, depth + 1)) yield return item;
    }
    public void Query(JsonElement tree)
    {
        lock (gate)
            foreach (var item in ParseTree(tree).Take(8192))
            {
                if (!rows.ContainsKey(item.Address) && rows.Count >= 8192) break;
                var row = rows.GetValueOrDefault(item.Address) ?? new(item.Address, item.Type, "", "—", "—", "—", "");
                rows[item.Address] = row with { Type = item.Type, Access = item.Access switch { 1 => "Lesen", 2 => "Schreiben", 3 => "Lesen / Schreiben", _ => "unbekannt" }, QueryValue = item.Value };
            }
    }
    public static string? FindAvatar(JsonElement node, int depth = 0)
    {
        if (depth > 32 || node.ValueKind != JsonValueKind.Object) return null;
        if (node.TryGetProperty("FULL_PATH", out var path) && path.GetString() == "/avatar/change" && node.TryGetProperty("VALUE", out var values) && values.ValueKind == JsonValueKind.Array && values.GetArrayLength() > 0 && values[0].ValueKind == JsonValueKind.String) return values[0].GetString();
        if (node.TryGetProperty("CONTENTS", out var contents) && contents.ValueKind == JsonValueKind.Object)
            foreach (var child in contents.EnumerateObject()) if (FindAvatar(child.Value, depth + 1) is { } id) return id;
        return null;
    }
}
