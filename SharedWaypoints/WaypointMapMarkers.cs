using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace SharedWaypoints;

/// <summary>
/// World-space waypoint pins under <c>mapScreen.mapCameraStationaryUI</c>, same as vanilla
/// door codes. Visible on the main ship radar and any camera (e.g. CrewMonitors map feeds)
/// that copies mapCamera's culling mask — no soft dependency on CrewMonitors.
/// </summary>
internal static class WaypointMapMarkers
{
    private const string RootName = "SharedWaypointMapMarkers";
    private const float HeightOffset = 1.35f;
    private const float NearScaleDistance = 8f;
    private const float NearScale = 1.8f;
    private const float FarScale = 1f;

    private static Transform? _root;
    private static readonly Dictionary<(ulong Owner, bool Inside), MarkerVisual> Markers = new();
    private static Sprite? _fallbackSprite;

    private sealed class MarkerVisual
    {
        public GameObject Go = null!;
        public RectTransform Rt = null!;
        public TextMeshProUGUI? Label;
        public Image? Box;
        public Text? FallbackText;
        public float BaseScale = 1f;
    }

    /// <summary>Reconcile marker GameObjects with <see cref="WaypointRegistry"/>.</summary>
    public static void Sync()
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value)
        {
            DestroyAll();
            return;
        }

        var start = StartOfRound.Instance;
        if (start == null || start.inShipPhase)
        {
            DestroyAll();
            return;
        }

        if (!EnsureRoot(start))
            return;

        var player = GameNetworkManager.Instance?.localPlayerController;
        var localId = player != null ? player.playerClientId : ulong.MaxValue;
        var localPos = player != null ? player.transform.position : Vector3.zero;
        var hasPlayer = player != null && !player.isPlayerDead;
        var maxDist = Plugin.MaxDistance?.Value ?? 500f;
        var showOwn = Plugin.ShowOwnWaypoint == null || Plugin.ShowOwnWaypoint.Value;

        var mapCam = start.mapScreen != null ? start.mapScreen.mapCamera : null;
        var mapCamPos = mapCam != null ? mapCam.transform.position : Vector3.zero;

        var seen = new HashSet<(ulong, bool)>();
        foreach (var wp in WaypointRegistry.Snapshot())
        {
            var key = (wp.OwnerPlayerClientId, wp.IsInside);
            seen.Add(key);

            if (!showOwn && wp.OwnerPlayerClientId == localId)
            {
                HideOrRemove(key);
                continue;
            }

            var visible = true;
            if (hasPlayer)
            {
                var dist = Vector3.Distance(localPos, wp.Position);
                if (dist > maxDist)
                    visible = false;
            }

            if (!Markers.TryGetValue(key, out var marker) || marker.Go == null)
            {
                marker = CreateMarker(wp);
                if (marker == null)
                    continue;
                Markers[key] = marker;
            }
            else
            {
                ApplyLabelAndColor(marker, wp);
            }

            PositionMarker(marker, wp.Position);

            if (marker.Go.activeSelf != visible)
                marker.Go.SetActive(visible);

            if (visible)
                UpdateScale(marker, mapCam != null, mapCamPos);
        }

        // Remove orphans (cleared / owner left).
        List<(ulong, bool)>? toRemove = null;
        foreach (var kv in Markers)
        {
            if (seen.Contains(kv.Key))
                continue;
            toRemove ??= new List<(ulong, bool)>();
            toRemove.Add(kv.Key);
        }

        if (toRemove != null)
        {
            foreach (var key in toRemove)
            {
                if (Markers.TryGetValue(key, out var m) && m.Go != null)
                    Object.Destroy(m.Go);
                Markers.Remove(key);
            }
        }
    }

    public static void DestroyAll()
    {
        foreach (var kv in Markers)
        {
            if (kv.Value.Go != null)
                Object.Destroy(kv.Value.Go);
        }
        Markers.Clear();

        if (_root != null)
        {
            Object.Destroy(_root.gameObject);
            _root = null;
        }
    }

    private static void HideOrRemove((ulong, bool) key)
    {
        if (!Markers.TryGetValue(key, out var marker))
            return;
        if (marker.Go != null)
            Object.Destroy(marker.Go);
        Markers.Remove(key);
    }

    private static bool EnsureRoot(StartOfRound start)
    {
        var map = start.mapScreen;
        if (map == null || map.mapCameraStationaryUI == null)
        {
            DestroyAll();
            return false;
        }

        var parent = map.mapCameraStationaryUI;
        if (_root != null && _root.parent != parent)
            DestroyAll();

        if (_root == null)
        {
            var go = new GameObject(RootName);
            go.transform.SetParent(parent, false);
            _root = go.transform;
        }

        return true;
    }

    private static MarkerVisual? CreateMarker(WaypointState wp)
    {
        if (_root == null)
            return null;

        var start = StartOfRound.Instance;
        GameObject go;
        var fromPrefab = false;

        if (start != null && start.objectCodePrefab != null)
        {
            go = Object.Instantiate(start.objectCodePrefab, _root, false);
            go.name = $"WP_{wp.OwnerPlayerClientId}_{(wp.IsInside ? "In" : "Out")}";
            fromPrefab = true;
        }
        else
        {
            go = BuildFallbackMarker();
            go.name = $"WP_{wp.OwnerPlayerClientId}_{(wp.IsInside ? "In" : "Out")}";
            go.transform.SetParent(_root, false);
        }

        var rt = go.GetComponent<RectTransform>();
        if (rt == null)
            rt = go.AddComponent<RectTransform>();

        var marker = new MarkerVisual
        {
            Go = go,
            Rt = rt,
            Label = go.GetComponentInChildren<TextMeshProUGUI>(true),
            Box = go.GetComponentInChildren<Image>(true),
            FallbackText = fromPrefab ? null : go.GetComponentInChildren<Text>(true),
            BaseScale = fromPrefab ? 1f : 0.02f
        };
        go.transform.localScale = Vector3.one * marker.BaseScale;

        // Prefab may nest the fill Image; prefer first non-filled Image for box tint if needed.
        if (marker.Box == null)
        {
            var images = go.GetComponentsInChildren<Image>(true);
            if (images != null && images.Length > 0)
                marker.Box = images[0];
        }

        ApplyLabelAndColor(marker, wp);
        PositionMarker(marker, wp.Position);
        return marker;
    }

    private static GameObject BuildFallbackMarker()
    {
        var go = new GameObject("SharedWaypointFallback", typeof(RectTransform));
        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(48f, 28f);

        // World-space canvas so orthographic map cams can see it if parent has no Canvas.
        var canvas = go.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        canvas.sortingOrder = 100;

        var boxGo = new GameObject("Box", typeof(RectTransform));
        boxGo.transform.SetParent(go.transform, false);
        var boxRt = boxGo.GetComponent<RectTransform>();
        boxRt.anchorMin = Vector2.zero;
        boxRt.anchorMax = Vector2.one;
        boxRt.offsetMin = Vector2.zero;
        boxRt.offsetMax = Vector2.zero;
        var img = boxGo.AddComponent<Image>();
        img.raycastTarget = false;
        img.sprite = GetFallbackSprite();
        img.color = Color.white;

        var textGo = new GameObject("Label", typeof(RectTransform));
        textGo.transform.SetParent(go.transform, false);
        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(2f, 1f);
        textRt.offsetMax = new Vector2(-2f, -1f);

        try
        {
            var tmp = textGo.AddComponent<TextMeshProUGUI>();
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 14f;
            tmp.raycastTarget = false;
            tmp.color = Color.white;
            if (TMP_Settings.defaultFontAsset != null)
                tmp.font = TMP_Settings.defaultFontAsset;
        }
        catch
        {
            var t = textGo.AddComponent<Text>();
            t.alignment = TextAnchor.MiddleCenter;
            t.fontSize = 12;
            t.raycastTarget = false;
            t.color = Color.white;
            if (Resources.GetBuiltinResource<Font>("Arial.ttf") is Font font)
                t.font = font;
        }

        return go;
    }

    private static Sprite GetFallbackSprite()
    {
        if (_fallbackSprite != null)
            return _fallbackSprite;
        var tex = Texture2D.whiteTexture;
        _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
        return _fallbackSprite;
    }

    private static void ApplyLabelAndColor(MarkerVisual marker, WaypointState wp)
    {
        var label = FormatLabel(wp);
        var color = wp.Color;

        if (marker.Label != null)
        {
            marker.Label.text = label;
            marker.Label.color = color;
        }
        else if (marker.FallbackText != null)
        {
            marker.FallbackText.text = label;
            marker.FallbackText.color = color;
        }

        if (marker.Box != null)
            marker.Box.color = color;
    }

    private static string FormatLabel(WaypointState wp)
    {
        var name = wp.OwnerName;
        if (string.IsNullOrEmpty(name))
            name = "?";
        // Short name + zone: "Glim In" / "Glim Out"
        var shortName = name.Length <= 8 ? name : name.Substring(0, 7) + "…";
        var zone = wp.IsInside ? "In" : "Out";
        return shortName + " " + zone;
    }

    /// <summary>
    /// Mirror <c>TerminalAccessibleObject.InitializeValues</c> world placement.
    /// </summary>
    private static void PositionMarker(MarkerVisual marker, Vector3 worldPos)
    {
        var rt = marker.Rt;
        var t = (Transform)rt;
        t.position = worldPos + Vector3.up * HeightOffset;
        t.position = t.position + (t.up * 1.2f - t.right * 1.2f);
    }

    private static void UpdateScale(MarkerVisual marker, bool hasMapCam, Vector3 mapCamPos)
    {
        if (!hasMapCam || marker.Go == null)
            return;

        var dist = Vector3.Distance(mapCamPos, marker.Go.transform.position);
        var mul = dist < NearScaleDistance ? NearScale : FarScale;
        var s = marker.BaseScale * mul;
        var target = new Vector3(s, s, s);
        marker.Go.transform.localScale = Vector3.Lerp(
            marker.Go.transform.localScale,
            target,
            Time.deltaTime * 10f);
    }
}
