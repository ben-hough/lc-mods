using HarmonyLib;
using Unity.Netcode;

namespace ShipStarterKit;

/// <summary>
/// Spawn hook: after StartOfRound.Start on the server (same place vanilla loads
/// ship grabbables). Does not run again between moons because StartOfRound persists.
/// </summary>
[HarmonyPatch(typeof(StartOfRound), "Start")]
internal static class StartOfRoundStartSupplyPatch
{
    public static void Postfix(StartOfRound __instance)
    {
        HostModGate.EnsureRegistered();

        var nm = NetworkManager.Singleton;
        if (nm == null || !(nm.IsServer || nm.IsHost))
            return;

        // Vanilla Start already called LoadShipGrabbableItems on server before this Postfix.
        StarterKitSpawner.TrySupplyOnShipStart();
    }
}

/// <summary>
/// New game on a save slot clears our ES3 flag so the fresh file gets a starter kit.
/// </summary>
[HarmonyPatch(typeof(GameNetworkManager), nameof(GameNetworkManager.ResetSavedGameValues))]
internal static class ResetSavedGameValuesClearGatePatch
{
    public static void Postfix(GameNetworkManager __instance)
    {
        if (__instance == null || string.IsNullOrEmpty(__instance.currentSaveFileName))
            return;
        Es3SaveGate.ClearSupplied(__instance.currentSaveFileName);
        StarterKitSpawner.ResetSession();
        Plugin.V($"Cleared ShipStarterKit supply flag after ResetSavedGameValues on '{__instance.currentSaveFileName}'.");
    }
}

[HarmonyPatch(typeof(GameNetworkManager), "Disconnect")]
internal static class DisconnectResetSessionPatch
{
    public static void Prefix()
    {
        StarterKitSpawner.ResetSession();
    }
}
