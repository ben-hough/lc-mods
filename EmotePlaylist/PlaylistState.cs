using System.Collections.Generic;

namespace EmotePlaylist;

internal sealed class PlaylistState
{
    public ulong OwnerPlayerId;
    public double StartServerTime;
    public int SecondsPerEmote;
    public string[] EmoteNames = System.Array.Empty<string>();

    public int IndexAt(double serverTime)
    {
        if (EmoteNames.Length == 0 || SecondsPerEmote <= 0)
            return 0;
        var elapsed = serverTime - StartServerTime;
        if (elapsed < 0)
            elapsed = 0;
        var idx = (int)(elapsed / SecondsPerEmote);
        var mod = idx % EmoteNames.Length;
        return mod < 0 ? mod + EmoteNames.Length : mod;
    }
}

internal static class PlaylistRegistry
{
    private static readonly Dictionary<ulong, PlaylistState> ByOwner = new();

    public static void Set(PlaylistState state) => ByOwner[state.OwnerPlayerId] = state;

    public static void Clear(ulong ownerPlayerId) => ByOwner.Remove(ownerPlayerId);

    public static bool TryGet(ulong ownerPlayerId, out PlaylistState state)
        => ByOwner.TryGetValue(ownerPlayerId, out state!);

    public static void ClearAll() => ByOwner.Clear();
}
