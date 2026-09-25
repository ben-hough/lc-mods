using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SprayPatterns;

/// <summary>Screen-space radial pattern selector.</summary>
internal sealed class RadialMenuUI : MonoBehaviour
{
    private Canvas? _canvas;
    private RectTransform? _root;
    private Image? _dim;
    private TextMeshProUGUI? _centerLabel;
    private Text? _centerFallback;
    private readonly List<SegmentView> _segments = new();
    private bool _open;
    private int _hoverIndex = -1;

    private struct SegmentView
    {
        public PatternDef Def;
        public RectTransform Rt;
        public Image Icon;
        public Image Highlight;
        public TextMeshProUGUI? Label;
        public Text? LabelFallback;
    }

    public bool IsOpen => _open;

    public void EnsureBuilt()
    {
        if (_canvas != null)
            return;

        _canvas = gameObject.AddComponent<Canvas>();
        _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        _canvas.sortingOrder = 6000;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        gameObject.AddComponent<GraphicRaycaster>();

        var dimGo = new GameObject("Dim", typeof(RectTransform));
        dimGo.transform.SetParent(transform, false);
        _dim = dimGo.AddComponent<Image>();
        _dim.color = new Color(0f, 0f, 0f, 0.45f);
        _dim.raycastTarget = false;
        var dimRt = _dim.rectTransform;
        dimRt.anchorMin = Vector2.zero;
        dimRt.anchorMax = Vector2.one;
        dimRt.offsetMin = Vector2.zero;
        dimRt.offsetMax = Vector2.zero;

        var rootGo = new GameObject("RadialRoot", typeof(RectTransform));
        rootGo.transform.SetParent(transform, false);
        _root = rootGo.GetComponent<RectTransform>();
        _root.anchorMin = _root.anchorMax = new Vector2(0.5f, 0.5f);
        _root.sizeDelta = new Vector2(520f, 520f);
        _root.anchoredPosition = Vector2.zero;

        var centerGo = new GameObject("CenterLabel", typeof(RectTransform));
        centerGo.transform.SetParent(_root, false);
        var centerRt = centerGo.GetComponent<RectTransform>();
        centerRt.anchorMin = centerRt.anchorMax = new Vector2(0.5f, 0.5f);
        centerRt.sizeDelta = new Vector2(180f, 64f);
        try
        {
            _centerLabel = centerGo.AddComponent<TextMeshProUGUI>();
            _centerLabel.alignment = TextAlignmentOptions.Center;
            _centerLabel.fontSize = 28f;
            _centerLabel.color = Color.white;
            _centerLabel.raycastTarget = false;
        }
        catch
        {
            _centerFallback = centerGo.AddComponent<Text>();
            _centerFallback.alignment = TextAnchor.MiddleCenter;
            _centerFallback.fontSize = 22;
            _centerFallback.color = Color.white;
            _centerFallback.raycastTarget = false;
            if (Resources.GetBuiltinResource<Font>("Arial.ttf") != null)
                _centerFallback.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        }

        SetVisible(false);
    }

    public void SetVisible(bool visible)
    {
        _open = visible;
        if (_canvas != null)
            _canvas.enabled = visible;
        if (_dim != null)
            _dim.enabled = visible;
        if (!visible)
            _hoverIndex = -1;
    }

    public void Rebuild()
    {
        EnsureBuilt();
        foreach (var seg in _segments)
        {
            if (seg.Rt != null)
                Destroy(seg.Rt.gameObject);
        }
        _segments.Clear();

        var enabled = PatternCatalog.GetEnabled();
        var n = enabled.Count;
        if (n == 0 || _root == null)
            return;

        var radius = 170f;
        for (var i = 0; i < n; i++)
        {
            var def = enabled[i];
            var ang = (i / (float)n) * Mathf.PI * 2f - Mathf.PI * 0.5f;
            var pos = new Vector2(Mathf.Cos(ang) * radius, Mathf.Sin(ang) * radius);

            var go = new GameObject($"Seg_{def.Id}", typeof(RectTransform));
            go.transform.SetParent(_root, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(96f, 96f);
            rt.anchoredPosition = pos;

            var hiGo = new GameObject("Highlight", typeof(RectTransform));
            hiGo.transform.SetParent(rt, false);
            var hiRt = hiGo.GetComponent<RectTransform>();
            hiRt.anchorMin = Vector2.zero;
            hiRt.anchorMax = Vector2.one;
            hiRt.offsetMin = new Vector2(-8f, -8f);
            hiRt.offsetMax = new Vector2(8f, 8f);
            var highlight = hiGo.AddComponent<Image>();
            highlight.color = new Color(1f, 0.9f, 0.3f, 0f);
            highlight.raycastTarget = false;

            var iconGo = new GameObject("Icon", typeof(RectTransform));
            iconGo.transform.SetParent(rt, false);
            var iconRt = iconGo.GetComponent<RectTransform>();
            iconRt.anchorMin = new Vector2(0.1f, 0.28f);
            iconRt.anchorMax = new Vector2(0.9f, 0.95f);
            iconRt.offsetMin = iconRt.offsetMax = Vector2.zero;
            var icon = iconGo.AddComponent<Image>();
            icon.sprite = PatternTextures.GetSprite(def.Id);
            icon.color = def.Tint;
            icon.preserveAspect = true;
            icon.raycastTarget = false;

            var labelGo = new GameObject("Label", typeof(RectTransform));
            labelGo.transform.SetParent(rt, false);
            var labelRt = labelGo.GetComponent<RectTransform>();
            labelRt.anchorMin = new Vector2(0f, 0f);
            labelRt.anchorMax = new Vector2(1f, 0.32f);
            labelRt.offsetMin = labelRt.offsetMax = Vector2.zero;

            TextMeshProUGUI? tmp = null;
            Text? fallback = null;
            try
            {
                tmp = labelGo.AddComponent<TextMeshProUGUI>();
                tmp.alignment = TextAlignmentOptions.Center;
                tmp.fontSize = 16f;
                tmp.color = Color.white;
                tmp.text = def.Label;
                tmp.raycastTarget = false;
            }
            catch
            {
                fallback = labelGo.AddComponent<Text>();
                fallback.alignment = TextAnchor.MiddleCenter;
                fallback.fontSize = 14;
                fallback.color = Color.white;
                fallback.text = def.Label;
                fallback.raycastTarget = false;
            }

            _segments.Add(new SegmentView
            {
                Def = def,
                Rt = rt,
                Icon = icon,
                Highlight = highlight,
                Label = tmp,
                LabelFallback = fallback,
            });
        }
    }

    /// <summary>Update hover from mouse; returns hovered pattern if any.</summary>
    public PatternId? TickHover()
    {
        if (!_open || _segments.Count == 0 || _root == null)
            return null;

        var mouse = InputUtil.MouseScreenPosition();
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            _root, mouse, null, out var local);

        var dist = local.magnitude;
        if (dist < 40f)
        {
            SetHover(-1, "Select");
            return null;
        }

        var ang = Mathf.Atan2(local.y, local.x);
        // Convert to 0..1 starting from top (-90°)
        var turned = ang + Mathf.PI * 0.5f;
        if (turned < 0f)
            turned += Mathf.PI * 2f;
        var idx = Mathf.FloorToInt((turned / (Mathf.PI * 2f)) * _segments.Count) % _segments.Count;
        if (idx < 0)
            idx += _segments.Count;

        SetHover(idx, _segments[idx].Def.Label);
        return _segments[idx].Def.Id;
    }

    private void SetHover(int idx, string center)
    {
        _hoverIndex = idx;
        if (_centerLabel != null)
            _centerLabel.text = center;
        if (_centerFallback != null)
            _centerFallback.text = center;

        for (var i = 0; i < _segments.Count; i++)
        {
            var on = i == idx;
            _segments[i].Highlight.color = on
                ? new Color(1f, 0.9f, 0.25f, 0.55f)
                : new Color(1f, 0.9f, 0.25f, 0f);
            _segments[i].Rt.localScale = on ? Vector3.one * 1.12f : Vector3.one;
        }
    }

    public PatternId? CurrentHover =>
        _hoverIndex >= 0 && _hoverIndex < _segments.Count
            ? _segments[_hoverIndex].Def.Id
            : null;
}
