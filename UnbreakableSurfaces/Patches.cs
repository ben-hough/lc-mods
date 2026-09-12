using System;
using System.Reflection;
using HarmonyLib;

namespace UnbreakableSurfaces;

[HarmonyPatch(typeof(BridgeTrigger))]
internal static class BridgePatches
{
    private static readonly FieldInfo? HasFallenField =
        AccessTools.Field(typeof(BridgeTrigger), "hasBridgeFallen");

    private static AccessTools.FieldRef<BridgeTrigger, bool>? _hasFallenRef;
    private static bool _loggedFieldMiss;

    static BridgePatches()
    {
        if (HasFallenField == null)
            return;
        try
        {
            _hasFallenRef = AccessTools.FieldRefAccess<BridgeTrigger, bool>(HasFallenField);
        }
        catch (Exception ex)
        {
            Plugin.Log?.LogWarning($"FieldRef for hasBridgeFallen failed, will use SetValue: {ex.Message}");
        }
    }

    [HarmonyPatch(nameof(BridgeTrigger.BridgeFallServerRpc))]
    [HarmonyPrefix]
    private static bool BlockFallServer()
        => !(Plugin.Enabled.Value && Plugin.Bridges.Value);

    [HarmonyPatch(nameof(BridgeTrigger.BridgeFallClientRpc))]
    [HarmonyPrefix]
    private static bool BlockFallClient()
        => !(Plugin.Enabled.Value && Plugin.Bridges.Value);

    [HarmonyPatch("LateUpdate")]
    [HarmonyPostfix]
    private static void KeepStanding(BridgeTrigger __instance)
    {
        if (!Plugin.Enabled.Value || !Plugin.Bridges.Value || __instance == null)
            return;

        if (__instance.bridgeDurability < 1f)
            __instance.bridgeDurability = 1f;

        try
        {
            if (_hasFallenRef != null)
                _hasFallenRef(__instance) = false;
            else if (HasFallenField != null)
                HasFallenField.SetValue(__instance, false);
            else if (!_loggedFieldMiss)
            {
                _loggedFieldMiss = true;
                Plugin.Log.LogWarning("BridgeTrigger.hasBridgeFallen field not found; fall RPCs are still blocked.");
            }
        }
        catch (Exception ex)
        {
            if (!_loggedFieldMiss)
            {
                _loggedFieldMiss = true;
                Plugin.Log.LogError($"Failed to clear hasBridgeFallen: {ex}");
            }
        }
    }
}

[HarmonyPatch(typeof(BridgeTriggerType2))]
internal static class SurfacePatches
{
    private static readonly FieldInfo? TimesTriggeredField =
        AccessTools.Field(typeof(BridgeTriggerType2), "timesTriggered");
    private static readonly FieldInfo? BridgeFellField =
        AccessTools.Field(typeof(BridgeTriggerType2), "bridgeFell");

    [HarmonyPatch(nameof(BridgeTriggerType2.AddToBridgeInstabilityServerRpc))]
    [HarmonyPrefix]
    private static bool BlockInstability()
        => !(Plugin.Enabled.Value && Plugin.BreakableSurfaces.Value);

    [HarmonyPatch(nameof(BridgeTriggerType2.OnTriggerEnter))]
    [HarmonyPostfix]
    private static void ResetInstability(BridgeTriggerType2 __instance)
    {
        if (!Plugin.Enabled.Value || !Plugin.BreakableSurfaces.Value || __instance == null)
            return;

        try
        {
            if (TimesTriggeredField != null)
                TimesTriggeredField.SetValue(__instance, 0);
            else
                __instance.timesTriggered = 0;

            if (BridgeFellField != null)
                BridgeFellField.SetValue(__instance, false);
            else
                __instance.bridgeFell = false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Failed to reset BridgeTriggerType2: {ex.Message}");
        }
    }
}
