using System;
using UnityEngine;

namespace WeedKillerRefill;

/// <summary>
/// While holding an empty weed killer bottle, press the configured key to refill the tank.
/// </summary>
internal sealed class WeedKillerRefillBehaviour : MonoBehaviour
{
    private GUIStyle? _hintStyle;
    private float _hintUntil;

    private void Update()
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return;

        if (!TryGetHeldWeedKiller(out var spray))
            return;

        var empty = spray.sprayCanTank <= Plugin.EmptyThreshold.Value;
        if (Plugin.ShowHint.Value && empty)
            _hintUntil = Time.unscaledTime + 0.25f;

        if (!Input.GetKeyDown(Plugin.RefillKey.Value))
            return;

        if (Plugin.OnlyWhenEmpty.Value && !empty)
            return;

        try
        {
            spray.sprayCanTank = 1f;
            spray.tryingToUseEmptyCan = false;
            spray.sprayCanShakeMeter = 0f;

            Plugin.Log.LogInfo("Refilled weed killer tank.");
            _hintUntil = Time.unscaledTime + 1.25f;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to refill weed killer: {ex.Message}");
        }
    }

    private void OnGUI()
    {
        if (!Plugin.ShowHint.Value || Time.unscaledTime > _hintUntil)
            return;

        if (!TryGetHeldWeedKiller(out var spray))
            return;

        var empty = spray.sprayCanTank <= Plugin.EmptyThreshold.Value;
        if (!empty && Time.unscaledTime > _hintUntil - 1f)
            return;

        _hintStyle ??= new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            padding = new RectOffset(10, 10, 6, 6),
        };

        var key = Plugin.RefillKey.Value.ToString();
        var msg = empty
            ? $"Weed killer empty — press {key} to refill"
            : $"Weed killer refilled ({key})";

        var size = _hintStyle.CalcSize(new GUIContent(msg));
        var rect = new Rect(
            (Screen.width - size.x) * 0.5f,
            Screen.height * 0.72f,
            size.x,
            size.y);
        GUI.Label(rect, msg, _hintStyle);
    }

    private static bool TryGetHeldWeedKiller(out SprayPaintItem spray)
    {
        spray = null!;
        try
        {
            var player = GameNetworkManager.Instance?.localPlayerController;
            if (player == null || player.isPlayerDead)
                return false;

            var held = player.currentlyHeldObjectServer;
            if (held is not SprayPaintItem item)
                return false;

            if (!item.isWeedKillerSprayBottle)
                return false;

            spray = item;
            return true;
        }
        catch
        {
            return false;
        }
    }
}
