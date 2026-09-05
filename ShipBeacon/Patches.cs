using HarmonyLib;

namespace ShipBeacon;

/// <summary>Recreate HUD after scene loads so it survives moon transitions.</summary>
[HarmonyPatch(typeof(StartOfRound), "Start")]
internal static class StartOfRoundStartPatch
{
    private static void Postfix()
    {
        ShipBeaconHud.EnsureExists();
        Plugin.Log.LogInfo("ShipBeacon HUD ensured after StartOfRound.Start.");
    }
}

[HarmonyPatch(typeof(HUDManager), "Start")]
internal static class HudManagerStartPatch
{
    private static void Postfix()
    {
        ShipBeaconHud.EnsureExists();
    }
}
