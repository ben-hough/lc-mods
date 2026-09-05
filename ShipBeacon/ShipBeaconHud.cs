using System;
using UnityEngine;

namespace ShipBeacon;

/// <summary>
/// Outdoor-only HUD that points toward the ship using a simple on-screen beacon.
/// </summary>
internal sealed class ShipBeaconHud : MonoBehaviour
{
    private GUIStyle? _style;
    private Texture2D? _bg;

    private void EnsureStyles()
    {
        if (_style != null)
            return;

        _bg = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.45f));
        _bg.Apply();

        _style = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(22f * Plugin.HudScale.Value),
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.85f, 0.95f, 1f, 0.95f), background = _bg },
            padding = new RectOffset(12, 12, 6, 6),
        };
    }

    private void OnGUI()
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return;

        if (!TryGetBeacon(out var angleDeg, out var distance))
            return;

        EnsureStyles();
        if (_style == null)
            return;

        _style.fontSize = Mathf.RoundToInt(22f * Plugin.HudScale.Value);

        var arrow = AngleToArrow(angleDeg);
        var label = Plugin.ShowDistance.Value
            ? $"{arrow}  SHIP  {distance:0}m  {arrow}"
            : $"{arrow}  SHIP  {arrow}";

        var size = _style.CalcSize(new GUIContent(label));
        var x = (Screen.width - size.x) * 0.5f;
        var y = Screen.height * Mathf.Clamp01(Plugin.VerticalOffset.Value);

        GUI.Label(new Rect(x, y, size.x, size.y), label, _style);
    }

    private static bool TryGetBeacon(out float signedAngleDeg, out float distance)
    {
        signedAngleDeg = 0f;
        distance = 0f;

        try
        {
            var start = StartOfRound.Instance;
            if (start == null || !start.shipDoorsEnabled || start.inShipPhase)
                return false;

            var player = GameNetworkManager.Instance?.localPlayerController;
            if (player == null || player.isPlayerDead)
                return false;

            // Outdoor only: not inside facility, not in hangar/ship room.
            if (player.isInsideFactory || player.isInHangarShipRoom)
                return false;

            Transform? ship = null;
            if (start.elevatorTransform != null)
                ship = start.elevatorTransform;
            else if (start.shipBounds != null)
                ship = start.shipBounds.transform;

            if (ship == null)
                return false;

            var cam = player.gameplayCamera != null
                ? player.gameplayCamera.transform
                : player.transform;

            var from = cam.position;
            var to = ship.position;
            var flat = to - from;
            flat.y = 0f;
            distance = flat.magnitude;
            if (distance < 0.5f)
                return false;

            var forward = cam.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
                return false;

            signedAngleDeg = Vector3.SignedAngle(forward.normalized, flat.normalized, Vector3.up);
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogDebug($"ShipBeacon HUD skipped: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Map camera-relative bearing (-180..180) to a coarse arrow glyph.
    /// 0° = ship ahead, positive = ship to the right.
    /// </summary>
    private static string AngleToArrow(float signedAngleDeg)
    {
        var a = signedAngleDeg;
        if (a < 0f)
            a += 360f;

        var sector = Mathf.RoundToInt(a / 45f) % 8;
        return sector switch
        {
            0 => "↑",
            1 => "↗",
            2 => "→",
            3 => "↘",
            4 => "↓",
            5 => "↙",
            6 => "←",
            7 => "↖",
            _ => "•",
        };
    }
}
