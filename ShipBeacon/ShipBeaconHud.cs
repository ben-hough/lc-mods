using System;
using UnityEngine;

namespace ShipBeacon;

/// <summary>
/// Outdoor HUD pointing toward the ship. Uses ASCII arrows (Unicode often fails in IMGUI fonts).
/// </summary>
internal sealed class ShipBeaconHud : MonoBehaviour
{
    private GUIStyle? _style;
    private Texture2D? _bg;
    private float _nextDebugLog;
    private string _lastHideReason = "";

    private void EnsureStyles()
    {
        if (_style != null)
            return;

        _bg = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        _bg.SetPixel(0, 0, new Color(0f, 0f, 0f, 0.55f));
        _bg.Apply();

        _style = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = Mathf.RoundToInt(20f * Plugin.HudScale.Value),
            fontStyle = FontStyle.Bold,
            normal = { textColor = new Color(0.9f, 0.95f, 1f, 1f), background = _bg },
            padding = new RectOffset(14, 14, 8, 8),
        };
    }

    private void OnGUI()
    {
        if (Event.current.type != EventType.Repaint)
            return;

        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return;

        if (!TryGetBeacon(out var angleDeg, out var distance, out var reason))
        {
            MaybeLogHidden(reason);
            return;
        }

        _lastHideReason = "";
        EnsureStyles();
        if (_style == null)
            return;

        _style.fontSize = Mathf.RoundToInt(20f * Plugin.HudScale.Value);

        var arrow = AngleToArrow(angleDeg);
        var label = Plugin.ShowDistance.Value
            ? $"{arrow}  SHIP  {distance:0}m  {arrow}"
            : $"{arrow}  SHIP  {arrow}";

        var size = _style.CalcSize(new GUIContent(label));
        var x = (Screen.width - size.x) * 0.5f;
        var y = Screen.height * Mathf.Clamp01(Plugin.VerticalOffset.Value);

        GUI.depth = -1000;
        GUI.Label(new Rect(x, y, size.x, size.y), label, _style);
    }

    private void MaybeLogHidden(string reason)
    {
        if (reason == _lastHideReason && Time.unscaledTime < _nextDebugLog)
            return;

        _lastHideReason = reason;
        _nextDebugLog = Time.unscaledTime + 8f;
        if (!string.IsNullOrEmpty(reason))
            Plugin.Log.LogInfo($"ShipBeacon hidden: {reason}");
    }

    private static bool TryGetBeacon(out float signedAngleDeg, out float distance, out string hideReason)
    {
        signedAngleDeg = 0f;
        distance = 0f;
        hideReason = "";

        try
        {
            var start = StartOfRound.Instance;
            if (start == null)
            {
                hideReason = "StartOfRound missing";
                return false;
            }

            // Only hide in orbit / company waiting room phase.
            if (start.inShipPhase)
            {
                hideReason = "inShipPhase (orbit)";
                return false;
            }

            var player = GameNetworkManager.Instance?.localPlayerController;
            if (player == null || player.isPlayerDead)
            {
                hideReason = "no local player / dead";
                return false;
            }

            // Outdoor only.
            if (player.isInsideFactory)
            {
                hideReason = "inside factory";
                return false;
            }

            if (player.isInHangarShipRoom)
            {
                hideReason = "inside ship";
                return false;
            }

            Transform? ship = null;
            if (start.elevatorTransform != null)
                ship = start.elevatorTransform;
            else if (start.shipBounds != null)
                ship = start.shipBounds.transform;
            else if (start.shipLandingPosition != null)
                ship = start.shipLandingPosition;

            if (ship == null)
            {
                hideReason = "no ship transform";
                return false;
            }

            var cam = player.gameplayCamera != null
                ? player.gameplayCamera.transform
                : player.transform;

            var from = cam.position;
            var to = ship.position;
            var flat = to - from;
            flat.y = 0f;
            distance = flat.magnitude;
            if (distance < 1f)
            {
                hideReason = $"too close ({distance:0.0}m)";
                return false;
            }

            var forward = cam.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f)
            {
                hideReason = "bad camera forward";
                return false;
            }

            signedAngleDeg = Vector3.SignedAngle(forward.normalized, flat.normalized, Vector3.up);
            return true;
        }
        catch (Exception ex)
        {
            hideReason = ex.Message;
            return false;
        }
    }

    /// <summary>ASCII-only arrows — Unity IMGUI default font often cannot draw Unicode arrows.</summary>
    private static string AngleToArrow(float signedAngleDeg)
    {
        var a = signedAngleDeg;
        if (a < 0f)
            a += 360f;

        var sector = Mathf.RoundToInt(a / 45f) % 8;
        return sector switch
        {
            0 => "^",
            1 => "/^",
            2 => ">>",
            3 => "\\v",
            4 => "v",
            5 => "v/",
            6 => "<<",
            7 => "^\\",
            _ => "*",
        };
    }
}
