using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShipBeacon;

/// <summary>
/// Outdoor ship direction HUD using Screen Space Overlay + HUDManager TMP font.
/// </summary>
internal sealed class ShipBeaconHud : MonoBehaviour
{
    private static ShipBeaconHud? _instance;

    private Canvas? _canvas;
    private TextMeshProUGUI? _label;
    private RectTransform? _labelRt;
    private float _nextDebugLog;
    private string _lastHideReason = "";
    private bool _loggedVisible;
    private float _nextHeartbeat;
    private bool _fontAssigned;
    private string _lastBeaconDetail = "";

    // Match clock HUD orange sampled from screenshot (~218,102,47) — less yellow than 1.0.8/9.
    private static readonly Color ClockOrange = new Color(0.855f, 0.400f, 0.185f, 1f);

    internal static void EnsureExists()
    {
        if (_instance != null)
            return;

        var go = new GameObject("ShipBeaconHUD");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<ShipBeaconHud>();
    }

    private void Awake()
    {
        _instance = this;
        BuildUi();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void BuildUi()
    {
        if (_canvas != null)
            return;

        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 5000; // above most LC HUD

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        var textGo = new GameObject("ShipBeaconLabel", typeof(RectTransform));
        textGo.transform.SetParent(transform, false);

        _labelRt = textGo.GetComponent<RectTransform>();
        _labelRt.anchorMin = new Vector2(0.5f, Plugin.VerticalOffset.Value);
        _labelRt.anchorMax = new Vector2(0.5f, Plugin.VerticalOffset.Value);
        // Center pivot — stable at bottom (or wherever VerticalOffset points).
        _labelRt.pivot = new Vector2(0.5f, 0.5f);
        _labelRt.sizeDelta = new Vector2(980f, 56f);
        _labelRt.anchoredPosition = Vector2.zero;

        // No grey backdrop — match bare orange clock HUD style.
        _label = textGo.AddComponent<TextMeshProUGUI>();
        _label.alignment = TextAlignmentOptions.Center;
        _label.fontStyle = FontStyles.Bold;
        _label.color = ClockOrange;
        _label.enableWordWrapping = false;
        _label.raycastTarget = false;
        _label.overflowMode = TextOverflowModes.Overflow;
        _label.text = "";
        // Soft glow like the top clock.
        _label.fontSharedMaterial = null;
        TryAssignFont();
        ApplyClockStyle();
        ApplyScale();

        _canvas.enabled = false;
        Plugin.Log.LogInfo("ShipBeacon canvas HUD built.");
    }

    private void TryAssignFont()
    {
        if (_label == null || _fontAssigned)
            return;

        try
        {
            TMP_FontAsset? font = null;
            var hud = HUDManager.Instance;
            if (hud != null)
            {
                // Prefer clock font so beacon matches top HUD.
                if (hud.clockNumber != null && hud.clockNumber.font != null)
                    font = hud.clockNumber.font;

                if (font == null && hud.controlTipLines != null)
                {
                    foreach (var tip in hud.controlTipLines)
                    {
                        if (tip != null && tip.font != null)
                        {
                            font = tip.font;
                            break;
                        }
                    }
                }

                if (font == null && hud.weightCounter != null)
                    font = hud.weightCounter.font;
            }

            if (font == null && TMP_Settings.defaultFontAsset != null)
                font = TMP_Settings.defaultFontAsset;

            if (font != null)
            {
                _label.font = font;
                _fontAssigned = true;
                Plugin.Log.LogInfo(string.Format("ShipBeacon TMP font assigned: {0}", font.name));
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning(string.Format("ShipBeacon font assign failed: {0}", ex.Message));
        }
    }

    private void ApplyScale()
    {
        if (_label == null)
            return;

        // Track clock size; stay slightly smaller than the clock digits.
        float size = 26f;
        try
        {
            var clock = HUDManager.Instance?.clockNumber;
            if (clock != null && clock.fontSize > 1f)
                size = clock.fontSize * 0.92f;
        }
        catch
        {
            // ignore — use fallback
        }

        _label.fontSize = size * Plugin.HudScale.Value;
    }

    private void ApplyClockStyle()
    {
        if (_label == null)
            return;

        try
        {
            // Vertex color only — faceColor/outline need a TMP material and NRE on cold start.
            _label.color = ClockOrange;
            _label.fontStyle = FontStyles.Bold;

            if (Plugin.MatchClockStyle == null || !Plugin.MatchClockStyle.Value)
                return;

            var hud = HUDManager.Instance;
            var clock = hud != null ? hud.clockNumber : null;
            if (clock == null)
                return;

            if (clock.font != null)
            {
                _label.font = clock.font;
                _fontAssigned = true;
            }
            // Never copy clock fontSharedMaterial (washes orange to white).
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning(string.Format("ApplyClockStyle: {0}", ex.Message));
            try { _label.color = ClockOrange; } catch { /* ignore */ }
        }
    }

    private void LateUpdate()
    {
        if (_canvas == null || _label == null || _labelRt == null)
            BuildUi();

        TryAssignFont();
        ApplyClockStyle();
        ApplyScale();

        if (Time.unscaledTime >= _nextHeartbeat)
        {
            _nextHeartbeat = Time.unscaledTime + 10f;
            var start = StartOfRound.Instance;
            var player = GameNetworkManager.Instance?.localPlayerController;
            Plugin.Log.LogInfo(string.Format(
                "[Heartbeat] enabled={0}, canvas={1}, canvasOn={2}, font={3}, hide='{4}', detail='{5}', inShipPhase={6}, playerNull={7}, dead={8}, insideFactory={9}, inShip={10}",
                Plugin.Enabled.Value, _canvas != null, _canvas?.enabled, _fontAssigned, _lastHideReason, _lastBeaconDetail,
                start?.inShipPhase, player == null, player?.isPlayerDead, player?.isInsideFactory, player?.isInHangarShipRoom));
        }

        // Don't use Plugin.Instance == null — BaseUnityPlugin is a UnityEngine.Object and
        // Unity's overloaded == can fake-null a live plugin and permanently hide the HUD.
        if (Plugin.Enabled == null || !Plugin.Enabled.Value)
        {
            SetVisible(false);
            _lastHideReason = "disabled";
            return;
        }

        if (_labelRt != null)
        {
            var y = Mathf.Clamp01(Plugin.VerticalOffset.Value);
            _labelRt.anchorMin = new Vector2(0.5f, y);
            _labelRt.anchorMax = new Vector2(0.5f, y);
        }

        ApplyScale();

        if (!TryGetBeacon(out var angleDeg, out var distance, out var reason))
        {
            SetVisible(false);
            MaybeLogHidden(reason);
            _loggedVisible = false;
            return;
        }

        try
        {
            _label!.text = FormatBeacon(angleDeg, distance);

            SetVisible(true);
            _lastHideReason = "";
            _lastBeaconDetail = string.Format("{0:0}m bearing={1:0}", distance, angleDeg);

            if (!_loggedVisible)
            {
                _loggedVisible = true;
                Plugin.Log.LogInfo(string.Format("ShipBeacon visible: {0:0}m, bearing {1:0} deg.", distance, angleDeg));
            }
        }
        catch (Exception ex)
        {
            SetVisible(false);
            MaybeLogHidden("label update: " + ex.Message);
            _loggedVisible = false;
        }
    }

    private void SetVisible(bool visible)
    {
        if (_canvas != null && _canvas.enabled != visible)
            _canvas.enabled = visible;

        // Also toggle the label GO in case canvas.enabled is ignored by LC overlays.
        if (_label != null && _label.gameObject.activeSelf != visible)
            _label.gameObject.SetActive(visible);
    }

    private void MaybeLogHidden(string reason)
    {
        reason ??= "";
        var changed = reason != _lastHideReason;
        _lastHideReason = reason;
        if (!changed && Time.unscaledTime < _nextDebugLog)
            return;

        _nextDebugLog = Time.unscaledTime + 8f;
        if (!string.IsNullOrEmpty(reason))
            Plugin.Log.LogInfo(string.Format("ShipBeacon hidden: {0}", reason));
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

            var ship = ResolveShipTransform(start);
            if (ship == null)
            {
                hideReason = "no ship transform";
                return false;
            }

            var cam = player.gameplayCamera != null
                ? player.gameplayCamera.transform
                : player.transform;

            var flat = ship.position - cam.position;
            flat.y = 0f;
            distance = flat.magnitude;
            if (distance < 0.5f)
            {
                hideReason = string.Format("too close ({0:0.0}m)", distance);
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

    private static Transform? ResolveShipTransform(StartOfRound start)
    {
        // Prefer the actual HangarShip root (matches TerminalStuff / loot scans).
        try
        {
            var hangar = GameObject.Find("/Environment/HangarShip")
                ?? GameObject.Find("Environment/HangarShip")
                ?? GameObject.Find("HangarShip");
            if (hangar != null)
                return hangar.transform;
        }
        catch
        {
            // ignored
        }

        if (start.shipDoorNode != null)
            return start.shipDoorNode;
        if (start.middleOfShipNode != null)
            return start.middleOfShipNode;
        if (start.elevatorTransform != null)
            return start.elevatorTransform;
        if (start.shipBounds != null)
            return start.shipBounds.transform;
        if (start.shipLandingPosition != null)
            return start.shipLandingPosition;

        try
        {
            var door = UnityEngine.Object.FindObjectOfType<HangarShipDoor>();
            if (door != null)
                return door.transform;
        }
        catch
        {
            // ignored
        }

        return null;
    }

    private static string FormatBeacon(float signedAngleDeg, float distance)
    {
        // SignedAngle: negative = ship is left of view, positive = right.
        var abs = Mathf.Abs(signedAngleDeg);
        string body = Plugin.ShowDistance.Value
            ? string.Format("SHIP  {0:0}m", distance)
            : "SHIP";

        // Straight ahead: caret arrows on both sides.
        if (abs <= 25f)
            return string.Format("^  {0}  ^", body);

        // Mostly behind: down carets.
        if (abs >= 155f)
            return string.Format("v  {0}  v", body);

        // Side: chevrons only on the pointing side; count grows with turn.
        var count = 1;
        if (abs > 45f) count = 2;
        if (abs > 70f) count = 3;
        if (abs > 100f) count = 4;

        if (signedAngleDeg < 0f)
            return string.Format("{0}  {1}", new string('<', count), body);

        return string.Format("{0}  {1}", body, new string('>', count));
    }
}
