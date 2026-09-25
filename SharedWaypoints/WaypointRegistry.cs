using System.Collections.Generic;
using System.Linq;

namespace SharedWaypoints;

internal static class WaypointRegistry
{
    // One outdoor + one indoor pin per player.
    private static readonly Dictionary<(ulong Owner, bool Inside), WaypointState> BySlot = new();

    public static int Count => BySlot.Count;

    public static void Set(WaypointState state)
        => BySlot[(state.OwnerPlayerClientId, state.IsInside)] = state;

    public static void Clear(ulong ownerPlayerClientId, bool isInside)
        => BySlot.Remove((ownerPlayerClientId, isInside));

    public static void ClearOwner(ulong ownerPlayerClientId)
    {
        BySlot.Remove((ownerPlayerClientId, false));
        BySlot.Remove((ownerPlayerClientId, true));
    }

    public static bool TryGet(ulong ownerPlayerClientId, bool isInside, out WaypointState state)
        => BySlot.TryGetValue((ownerPlayerClientId, isInside), out state!);

    public static bool HasAny(ulong ownerPlayerClientId)
        => BySlot.ContainsKey((ownerPlayerClientId, false))
           || BySlot.ContainsKey((ownerPlayerClientId, true));

    public static void ClearAll() => BySlot.Clear();

    public static IEnumerable<WaypointState> Snapshot() => BySlot.Values.ToList();
}
