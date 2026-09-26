using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace MapClickCodes;

/// <summary>
/// Hook the vanilla main-monitor radar switch path. If the interact UV hits a code
/// marker, activate that code alongside the radar target advance (never suppress, 1.0.7+).
/// </summary>
[HarmonyPatch(typeof(ManualCameraRenderer), nameof(ManualCameraRenderer.SwitchRadarTargetForward))]
internal static class SwitchRadarTargetForwardPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ManualCameraRenderer __instance)
    {
        // Only the main ship mapScreen (not other ManualCameraRenderer instances).
        var map = StartOfRound.Instance != null ? StartOfRound.Instance.mapScreen : null;
        if (map == null || !ReferenceEquals(__instance, map))
            return true;

        if (!HostModGate.FeaturesActive)
            return true;

        // 1.0.7: try activate on high-conf mesh-UV hit, but NEVER suppress vanilla radar cycle.
        // False aabb / inflated hitboxes previously ate SwitchRadarTargetForward; activating a
        // code + advancing radar once is acceptable. Always return true.
        try
        {
            if (MapCodeClickController.TryActivateFromCurrentLook(
                    requireInteractPress: false, forceMapContext: true, alwaysLog: true))
            {
                Plugin.V("MapClick: code marker activated alongside SwitchRadarTargetForward (vanilla continues).");
            }
        }
        catch (System.Exception ex)
        {
            Plugin.Log.LogError(
                $"MapClick: SwitchRadarTargetForward activate error (vanilla switch continues): {ex}");
        }

        return true;
    }
}

[HarmonyPatch(typeof(ManualCameraRenderer), nameof(ManualCameraRenderer.SwitchRadarTargetAndSync))]
internal static class SwitchRadarTargetAndSyncPatch
{
    [HarmonyPrefix]
    private static bool Prefix(ManualCameraRenderer __instance)
    {
        // 1.0.7: never suppress radar sync — ClickConsumed is unused for radar.
        return true;
    }
}

/// <summary>
/// When the local player interacts with a trigger while looking near the main map mesh
/// (or a CrewMonitors map-feed panel), attempt click-to-code with forceMapContext
/// alongside vanilla (does not block non-map triggers).
/// </summary>
[HarmonyPatch(typeof(InteractTrigger), nameof(InteractTrigger.Interact))]
internal static class InteractTriggerInteractPatch
{
    [HarmonyPrefix]
    private static void Prefix(InteractTrigger __instance, Transform playerTransform)
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value)
            return;
        if (!HostModGate.FeaturesActive)
            return;
        if (Plugin.ClickConsumedThisFrame)
            return;

        var gnm = GameNetworkManager.Instance;
        var player = gnm != null ? gnm.localPlayerController : null;
        if (player == null || playerTransform == null)
            return;

        // Vanilla passes player.thisPlayerBody, not player.transform — accept either,
        // plus any transform under the local player hierarchy.
        if (!IsLocalPlayerInteractTransform(player, playerTransform))
            return;

        if (!player.isInHangarShipRoom)
            return;

        // Only force map context when the look ray is near the main map or a CrewMonitors map panel.
        if (!MapCodeClickController.IsLookingNearMapScreen(player))
            return;

        MapCodeClickController.TryActivateFromCurrentLook(
            requireInteractPress: false, forceMapContext: true, alwaysLog: true);
        // Do not skip vanilla Interact — non-map triggers and map hover still run.
    }

    /// <summary>
    /// InteractTrigger.Interact is invoked with PlayerControllerB.thisPlayerBody
    /// (not player.transform). Match body, root, or any descendant of the local player.
    /// </summary>
    private static bool IsLocalPlayerInteractTransform(PlayerControllerB player, Transform playerTransform)
    {
        if (ReferenceEquals(playerTransform, player.transform))
            return true;

        var body = player.thisPlayerBody;
        if (body != null && ReferenceEquals(playerTransform, body))
            return true;

        // Any transform under the local player root (or under thisPlayerBody).
        if (playerTransform.IsChildOf(player.transform))
            return true;
        if (body != null && playerTransform.IsChildOf(body))
            return true;

        // Body is sometimes a sibling / alternate root under the same player object tree.
        if (body != null && player.transform.IsChildOf(body) &&
            (ReferenceEquals(playerTransform, body) || playerTransform.IsChildOf(body)))
            return true;

        return false;
    }
}

/// <summary>
/// After vanilla hover tip: when aimed at a map code marker, override cursor icon + tip
/// so the player can tell they are on a clickable code (overrides generic map-switch tip).
/// </summary>
[HarmonyPatch(typeof(PlayerControllerB), "SetHoverTipAndCurrentInteractTrigger")]
internal static class SetHoverTipMapCodePatch
{
    private static Sprite? _cachedShipHoverIcon;
    private static Sprite? _whiteFallbackSprite;
    private static bool _shipHoverSearchDone;

    [HarmonyPostfix]
    private static void Postfix(PlayerControllerB __instance)
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value)
            return;
        if (Plugin.ShowHoverTip != null && !Plugin.ShowHoverTip.Value)
            return;
        if (!HostModGate.FeaturesActive)
            return;

        var gnm = GameNetworkManager.Instance;
        var local = gnm != null ? gnm.localPlayerController : null;
        if (local == null || !ReferenceEquals(__instance, local))
            return;
        if (!__instance.isPlayerControlled || __instance.isPlayerDead)
            return;
        if (!__instance.isInHangarShipRoom)
            return;

        if (!MapCodeClickController.TryPeekMarkerUnderLook(out var matched, out string code))
            return;

        string verb = matched.isBigDoor ? "Open" : "Activate";
        string tip = $"{verb} {code} : [E]";

        if (__instance.cursorTip != null)
            __instance.cursorTip.text = tip;

        if (__instance.cursorIcon == null)
            return;

        Sprite? icon = null;
        if (__instance.hoveringOverTrigger != null && __instance.hoveringOverTrigger.hoverIcon != null)
            icon = __instance.hoveringOverTrigger.hoverIcon;
        else if (__instance.cursorIcon.sprite != null)
            icon = __instance.cursorIcon.sprite;
        else
            icon = GetShipOrWhiteHoverIcon();

        if (icon != null)
            __instance.cursorIcon.sprite = icon;

        __instance.cursorIcon.enabled = true;
    }

    private static Sprite? GetShipOrWhiteHoverIcon()
    {
        if (!_shipHoverSearchDone)
        {
            _shipHoverSearchDone = true;
            try
            {
                var triggers = Object.FindObjectsOfType<InteractTrigger>();
                for (int i = 0; i < triggers.Length; i++)
                {
                    var t = triggers[i];
                    if (t == null || t.hoverIcon == null)
                        continue;
                    // Prefer triggers that look like ship map / monitor interacts.
                    string n = t.gameObject.name ?? "";
                    string tip = t.hoverTip ?? "";
                    if (n.IndexOf("Map", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || tip.IndexOf("Switch", System.StringComparison.OrdinalIgnoreCase) >= 0
                        || tip.IndexOf("monitor", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        _cachedShipHoverIcon = t.hoverIcon;
                        break;
                    }
                    if (_cachedShipHoverIcon == null)
                        _cachedShipHoverIcon = t.hoverIcon;
                }
            }
            catch
            {
                /* FindObjects can fail during teardown */
            }
        }

        if (_cachedShipHoverIcon != null)
            return _cachedShipHoverIcon;

        if (_whiteFallbackSprite == null)
        {
            try
            {
                var tex = Texture2D.whiteTexture;
                _whiteFallbackSprite = Sprite.Create(
                    tex,
                    new Rect(0f, 0f, tex.width, tex.height),
                    new Vector2(0.5f, 0.5f),
                    100f);
            }
            catch
            {
                return null;
            }
        }

        return _whiteFallbackSprite;
    }
}
