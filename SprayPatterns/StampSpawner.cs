using System.Collections.Generic;
using UnityEngine;

namespace SprayPatterns;

/// <summary>World-space translucent quads for pattern stamps + ghost preview.</summary>
internal static class StampSpawner
{
    private static readonly List<GameObject> Pool = new();
    private static int _index;
    private static Material? _baseMat;
    private static GameObject? _preview;
    private static MeshRenderer? _previewRenderer;

    private static readonly string[] ShaderCandidates =
    {
        "Sprites/Default",
        "UI/Default",
        "Unlit/Transparent",
        "Legacy Shaders/Transparent/Diffuse",
        "HDRP/Unlit",
        "Universal Render Pipeline/Unlit",
    };

    public static Material GetSharedMaterial()
    {
        if (_baseMat != null)
            return _baseMat;

        Shader? shader = null;
        foreach (var name in ShaderCandidates)
        {
            shader = Shader.Find(name);
            if (shader != null)
            {
                Plugin.V($"Stamp shader: {name}");
                break;
            }
        }

        if (shader == null)
        {
            Plugin.Log.LogWarning("No transparent shader found; using Sprites/Default fallback create.");
            shader = Shader.Find("Hidden/InternalErrorShader") ?? Shader.Find("Standard");
        }

        _baseMat = new Material(shader!)
        {
            name = "SprayPatterns_StampMat",
            hideFlags = HideFlags.HideAndDontSave,
        };

        if (_baseMat.HasProperty("_Color"))
            _baseMat.SetColor("_Color", Color.white);
        if (_baseMat.HasProperty("_BaseColor"))
            _baseMat.SetColor("_BaseColor", Color.white);
        if (_baseMat.HasProperty("_ZWrite"))
            _baseMat.SetFloat("_ZWrite", 0f);

        // Best-effort transparent mode for HDRP Unlit
        if (_baseMat.HasProperty("_Surface"))
            _baseMat.SetFloat("_Surface", 1f);
        if (_baseMat.HasProperty("_BlendMode"))
            _baseMat.SetFloat("_BlendMode", 0f);

        return _baseMat;
    }

    public static void PlaceStamp(
        PatternId pattern,
        Vector3 position,
        Vector3 forward,
        Vector3 normal,
        float rotationDegrees,
        float size,
        Color tint,
        Transform? parent,
        bool isPreview)
    {
        if (pattern == PatternId.Vanilla)
            return;

        if (isPreview)
        {
            EnsurePreview();
            ApplyTo(_preview!, _previewRenderer!, pattern, position, forward, normal, rotationDegrees, size, tint, parent, preview: true);
            if (_preview != null && !_preview.activeSelf)
                _preview.SetActive(true);
            return;
        }

        var go = NextFromPool(parent);
        var mr = go.GetComponent<MeshRenderer>();
        ApplyTo(go, mr, pattern, position, forward, normal, rotationDegrees, size, tint, parent, preview: false);
        go.SetActive(true);
    }

    public static void HidePreview()
    {
        if (_preview != null && _preview.activeSelf)
            _preview.SetActive(false);
    }

    private static void EnsurePreview()
    {
        if (_preview != null)
            return;
        _preview = CreateQuadObject("SprayPatterns_Preview");
        Object.DontDestroyOnLoad(_preview);
        _previewRenderer = _preview.GetComponent<MeshRenderer>();
        _preview.SetActive(false);
    }

    private static GameObject NextFromPool(Transform? parent)
    {
        var max = Mathf.Max(32, Plugin.MaxStamps.Value);
        _index = (_index + 1) % max;

        while (Pool.Count <= _index)
        {
            var created = CreateQuadObject($"SprayPatterns_Stamp_{Pool.Count}");
            Object.DontDestroyOnLoad(created);
            created.SetActive(false);
            Pool.Add(created);
        }

        var go = Pool[_index];
        if (go == null)
        {
            go = CreateQuadObject($"SprayPatterns_Stamp_{_index}");
            Object.DontDestroyOnLoad(go);
            Pool[_index] = go;
        }

        if (parent != null && go.transform.parent != parent)
            go.transform.SetParent(parent, true);
        else if (parent == null && go.transform.parent != null)
            go.transform.SetParent(null, true);

        return go;
    }

    private static GameObject CreateQuadObject(string name)
    {
        var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
        go.name = name;
        Object.Destroy(go.GetComponent<Collider>());

        var mr = go.GetComponent<MeshRenderer>();
        mr.sharedMaterial = new Material(GetSharedMaterial())
        {
            name = name + "_Mat",
            hideFlags = HideFlags.HideAndDontSave,
        };
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;

        return go;
    }

    private static void ApplyTo(
        GameObject go,
        MeshRenderer mr,
        PatternId pattern,
        Vector3 position,
        Vector3 forward,
        Vector3 normal,
        float rotationDegrees,
        float size,
        Color tint,
        Transform? parent,
        bool preview)
    {
        if (parent != null && go.transform.parent != parent)
            go.transform.SetParent(parent, worldPositionStays: true);

        // Sit slightly off the surface along the spray forward (same idea as vanilla -0.1f).
        var n = normal.sqrMagnitude > 0.001f ? normal.normalized : -forward.normalized;
        go.transform.position = position + n * 0.02f;

        // Quad faces along +Z by default; aim into the surface (along spray forward).
        var face = forward.sqrMagnitude > 0.001f ? forward.normalized : -n;
        go.transform.rotation = Quaternion.LookRotation(face, Vector3.up);
        go.transform.Rotate(0f, 0f, rotationDegrees, Space.Self);
        go.transform.localScale = new Vector3(size, size, size);

        var mat = mr.material;
        var tex = PatternTextures.GetTexture(pattern);
        if (mat.HasProperty("_MainTex"))
            mat.mainTexture = tex;
        if (mat.HasProperty("_BaseColorMap"))
            mat.SetTexture("_BaseColorMap", tex);
        if (mat.HasProperty("_UnlitColorMap"))
            mat.SetTexture("_UnlitColorMap", tex);

        var color = tint;
        if (preview)
            color.a = Mathf.Clamp01(Plugin.PreviewOpacity.Value);
        else
            color.a = 1f;

        if (mat.HasProperty("_Color"))
            mat.SetColor("_Color", color);
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_UnlitColor"))
            mat.SetColor("_UnlitColor", color);
    }

    public static Transform? ResolveParent(RaycastHit hit, bool playerInElevatorOrShipPhase)
    {
        var layer = hit.collider.gameObject.layer;
        if (layer == 11 || layer == 8 || layer == 0)
        {
            if (playerInElevatorOrShipPhase || RoundManager.Instance == null)
                return StartOfRound.Instance != null ? StartOfRound.Instance.elevatorTransform : null;

            if (RoundManager.Instance.mapPropsContainer == null)
                RoundManager.Instance.mapPropsContainer = GameObject.FindGameObjectWithTag("MapPropsContainer");

            return RoundManager.Instance.mapPropsContainer != null
                ? RoundManager.Instance.mapPropsContainer.transform
                : (StartOfRound.Instance != null ? StartOfRound.Instance.elevatorTransform : null);
        }

        return hit.collider.transform;
    }
}
