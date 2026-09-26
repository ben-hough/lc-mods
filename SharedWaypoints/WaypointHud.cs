using System;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SharedWaypoints;

/// <summary>
/// Fixed-position HUD readout for shared waypoints (no world-tracked floating markers).
/// </summary>
internal sealed class WaypointHud : MonoBehaviour
{
    private static WaypointHud? _instance;

    private Canvas? _canvas;
    private RectTransform? _panelRt;
    private Image? _backdrop;
    private TextMeshProUGUI? _label;
    private Text? _fallback;
    private bool _fontAssigned;
    private TMP_FontAsset? _tmpFont;
    private bool _wasInShipPhase;
    private float _nextRefresh;
    private string _cachedText = "";

    private static readonly Color PanelBg = new Color(0.05f, 0.05f, 0.08f, 0.72f);

    internal static void EnsureExists()
    {
        if (_instance != null)
            return;

        var go = new GameObject("SharedWaypointsHUD");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<WaypointHud>();
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
        _canvas.sortingOrder = 5000;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("Panel", typeof(RectTransform));
        panelGo.transform.SetParent(transform, false);
        _panelRt = panelGo.GetComponent<RectTransform>();
        // Fixed top-right, clear of weight/clock clutter on the left/top-center.
        _panelRt.anchorMin = new Vector2(1f, 1f);
        _panelRt.anchorMax = new Vector2(1f, 1f);
        _panelRt.pivot = new Vector2(1f, 1f);
        _panelRt.sizeDelta = new Vector2(340f, 160f);
        _panelRt.anchoredPosition = new Vector2(-24f, -96f);

        _backdrop = panelGo.AddComponent<Image>();
        _backdrop.color = PanelBg;
        _backdrop.raycastTarget = false;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(panelGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(12f, 8f);
        textRt.offsetMax = new Vector2(-12f, -8f);

        try
        {
            _label = textGo.AddComponent<TextMeshProUGUI>();
            _label.alignment = TextAlignmentOptions.TopLeft;
            _label.fontStyle = FontStyles.Normal;
            _label.enableWordWrapping = true;
            _label.raycastTarget = false;
            _label.overflowMode = TextOverflowModes.Overflow;
            _label.fontSize = 20f;
            _label.lineSpacing = 2f;
            _label.color = Color.white;
            _label.text = "";
        }
        catch
        {
            _fallback = textGo.AddComponent<Text>();
            _fallback.alignment = TextAnchor.UpperLeft;
            _fallback.raycastTarget = false;
            _fallback.fontSize = 16;
            _fallback.horizontalOverflow = HorizontalWrapMode.Wrap;
            _fallback.verticalOverflow = VerticalWrapMode.Overflow;
            _fallback.color = Color.white;
            if (Resources.GetBuiltinResource<Font>("Arial.ttf") is Font font)
                _fallback.font = font;
            _fallback.text = "";
        }

        SetPanelVisible(false);
        Plugin.Log.LogInfo("SharedWaypoints fixed HUD panel built (top-right).");
    }

    private void Update()
    {
        WaypointNet.EnsureRegistered();
        HandleInput();
        MaybeClearOnShipPhase();
    }

    private void LateUpdate()
    {
        if (_canvas == null)
            BuildUi();

        TryAssignFont();

        // Map markers need per-frame scale lerp (vanilla door codes); cheap reconcile.
        WaypointMapMarkers.Sync();

        if (Time.unscaledTime < _nextRefresh)
            return;
        _nextRefresh = Time.unscaledTime + 0.1f;
        RefreshPanel();
    }

    private void MaybeClearOnShipPhase()
    {
        var start = StartOfRound.Instance;
        var inShip = start != null && start.inShipPhase;
        if (inShip && !_wasInShipPhase)
        {
            WaypointRegistry.ClearAll();
            WaypointMapMarkers.DestroyAll();
            Plugin.Log.LogInfo("Cleared waypoints entering ship phase.");
        }
        _wasInShipPhase = inShip;
    }

    private void HandleInput()
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value)
            return;

        var player = GameNetworkManager.Instance?.localPlayerController;
        if (player == null || player.isPlayerDead)
            return;

        if (player.isTypingChat || player.inTerminalMenu)
            return;

        if (InputUtil.WasPressedThisFrame(Plugin.DropKey.Value))
            DropWaypoint(player);

        if (InputUtil.WasPressedThisFrame(Plugin.ClearKey.Value))
            ClearWaypoint(player);
    }

    private static void DropWaypoint(GameNetcodeStuff.PlayerControllerB player)
    {
        var state = new WaypointState
        {
            OwnerPlayerClientId = player.playerClientId,
            OwnerName = string.IsNullOrEmpty(player.playerUsername) ? "Player" : player.playerUsername,
            Position = player.transform.position,
            IsInside = player.isInsideFactory,
            ColorIndex = WaypointState.ColorIndexFor(player.playerClientId)
        };

        WaypointRegistry.Set(state);
        WaypointNet.BroadcastSet(state);

        var where = state.IsInside ? "inside" : "outside";
        Feedback(
            $"Waypoint dropped ({where})",
            "Shared Waypoint",
            $"Dropped {where} pin (keeps your other zone pin).");
        Plugin.Log.LogInfo(
            $"Dropped waypoint inside={state.IsInside} pos={state.Position} owner={state.OwnerPlayerClientId}.");
    }

    private static void ClearWaypoint(GameNetcodeStuff.PlayerControllerB player)
    {
        var id = player.playerClientId;
        var inside = player.isInsideFactory;
        if (!WaypointRegistry.TryGet(id, inside, out _))
        {
            var zone = inside ? "indoor" : "outdoor";
            Feedback($"No {zone} waypoint", "Shared Waypoint", $"You have no {zone} pin to clear.");
            return;
        }

        WaypointRegistry.Clear(id, inside);
        WaypointNet.BroadcastClear(id, inside);
        var cleared = inside ? "indoor" : "outdoor";
        Feedback($"Waypoint cleared ({cleared})", "Shared Waypoint", $"Your {cleared} pin was removed.");
        Plugin.Log.LogInfo($"Cleared own waypoint owner={id} inside={inside}.");
    }

    private static void Feedback(string tip, string title, string body)
    {
        try
        {
            var hud = HUDManager.Instance;
            if (hud == null)
                return;
            hud.ChangeControlTip(3, tip);
            hud.DisplayTip(title, body);
        }
        catch
        {
            // ignored
        }
    }

    private void TryAssignFont()
    {
        if (_fontAssigned)
            return;

        try
        {
            TMP_FontAsset? font = null;
            var hud = HUDManager.Instance;
            if (hud != null)
            {
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
                _tmpFont = font;
                _fontAssigned = true;
                if (_label != null)
                    _label.font = font;
                Plugin.Log.LogInfo($"SharedWaypoints TMP font assigned: {font.name}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Font assign failed: {ex.Message}");
        }
    }

    private void RefreshPanel()
    {
        if (_canvas == null || _panelRt == null)
            return;

        if (Plugin.Enabled == null || !Plugin.Enabled.Value)
        {
            SetPanelVisible(false);
            return;
        }

        var player = GameNetworkManager.Instance?.localPlayerController;
        if (player == null || player.isPlayerDead)
        {
            SetPanelVisible(false);
            return;
        }

        var start = StartOfRound.Instance;
        if (start == null || start.inShipPhase)
        {
            SetPanelVisible(false);
            return;
        }

        var localInside = player.isInsideFactory;
        var localPos = player.transform.position;
        var localId = player.playerClientId;
        var maxDist = Plugin.MaxDistance?.Value ?? 500f;
        var showOwn = Plugin.ShowOwnWaypoint == null || Plugin.ShowOwnWaypoint.Value;
        var scale = Mathf.Clamp(Plugin.HudScale?.Value ?? 1f, 0.6f, 2f);

        _panelRt.localScale = Vector3.one * scale;

        var lines = new List<(string text, Color color)>();
        foreach (var wp in WaypointRegistry.Snapshot())
        {
            if (wp.IsInside != localInside)
                continue;

            if (!showOwn && wp.OwnerPlayerClientId == localId)
                continue;

            var dist = Vector3.Distance(localPos, wp.Position);
            if (dist > maxDist)
                continue;

            var bearing = BearingArrow(player, wp.Position);
            var shortName = Shorten(wp.OwnerName, 14);
            var where = wp.IsInside ? "IN" : "OUT";
            var mine = wp.OwnerPlayerClientId == localId ? "*" : " ";
            lines.Add(($"{mine}{bearing} {shortName}  {dist:0}m  [{where}]", wp.Color));
        }

        if (lines.Count == 0)
        {
            SetPanelVisible(false);
            return;
        }

        var sb = new StringBuilder(128);
        sb.Append("<color=#FFAA55>WAYPOINTS</color>\n");
        foreach (var (text, color) in lines)
        {
            var hex = ColorUtility.ToHtmlStringRGB(color);
            sb.Append("<color=#").Append(hex).Append('>').Append(text).Append("</color>\n");
        }

        var built = sb.ToString().TrimEnd();
        if (built != _cachedText)
        {
            _cachedText = built;
            if (_label != null)
            {
                _label.text = built;
                _label.color = Color.white;
            }
            else if (_fallback != null)
            {
                // Unity UI Text has no rich color tags reliably; plain text.
                var plain = new StringBuilder();
                plain.AppendLine("WAYPOINTS");
                foreach (var (text, _) in lines)
                    plain.AppendLine(text);
                _fallback.text = plain.ToString().TrimEnd();
            }
        }

        // Grow panel height with line count.
        var h = Mathf.Clamp(36f + lines.Count * 26f, 60f, 280f);
        _panelRt.sizeDelta = new Vector2(340f, h);
        SetPanelVisible(true);
    }

    private void SetPanelVisible(bool visible)
    {
        if (_panelRt != null && _panelRt.gameObject.activeSelf != visible)
            _panelRt.gameObject.SetActive(visible);
        if (!visible)
            _cachedText = "";
    }

    private static string Shorten(string name, int max)
    {
        if (string.IsNullOrEmpty(name))
            return "?";
        return name.Length <= max ? name : name.Substring(0, max - 1) + "…";
    }

    /// <summary>
    /// Relative bearing from the player's look yaw to the waypoint (horizontal).
    /// </summary>
    private static string BearingArrow(GameNetcodeStuff.PlayerControllerB player, Vector3 target)
    {
        var to = target - player.transform.position;
        to.y = 0f;
        if (to.sqrMagnitude < 0.01f)
            return "●";

        var forward = player.transform.forward;
        forward.y = 0f;
        if (forward.sqrMagnitude < 0.0001f)
            forward = Vector3.forward;
        forward.Normalize();

        var angle = Vector3.SignedAngle(forward, to.normalized, Vector3.up);
        // 8-way relative arrow
        var abs = Mathf.Abs(angle);
        if (abs < 22.5f)
            return "↑";
        if (abs > 157.5f)
            return "↓";
        if (angle > 0f)
        {
            if (angle < 67.5f) return "↗";
            if (angle < 112.5f) return "→";
            return "↘";
        }

        if (angle > -67.5f) return "↖";
        if (angle > -112.5f) return "←";
        return "↙";
    }
}
