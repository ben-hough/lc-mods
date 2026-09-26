using HarmonyLib;

namespace SharedWaypoints;

[HarmonyPatch(typeof(StartOfRound), "Start")]
internal static class StartOfRoundStartPatch
{
    private static void Postfix(StartOfRound __instance)
    {
        WaypointHud.EnsureExists();
        // Ship/orbit phase or fresh load — drop stale pins.
        if (__instance != null && __instance.inShipPhase)
        {
            WaypointRegistry.ClearAll();
            WaypointMapMarkers.DestroyAll();
        }
        Plugin.Log.LogInfo("SharedWaypoints HUD ensured after StartOfRound.Start.");
    }
}

[HarmonyPatch(typeof(HUDManager), "Start")]
internal static class HudManagerStartPatch
{
    private static void Postfix()
    {
        WaypointHud.EnsureExists();
    }
}

[HarmonyPatch(typeof(StartOfRound), "ShipLeave")]
internal static class ShipLeavePatch
{
    private static void Prefix()
    {
        WaypointRegistry.ClearAll();
        WaypointMapMarkers.DestroyAll();
        Plugin.Log.LogInfo("Cleared waypoints on ShipLeave.");
    }
}

[HarmonyPatch(typeof(GameNetworkManager), "Disconnect")]
internal static class DisconnectPatch
{
    private static void Prefix()
    {
        WaypointRegistry.ClearAll();
        WaypointMapMarkers.DestroyAll();
        Plugin.Log.LogInfo("Cleared waypoints on disconnect.");
    }
}
