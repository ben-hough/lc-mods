using GameNetcodeStuff;
using HarmonyLib;

namespace WeedKillerRefill;

[HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.SetControlTipsForItem))]
internal static class GrabbableObjectControlTipsPatch
{
    [HarmonyPostfix]
    private static void Postfix(GrabbableObject __instance)
    {
        if (!Plugin.ShowControlTip.Value || !Plugin.Enabled.Value)
            return;

        if (__instance is not SprayPaintItem spray || !spray.isWeedKillerSprayBottle)
            return;

        if (!WeedKillerUtil.IsHeldByLocalPlayer(spray))
            return;

        if (!WeedKillerUtil.IsEmpty(spray))
            return;

        var hud = HUDManager.Instance;
        if (hud == null)
            return;

        // UI shows RefillKey only (default R). Q-shake path kept but not advertised.
        if (!Plugin.AllowKeyRefill.Value)
            return;
        var r = InputUtil.TipLabel(Plugin.RefillKey.Value);
        hud.ChangeControlTip(2, $"Refill : [{r}]");
    }
}

/// <summary>
/// Vanilla skips shake for weed killer. BetterSprayPaint models shake restore on paint cans;
/// we use the same interact to refill the weed killer tank.
/// </summary>
[HarmonyPatch(typeof(SprayPaintItem), nameof(SprayPaintItem.ItemInteractLeftRight))]
internal static class SprayPaintShakeRefillPatch
{
    [HarmonyPostfix]
    private static void Postfix(SprayPaintItem __instance, bool right)
    {
        if (!Plugin.Enabled.Value || right)
            return;

        if (!__instance.isWeedKillerSprayBottle)
            return;

        if (!WeedKillerUtil.IsHeldByLocalPlayer(__instance))
            return;

        if (__instance.isSpraying)
            return;

        if (Plugin.OnlyWhenEmpty.Value && !WeedKillerUtil.IsEmpty(__instance))
        {
            // Still top up shake meter so a half-used can sprays again after release quirks.
            __instance.sprayCanShakeMeter = 1f;
            return;
        }

        WeedKillerUtil.Refill(__instance, "Shake.Q");
        WeedKillerUtil.PlayShakeFeedback(__instance);
    }
}

/// <summary>Optional R (or config key) path — same LateUpdate as before.</summary>
[HarmonyPatch(typeof(SprayPaintItem), nameof(SprayPaintItem.LateUpdate))]
internal static class SprayPaintLateUpdatePatch
{
    [HarmonyPostfix]
    private static void Postfix(SprayPaintItem __instance)
    {
        if (!Plugin.Enabled.Value || !Plugin.AllowKeyRefill.Value)
            return;

        if (!__instance.isWeedKillerSprayBottle)
            return;

        if (!WeedKillerUtil.IsHeldByLocalPlayer(__instance))
            return;

        RefillController.TickHeld(__instance, "Spray.LateUpdate");
    }
}

[HarmonyPatch(typeof(PlayerControllerB), "LateUpdate")]
internal static class PlayerLateUpdateRefillPatch
{
    [HarmonyPostfix]
    private static void Postfix(PlayerControllerB __instance)
    {
        if (!Plugin.AllowKeyRefill.Value)
            return;

        if (__instance != GameNetworkManager.Instance?.localPlayerController)
            return;

        RefillController.Tick("Player.LateUpdate");
    }
}

[HarmonyPatch(typeof(StartOfRound), "Start")]
internal static class StartOfRoundEnsurePatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        WeedKillerRefillBehaviour.EnsureExists();
        Plugin.Log.LogInfo("[StartOfRound.Start] refill behaviour ensured");
    }
}

[HarmonyPatch(typeof(HUDManager), "Start")]
internal static class HudManagerEnsurePatch
{
    [HarmonyPostfix]
    private static void Postfix() => WeedKillerRefillBehaviour.EnsureExists();
}

/// <summary>
/// When the ship charger runs ChargeBatteries, keep tank in sync with battery
/// (same idea as WeedKillerAdjuster's Use Battery mode).
/// </summary>
[HarmonyPatch(typeof(GrabbableObject), nameof(GrabbableObject.ChargeBatteries))]
internal static class ChargeBatteriesTankSyncPatch
{
    [HarmonyPostfix]
    private static void Postfix(GrabbableObject __instance)
    {
        if (__instance is not SprayPaintItem spray || !spray.isWeedKillerSprayBottle)
            return;

        try
        {
            var bat = spray.insertedBattery;
            if (bat == null)
                return;

            // If something charged the battery, mirror into tank.
            if (bat.charge > spray.sprayCanTank)
            {
                spray.sprayCanTank = bat.charge;
                spray.sprayCanShakeMeter = 1f;
                spray.tryingToUseEmptyCan = bat.charge <= Plugin.EmptyThreshold.Value;
                Plugin.V($"[ChargeBatteries] synced tank={spray.sprayCanTank:0.###} from battery");
            }
        }
        catch
        {
            // ignored
        }
    }
}
