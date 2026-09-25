using HarmonyLib;

namespace SprayPatterns;

[HarmonyPatch(typeof(SprayPaintItem))]
internal static class SprayPaintPatches
{
    /// <summary>
    /// When a pattern (non-Vanilla) is selected, LMB stamps that pattern once instead of free-paint.
    /// Weed killer and Vanilla selection leave vanilla ItemActivate alone.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(nameof(SprayPaintItem.ItemActivate))]
    private static bool ItemActivatePrefix(SprayPaintItem __instance, bool used, bool buttonDown)
    {
        if (!Plugin.Enabled.Value)
            return true;
        if (__instance.isWeedKillerSprayBottle)
            return true;
        if (!__instance.IsOwner)
            return true;
        if (!PatternCatalog.IsPatternStamp(PatternCatalog.Selected))
            return true;

        var controller = SprayPatternController.Instance;
        if (controller == null)
            return true;

        // Block spray while radial is open (selection click uses LMB).
        if (controller.IsRadialOpen)
            return false;

        if (buttonDown)
        {
            controller.TryApplySelectedStamp(__instance);
            return false; // skip StartSpraying / continuous free-paint
        }

        // Button up: nothing to stop if we never started continuous spray.
        return false;
    }

    /// <summary>Safety net: never free-paint while a pattern stamp mode is selected.</summary>
    [HarmonyPrefix]
    [HarmonyPatch(nameof(SprayPaintItem.TrySpraying))]
    private static bool TrySprayingPrefix(SprayPaintItem __instance, ref bool __result)
    {
        if (!Plugin.Enabled.Value)
            return true;
        if (__instance.isWeedKillerSprayBottle)
            return true;
        if (!PatternCatalog.IsPatternStamp(PatternCatalog.Selected))
            return true;

        __result = false;
        return false;
    }
}
