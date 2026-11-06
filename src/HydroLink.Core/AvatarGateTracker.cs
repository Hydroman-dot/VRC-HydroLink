namespace HydroLink.Core;

// Query values may reconcile an already armed session, never arm hardware or replay contacts.
public sealed class AvatarGateTracker
{
    private readonly Dictionary<string, Entry> entries = [];
    private sealed class Entry(string avatar, DateTimeOffset after)
    {
        public string Avatar = avatar;
        public DateTimeOffset After = after, LastQuery = DateTimeOffset.MinValue, LastLive = DateTimeOffset.MinValue;
        public bool? LiveValue;
        public int EnableConfirmations;
    }
    public void Begin(string key, string avatar, DateTimeOffset after) => entries[key] = new(avatar, after);
    public void Remove(string key) => entries.Remove(key);
    public void Clear() => entries.Clear();
    public void Live(string key, bool enabled, DateTimeOffset at)
    {
        if (!entries.TryGetValue(key, out var entry)) return;
        entry.LastLive = at; entry.LiveValue = enabled; entry.EnableConfirmations = 0;
    }
    public bool Accept(string key, string avatar, DateTimeOffset started, bool enabled)
    {
        if (!entries.TryGetValue(key, out var entry) || entry.Avatar != avatar || started < entry.After || started <= entry.LastQuery || started <= entry.LastLive) return false;
        entry.LastQuery = started;
        if (enabled && entry.LiveValue == false)
        {
            // A late HTTP reply must not undo a just-received OFF. After a grace period,
            // two distinct fresh queries can recover a subsequently missed ON datagram.
            if (started < entry.LastLive.AddSeconds(3)) return false;
            if (++entry.EnableConfirmations < 2) return false;
        }
        else entry.EnableConfirmations = 0;
        return true;
    }
}
