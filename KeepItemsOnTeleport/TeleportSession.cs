using System.Collections.Generic;

namespace KeepItemsOnTeleport;

/// <summary>
/// Tracks who is mid-teleport and whether it is the inverse teleporter.
/// Cleared when we skip a drop or when the teleport finishes.
/// </summary>
internal static class TeleportSession
{
    private static readonly Dictionary<ulong, bool> InverseByPlayer = new();

    internal static void Mark(ulong playerClientId, bool isInverse)
    {
        InverseByPlayer[playerClientId] = isInverse;
        Plugin.V($"Marked teleport player={playerClientId} inverse={isInverse}");
    }

    internal static bool TryGet(ulong playerClientId, out bool isInverse)
        => InverseByPlayer.TryGetValue(playerClientId, out isInverse);

    internal static void Clear(ulong playerClientId)
    {
        if (InverseByPlayer.Remove(playerClientId))
            Plugin.V($"Cleared teleport mark player={playerClientId}");
    }
}
