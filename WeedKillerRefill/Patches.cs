using HarmonyLib;

namespace WeedKillerRefill;

[HarmonyPatch(typeof(SprayPaintItem))]
internal static class SprayPaintItemPatches
{
    /// <summary>
    /// Add a vanilla top-right tip: "Refill : [R]" when holding an empty weed killer.
    /// Tip indices: 0 Drop, 1 Spray — use 2 for refill.
    /// </summary>
    [HarmonyPostfix]
    [HarmonyPatch(nameof(SprayPaintItem.SetControlTipsForItem))]
    private static void SetControlTipsForItem_Postfix(SprayPaintItem __instance)
    {
        if (!Plugin.ShowControlTip.Value || !Plugin.Enabled.Value)
            return;

        if (!__instance.isWeedKillerSprayBottle)
            return;

        // Only for the item the local player is holding.
        var player = GameNetworkManager.Instance?.localPlayerController;
        if (player == null)
            return;

        var held = player.currentlyHeldObject ?? player.currentlyHeldObjectServer;
        if (held != __instance)
            return;

        if (!WeedKillerUtil.IsEmpty(__instance))
            return;

        var hud = HUDManager.Instance;
        if (hud == null)
            return;

        var key = InputUtil.TipLabel(Plugin.RefillKey.Value);
        hud.ChangeControlTip(2, $"Refill : [{key}]");
    }
}
