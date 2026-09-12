using System.Collections.Generic;

namespace SharedWaypoints;

internal static class WaypointRegistry
{
    private static readonly Dictionary<ulong, WaypointState> ByOwner = new();

    public static IReadOnlyDictionary<ulong, WaypointState> All => ByOwner;

    public static void Set(WaypointState state) => ByOwner[state.OwnerPlayerClientId] = state;

    public static void Clear(ulong ownerPlayerClientId) => ByOwner.Remove(ownerPlayerClientId);

    public static bool TryGet(ulong ownerPlayerClientId, out WaypointState state)
        => ByOwner.TryGetValue(ownerPlayerClientId, out state!);

    public static void ClearAll() => ByOwner.Clear();

    public static IEnumerable<WaypointState> Snapshot() => ByOwner.Values;
}
