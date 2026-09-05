using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace ShipBeacon;

/// <summary>
/// Outdoor ship direction HUD using a Screen Space Overlay canvas (IMGUI often dies after moon load).
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

    internal static void EnsureExists()
    {
        if (_instance != null)
            return;

        var go = new GameObject("ShipBeaconHUD");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
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
        _canvas.sortingOrder = 1200;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        var textGo = new GameObject("ShipBeaconLabel", typeof(RectTransform));
        textGo.transform.SetParent(transform, false);

        _labelRt = textGo.GetComponent<RectTransform>();
        _labelRt.anchorMin = new Vector2(0.5f, Plugin.VerticalOffset.Value);
        _labelRt.anchorMax = new Vector2(0.5f, Plugin.VerticalOffset.Value);
        _labelRt.pivot = new Vector2(0.5f, 0.5f);
        _labelRt.sizeDelta = new Vector2(640f, 64f);
        _labelRt.anchoredPosition = Vector2.zero;

        // Soft backdrop via Image behind text.
        var bgGo = new GameObject("ShipBeaconBg", typeof(RectTransform));
        bgGo.transform.SetParent(textGo.transform, false);
        bgGo.transform.SetAsFirstSibling();
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = new Vector2(-12f, -6f);
        bgRt.offsetMax = new Vector2(12f, 6f);
        var bg = bgGo.AddComponent<Image>();
        bg.color = new Color(0f, 0f, 0f, 0.55f);
        bg.raycastTarget = false;

        _label = textGo.AddComponent<TextMeshProUGUI>();
        _label.alignment = TextAlignmentOptions.Center;
        _label.fontStyle = FontStyles.Bold;
        _label.color = new Color(0.9f, 0.96f, 1f, 1f);
        _label.enableWordWrapping = false;
        _label.raycastTarget = false;
        _label.text = "";
        ApplyScale();

        _canvas.enabled = false;
        Plugin.Log.LogInfo("ShipBeacon canvas HUD built.");
    }

    private void ApplyScale()
    {
        if (_label == null)
            return;
        _label.fontSize = 28f * Plugin.HudScale.Value;
    }

    private void LateUpdate()
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
        {
            SetVisible(false);
            return;
        }

        if (_canvas == null || _label == null || _labelRt == null)
            BuildUi();

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

        var arrow = AngleToArrow(angleDeg);
        _label!.text = Plugin.ShowDistance.Value
            ? $"{arrow}  SHIP  {distance:0}m  {arrow}"
            : $"{arrow}  SHIP  {arrow}";

        SetVisible(true);

        if (!_loggedVisible)
        {
            _loggedVisible = true;
            _lastHideReason = "";
            Plugin.Log.LogInfo($"ShipBeacon visible: {distance:0}m, bearing {angleDeg:0} deg.");
        }
    }

    private void SetVisible(bool visible)
    {
        if (_canvas != null && _canvas.enabled != visible)
            _canvas.enabled = visible;
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

            var flat = ship.position - cam.position;
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
