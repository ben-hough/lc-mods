using HarmonyLib;

namespace WeedKillerRefill;

[HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.SetControlTipsForItem))]
internal static class GrabbableObjectControlTipsPatch
{
    /// <summary>
    /// SetControlTipsForItem is declared on GrabbableObject (virtual), not SprayPaintItem.
    /// Patching SprayPaintItem makes HarmonyX throw and aborts the whole plugin Awake.
    /// </summary>
    [HarmonyPostfix]
    private static void Postfix(GrabbableObject __instance)
    {
        if (!Plugin.ShowControlTip.Value || !Plugin.Enabled.Value)
            return;

        if (__instance is not SprayPaintItem spray || !spray.isWeedKillerSprayBottle)
            return;

        var player = GameNetworkManager.Instance?.localPlayerController;
        if (player == null)
            return;

        var held = player.currentlyHeldObject ?? player.currentlyHeldObjectServer;
        if (held != __instance)
            return;

        if (!WeedKillerUtil.IsEmpty(spray))
            return;

        var hud = HUDManager.Instance;
        if (hud == null)
            return;

        var key = InputUtil.TipLabel(Plugin.RefillKey.Value);
        hud.ChangeControlTip(2, $"Refill : [{key}]");
    }
}
