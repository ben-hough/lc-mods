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

    // SendTo.NotMe replicas. The owner already dropped (or kept, by skipping AndSync
    // so this RPC is never sent). Observers must apply a replica they actually receive.
    [HarmonyPatch(nameof(PlayerControllerB.DropAllHeldItemsRpc))]
    [HarmonyPrefix]
    private static void DropAllHeldItemsRpcPrefix()
    {
        TeleportSession.ApplyingRemoteDrop = true;
    }

    [HarmonyPatch(nameof(PlayerControllerB.DropAllHeldItemsRpc))]
    [HarmonyPostfix]
    private static void DropAllHeldItemsRpcPostfix()
    {
        TeleportSession.ApplyingRemoteDrop = false;
    }

    [HarmonyPatch(nameof(PlayerControllerB.DropAllHeldItemsNonexactRpc))]
    [HarmonyPrefix]
    private static void DropAllHeldItemsNonexactRpcPrefix()
    {
        TeleportSession.ApplyingRemoteDrop = true;
    }

    [HarmonyPatch(nameof(PlayerControllerB.DropAllHeldItemsNonexactRpc))]
    [HarmonyPostfix]
    private static void DropAllHeldItemsNonexactRpcPostfix()
    {
        TeleportSession.ApplyingRemoteDrop = false;
    }

    // Marks are only consumed by the client that intercepted a drop. Other clients
    // still have the button-press mark after a kept ship beam. Clear it once the
    // teleport actually moves the player so the next real drop (centipede, death, wolf) lands.
    [HarmonyPatch(nameof(PlayerControllerB.TeleportPlayer))]
    [HarmonyPostfix]
    private static void TeleportPlayerPostfix(PlayerControllerB __instance)
    {
        if (__instance != null)
            TeleportSession.Clear(__instance.playerClientId);
    }

    private static bool ShouldKeepItems(PlayerControllerB player, bool disconnecting, string source)
    {
        // Replica of a drop the owner already performed. Skipping it leaves the item
        // parented to serverItemHolder with isHeld still set, so teammates keep seeing it.
        if (TeleportSession.ApplyingRemoteDrop)
            return false;

        if (!HostModGate.FeaturesActive)
            return false;

        if (player == null)
            return false;

        // Death / disconnect should still dump inventory.
        if (disconnecting || player.isPlayerDead)
            return false;

        bool isInverse;
        var marked = TeleportSession.TryGet(player.playerClientId, out isInverse);

        if (!marked)
            return false;

        var keep = isInverse
            ? HostModGate.KeepOnInverse
            : HostModGate.KeepOnNormal;

        if (!keep)
        {
            TeleportSession.Clear(player.playerClientId);
            return false;
        }

        TeleportSession.Clear(player.playerClientId);
        Plugin.V($"Kept items on teleport ({source}) player={player.playerClientId} inverse={isInverse}");
        return true;
    }

}

[HarmonyPatch(typeof(StartOfRound), "Start")]
internal static class HostModGateStartPatch
{
    private static void Postfix()
    {
        HostModGate.EnsureRegistered();
    }
}
