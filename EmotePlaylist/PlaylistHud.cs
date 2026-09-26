using System;
using System.Text;
using TMPro;
using TooManyEmotes;
using UnityEngine;
using UnityEngine.UI;

namespace EmotePlaylist;

/// <summary>
/// On-screen emote playlist panel (Screen Space Overlay), modeled after ShipBeaconHud.
/// </summary>
internal sealed class PlaylistHud : MonoBehaviour
{
    private static PlaylistHud? _instance;

    private Canvas? _canvas;
    private Image? _backdrop;
    private TextMeshProUGUI? _label;
    private bool _fontAssigned;
    private bool _visible;
    private float _nextRefresh;
    private string _cachedText = "";

    private static readonly Color PanelBg = new Color(0.05f, 0.05f, 0.08f, 0.78f);
    private static readonly Color TextColor = new Color(0.92f, 0.92f, 0.95f, 1f);
    private static readonly Color AccentOrange = new Color(0.855f, 0.400f, 0.185f, 1f);

    internal static PlaylistHud? Instance => _instance;

    internal static void EnsureExists()
    {
        if (_instance != null)
            return;

        var go = new GameObject("EmotePlaylistHUD");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<PlaylistHud>();
    }

    internal static void Toggle()
    {
        EnsureExists();
        if (_instance == null)
            return;
        _instance.SetVisible(!_instance._visible);
    }

    internal static void Show()
    {
        EnsureExists();
        _instance?.SetVisible(true);
    }

    internal static void Hide()
    {
        _instance?.SetVisible(false);
    }

    internal static bool IsVisible => _instance != null && _instance._visible;

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
        _canvas.sortingOrder = 4900;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        gameObject.AddComponent<GraphicRaycaster>();

        var panelGo = new GameObject("Panel", typeof(RectTransform));
        panelGo.transform.SetParent(transform, false);
        var panelRt = panelGo.GetComponent<RectTransform>();
        // Mid-left panel
        panelRt.anchorMin = new Vector2(0f, 0.5f);
        panelRt.anchorMax = new Vector2(0f, 0.5f);
        panelRt.pivot = new Vector2(0f, 0.5f);
        panelRt.sizeDelta = new Vector2(360f, 280f);
        panelRt.anchoredPosition = new Vector2(24f, 40f);

        _backdrop = panelGo.AddComponent<Image>();
        _backdrop.color = PanelBg;
        _backdrop.raycastTarget = false;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(panelGo.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(14f, 10f);
        textRt.offsetMax = new Vector2(-14f, -10f);

        _label = textGo.AddComponent<TextMeshProUGUI>();
        _label.alignment = TextAlignmentOptions.TopLeft;
        _label.fontStyle = FontStyles.Normal;
        _label.color = TextColor;
        _label.enableWordWrapping = true;
        _label.raycastTarget = false;
        _label.overflowMode = TextOverflowModes.Overflow;
        _label.fontSize = 20f;
        _label.lineSpacing = 4f;
        _label.text = "";
        TryAssignFont();

        _canvas.enabled = false;
        if (_backdrop != null)
            _backdrop.gameObject.SetActive(false);

        Plugin.Log.LogInfo("EmotePlaylist HUD built.");
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
                if (hud.controlTipLines != null)
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

                if (font == null && hud.clockNumber != null && hud.clockNumber.font != null)
                    font = hud.clockNumber.font;

                if (font == null && hud.weightCounter != null)
                    font = hud.weightCounter.font;
            }

            if (font == null && TMP_Settings.defaultFontAsset != null)
                font = TMP_Settings.defaultFontAsset;

            if (font != null)
            {
                _label.font = font;
                _fontAssigned = true;
                Plugin.Log.LogInfo($"EmotePlaylist HUD font: {font.name}");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"EmotePlaylist HUD font assign failed: {ex.Message}");
        }
    }

    private void LateUpdate()
    {
        if (_canvas == null || _label == null)
            BuildUi();

        if (!_visible)
            return;

        TryAssignFont();

        if (Time.unscaledTime < _nextRefresh)
            return;

        _nextRefresh = Time.unscaledTime + 0.5f;
        RefreshText();
    }

    private void RefreshText()
    {
        if (_label == null)
            return;

        var sb = new StringBuilder(256);
        sb.Append("<b><color=#DA662F>Emote Playlist</color></b>\n");

        var running = PlaylistRunner.TryGetStatus(out var curIndex, out var remain, out var total);
        var overrideNames = GetOverrideNames();

        for (var i = 0; i < 8; i++)
        {
            var name = GetSlotName(i, overrideNames);
            var line = $"{i + 1}. {name}";
            if (running && i == curIndex)
                sb.Append($"<color=#DA662F><b>> {line}</b>  ({remain}s)</color>\n");
            else
                sb.Append(line).Append('\n');
        }

        var startKey = InputUtil.TipLabel(Plugin.PlaylistKey.Value);
        var listKey = InputUtil.TipLabel(Plugin.ListKey.Value);
        if (running)
            sb.Append($"\n<color=#AAAAAA>{startKey} stop · {listKey} hide · {curIndex + 1}/{Mathf.Max(total, 1)} ({remain}s)</color>");
        else
            sb.Append($"\n<color=#AAAAAA>{startKey} start/stop · {listKey} hide</color>");

        _cachedText = sb.ToString();
        _label.text = _cachedText;
        _label.color = TextColor;
    }

    private static string[]? GetOverrideNames()
    {
        var over = Plugin.PlaylistOverride?.Value;
        if (string.IsNullOrWhiteSpace(over))
            return null;

        var parts = over.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
        var names = new string[parts.Length];
        for (var i = 0; i < parts.Length; i++)
            names[i] = parts[i].Trim();
        return names;
    }

    private static string GetSlotName(int index, string[]? overrideNames)
    {
        if (overrideNames != null)
        {
            if (index < overrideNames.Length && !string.IsNullOrEmpty(overrideNames[index]))
                return overrideNames[index];
            return "(empty)";
        }

        try
        {
            var emote = QuickEmotes.GetQuickEmote(index);
            if (emote != null && !string.IsNullOrEmpty(emote.emoteName))
                return emote.emoteName;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"HUD quick slot {index}: {ex.Message}");
        }

        return "(empty)";
    }

    private void SetVisible(bool visible)
    {
        _visible = visible;
        if (_canvas != null)
            _canvas.enabled = visible;
        if (_backdrop != null && _backdrop.gameObject.activeSelf != visible)
            _backdrop.gameObject.SetActive(visible);

        if (visible)
        {
            _nextRefresh = 0f;
            RefreshText();
            Plugin.Log.LogInfo("EmotePlaylist HUD shown.");
        }
        else
        {
            Plugin.Log.LogInfo("EmotePlaylist HUD hidden.");
        }
    }
}
