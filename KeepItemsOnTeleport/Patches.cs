using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;

namespace KeepItemsOnTeleport;

[HarmonyPatch(typeof(ShipTeleporter))]
internal static class ShipTeleporterPatches
{
    /// <summary>Normal teleporter: beam targeted radar player up to the ship.</summary>
    [HarmonyPatch(nameof(ShipTeleporter.PressTeleportButtonClientRpc))]
    [HarmonyPostfix]
    private static void PressTeleportButtonClientRpcPostfix(ShipTeleporter __instance)
    {
        if (__instance == null || __instance.isInverseTeleporter)
            return;

        var player = StartOfRound.Instance?.mapScreen?.targetedPlayer;
        if (player == null)
            return;

        TeleportSession.Mark(player.playerClientId, isInverse: false);
    }

    /// <summary>Inverse teleporter: beam a player out into the facility.</summary>
    [HarmonyPatch(nameof(ShipTeleporter.TeleportPlayerOutWithInverseTeleporter))]
    [HarmonyPrefix]
    private static void InverseTeleportPrefix(int playerObj)
    {
        var start = StartOfRound.Instance;
        if (start == null || playerObj < 0 || playerObj >= start.allPlayerScripts.Length)
            return;

        var player = start.allPlayerScripts[playerObj];
        if (player == null)
            return;

        TeleportSession.Mark(player.playerClientId, isInverse: true);
    }
}

[HarmonyPatch(typeof(PlayerControllerB))]
internal static class DropHeldItemsPatches
{
    [HarmonyPatch(nameof(PlayerControllerB.DropAllHeldItems))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static bool DropAllHeldItemsPrefix(PlayerControllerB __instance, bool disconnecting)
    {
        return !ShouldKeepItems(__instance, disconnecting, "DropAllHeldItems");
    }

    [HarmonyPatch(nameof(PlayerControllerB.DropAllHeldItemsAndSync))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static bool DropAllHeldItemsAndSyncPrefix(PlayerControllerB __instance)
    {
        return !ShouldKeepItems(__instance, disconnecting: false, "DropAllHeldItemsAndSync");
    }

    [HarmonyPatch(nameof(PlayerControllerB.DropAllHeldItemsAndSyncNonexact))]
    [HarmonyPrefix]
    [HarmonyPriority(Priority.Last)]
    private static bool DropAllHeldItemsAndSyncNonexactPrefix(PlayerControllerB __instance)
    {
        return !ShouldKeepItems(__instance, disconnecting: false, "DropAllHeldItemsAndSyncNonexact");
    }

    private static bool ShouldKeepItems(PlayerControllerB player, bool disconnecting, string source)
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value)
            return false;

        if (player == null)
            return false;

        // Death / disconnect should still dump inventory.
        if (disconnecting || player.isPlayerDead)
            return false;

        bool isInverse;
        var marked = TeleportSession.TryGet(player.playerClientId, out isInverse);

        // Fallback: vanilla sets this around ship teleports.
        if (!marked && player.teleportedLastFrame)
        {
            // Unknown which pad — honor either toggle if both on, else keep when any keep is enabled.
            isInverse = GuessInverseFromPlayer(player);
            marked = true;
        }

        if (!marked)
            return false;

        var keep = isInverse
            ? Plugin.KeepOnInverseTeleporter.Value
            : Plugin.KeepOnNormalTeleporter.Value;

        if (!keep)
        {
            TeleportSession.Clear(player.playerClientId);
            return false;
        }

        TeleportSession.Clear(player.playerClientId);
        Plugin.V($"Kept items on teleport ({source}) player={player.playerClientId} inverse={isInverse}");
        return true;
    }

    private static bool GuessInverseFromPlayer(PlayerControllerB player)
    {
        // Best-effort: if they are leaving the ship hangar area, treat as inverse.
        try
        {
            if (player.isInHangarShipRoom)
                return true;
        }
        catch
        {
            // ignored
        }

        return false;
    }
}
