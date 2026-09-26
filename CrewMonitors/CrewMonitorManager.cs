using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using GameNetcodeStuff;
using OpenBodyCams;
using OpenBodyCams.API;
using TMPro;
using UnityEngine;
using UnityEngine.Events;

namespace CrewMonitors;

/// <summary>
/// Creates per-player OpenBodyCams body cams on ship monitors (optionally
/// reusing whitelisted vanilla screens, then cloning SingleScreen panels on
/// Cube.001) and keeps assignments fresh. Each slot gets a cycle button and
/// a panel focus trigger that drives StartOfRound.mapScreen radar targeting.
/// </summary>
public sealed class CrewMonitorManager : MonoBehaviour
{
    /// <summary>CycleIndex: -2 = black Off, -1 = original/external ship cam, >=0 = living player index.</summary>
    private const int CycleOff = -2;
    private const int CycleExternal = -1;

    private const string MonitorWallPath = "Environment/HangarShip/ShipModels2b/MonitorWall";
    private const string SingleScreenPath = MonitorWallPath + "/SingleScreen";
    private const string MainWallCubeName = "Cube.001";
    private const string ClonePrefix = "CrewMonitor_Clone_";
    private const string SlotRootName = "CrewMonitors_Root";
    private const string QuadTemplateName = "CrewMonitors_QuadTemplate";

    /// <summary>
    /// Maps CloneScaleFactor so 0.6 ≈ 2× SingleScreen.localScale (prior good tile size).
    /// </summary>
    private const float SingleScreenScaleAtDefaultFactor = 2.0f;
    private const float DefaultCloneScaleFactor = 0.6f;

    /// <summary>Vanilla ManualCameraRenderer.MapCameraFocusOnPosition Y offset.</summary>
    private const float MapCameraYOffset = 3.636f;
    private const float MapCameraNearDefault = -2.47f;
    private const float MapCameraFarDefault = 7.52f;

    private static readonly string[] RejectNameTokens =
    {
        "Button",
        "Overlay",
        "Switch",
        "OnButton",
        "CameraMonitorOn",
        "CameraMonitorSwitch"
    };

    private static readonly string[] AxisNames = { "X", "Y", "Z" };

    internal static CrewMonitorManager? Instance { get; private set; }

    private readonly List<CrewSlot> _slots = new();
    private Transform? _monitorWall;
    private Transform? _placementAnchor;
    private MeshRenderer? _anchorRenderer;
    private MeshRenderer? _cloneTemplate;
    private GameObject? _ownedQuadTemplate;
    private WallPlacement _wallPlacement;
    private bool _wallPlacementValid;
    private GameObject? _slotRoot;
    private bool _setupStarted;
    private bool _setupComplete;
    private bool _refreshRequested;
    private bool _setupSoonRequested;
    private float _refreshTimer;
    private float _setupWaitElapsed;

    private void Awake()
    {
        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
        TeardownSlots();
        if (_ownedQuadTemplate != null)
        {
            Destroy(_ownedQuadTemplate);
            _ownedQuadTemplate = null;
        }
    }

    private void Start()
    {
        StartCoroutine(SetupWhenReady());
    }

    private void Update()
    {
        if (!Plugin.Enabled.Value)
        {
            if (_slots.Count > 0)
                PowerDownAll();
            return;
        }

        if (_setupSoonRequested && !_setupComplete)
        {
            _setupSoonRequested = false;
            if (!_setupStarted)
                StartCoroutine(SetupWhenReady());
        }

        if (!_setupComplete)
            return;

        float interval = Mathf.Max(0.25f, Plugin.RefreshSeconds.Value);
        _refreshTimer += Time.deltaTime;
        if (_refreshRequested || _refreshTimer >= interval)
        {
            _refreshRequested = false;
            _refreshTimer = 0f;
            RefreshAssignments();
        }
    }

    /// <summary>
    /// Keep each map-feed slot's private camera over that slot's AssignedPlayer
    /// without touching StartOfRound.mapScreen.targetTransformIndex.
    /// </summary>
    private void LateUpdate()
    {
        if (!Plugin.Enabled.Value || !_setupComplete)
            return;

        foreach (var slot in _slots)
        {
            if (!slot.ShowMapFeed || slot.AssignedPlayer == null)
                continue;
            UpdateMapFeedFollow(slot);
            ApplyMapFeedTextureToMonitor(slot);
        }
    }

    internal void RequestRefresh() => _refreshRequested = true;

    internal void RequestSetupSoon() => _setupSoonRequested = true;

    private IEnumerator SetupWhenReady()
    {
        if (_setupStarted)
            yield break;
        _setupStarted = true;

        // Wait for HangarShip + OpenBodyCams MainBodyCam (LateInit runs on ConnectClientToPlayerObject).
        _setupWaitElapsed = 0f;
        const float maxWait = 12f;
        while (_setupWaitElapsed < maxWait)
        {
            var wall = GameObject.Find(MonitorWallPath);
            bool obcReady = BodyCam.MainBodyCam != null || BodyCamComponent.GetAllBodyCams().Length > 0;
            if (wall != null && (obcReady || _setupWaitElapsed > 3f))
            {
                _monitorWall = wall.transform;
                break;
            }

            _setupWaitElapsed += 0.25f;
            yield return new WaitForSeconds(0.25f);
        }

        if (_monitorWall == null)
        {
            Plugin.Log.LogWarning("MonitorWall not found; crew monitors will not set up.");
            yield break;
        }

        try
        {
            BuildSlots();
            _setupComplete = true;
            RefreshAssignments();
            Plugin.Log.LogInfo($"Crew monitors ready with {_slots.Count} slot(s).");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"Crew monitor setup failed: {ex}");
        }
    }

    private void BuildSlots()
    {
        TeardownSlots();

        int max = Mathf.Clamp(Plugin.MaxMonitors.Value, 1, 8);
        _slotRoot = new GameObject(SlotRootName);
        _slotRoot.transform.SetParent(_monitorWall, worldPositionStays: false);
        _slotRoot.transform.localPosition = Vector3.zero;
        _slotRoot.transform.localRotation = Quaternion.identity;
        _slotRoot.transform.localScale = Vector3.one;

        ResolvePlacementAnchor();
        _wallPlacementValid = TryComputeWallPlacement(out _wallPlacement);
        var renderers = CollectReusableRenderers();
        EnsureCloneTemplate(renderers);

        int cloneOrdinal = 0;
        for (int i = 0; i < max; i++)
        {
            MeshRenderer? renderer = null;
            GameObject host;
            bool isClone = false;

            if (i < renderers.Count)
            {
                renderer = renderers[i];
                host = renderer.gameObject;
            }
            else
            {
                if (_cloneTemplate == null)
                {
                    Plugin.Log.LogWarning("No template MeshRenderer available to clone; stopping slot creation.");
                    break;
                }

                host = CloneMonitor(cloneOrdinal, out renderer);
                cloneOrdinal++;
                isClone = true;
                if (renderer == null)
                    break;
            }

            int matIndex = PickMaterialIndex(renderer);
            BodyCamComponent bodyCam;
            try
            {
                // mapRenderer null: we control targets per player (do not sync to radar).
                bodyCam = BodyCam.CreateBodyCam(host, renderer, matIndex, null);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"CreateBodyCam failed for slot {i} on {host.name}: {ex.Message}");
                if (isClone)
                    Destroy(host);
                continue;
            }

            try
            {
                float aspect = 4f / 3f;
                var b = renderer.localBounds.size;
                float fw = Mathf.Max(b.x, Mathf.Max(b.y, b.z));
                float fh = Mathf.Min(b.x, Mathf.Min(b.y, b.z));
                if (fw > 0.001f && fh > 0.001f)
                    aspect = Mathf.Clamp(fw / Mathf.Max(0.001f, fh), 0.5f, 3f);
                int h = Mathf.Max(90, Mathf.RoundToInt(480f / aspect));
                bodyCam.Resolution = new Vector2Int(480, h);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Set Resolution 480 failed: {ex.Message}");
            }

            bodyCam.SetTargetToNone();
            bodyCam.SetScreenPowered(false);
            try { bodyCam.UpdateScreenMaterial(); } catch { /* ignore */ }
            ApplyScreenBrightness(bodyCam);

            var slot = new CrewSlot
            {
                Index = i,
                Host = host,
                Renderer = renderer,
                BodyCam = bodyCam,
                IsClone = isClone,
                OriginalOffMaterial = GetMonitorOffMaterial(bodyCam),
                CycleIndex = CycleOff,
                UserCycled = false,
                Nameplate = Plugin.ShowNameplates.Value ? CreateNameplate(host, renderer) : null
            };
            _slots.Add(slot);
            SetupSlotInteractions(slot);

            var localPos = host.transform.localPosition;
            var worldPos = host.transform.position;
            string kind = isClone ? "clone" : "reuse";
            Plugin.Log.LogInfo(
                $"Slot {i} ({kind}): {host.name} parent={host.transform.parent?.name ?? "none"} " +
                $"local={localPos} world={worldPos} path={GetPath(host.transform)}");
        }
    }

    private void ResolvePlacementAnchor()
    {
        _placementAnchor = null;
        _anchorRenderer = null;
        if (_monitorWall == null)
            return;

        var cube = _monitorWall.Find(MainWallCubeName);
        if (cube != null)
        {
            _placementAnchor = cube;
            _anchorRenderer = cube.GetComponent<MeshRenderer>();
            Plugin.Log.LogInfo(
                $"Clone placement anchor: {MainWallCubeName} localPos={cube.localPosition} " +
                $"localRot={cube.localRotation.eulerAngles} localScale={cube.localScale}");
            return;
        }

        _placementAnchor = _monitorWall;
        _anchorRenderer = _monitorWall.GetComponent<MeshRenderer>();
        Plugin.Log.LogInfo($"Clone placement anchor: MonitorWall origin (no {MainWallCubeName} found)");
    }

    private bool TryComputeWallPlacement(out WallPlacement wp)
    {
        wp = default;
        if (_placementAnchor == null)
            return false;

        var mr = _anchorRenderer ?? _placementAnchor.GetComponent<MeshRenderer>();
        if (mr == null)
        {
            Plugin.Log.LogWarning("Placement anchor has no MeshRenderer; cannot compute wall bounds/depth.");
            return false;
        }

        Bounds b = mr.localBounds;
        Vector3 size = b.size;

        // Thin axis = depth into the wall.
        int depth = 0;
        if (size.y <= size.x && size.y <= size.z)
            depth = 1;
        else if (size.z <= size.x && size.z <= size.y)
            depth = 2;

        int axisA = (depth + 1) % 3;
        int axisB = (depth + 2) % 3;

        Vector3 worldA = _placementAnchor.TransformDirection(AxisUnit(axisA)).normalized;
        Vector3 worldB = _placementAnchor.TransformDirection(AxisUnit(axisB)).normalized;

        int vert;
        int horiz;
        if (Mathf.Abs(Vector3.Dot(worldA, Vector3.up)) >= Mathf.Abs(Vector3.Dot(worldB, Vector3.up)))
        {
            vert = axisA;
            horiz = axisB;
        }
        else
        {
            vert = axisB;
            horiz = axisA;
        }

        Vector3 worldH = _placementAnchor.TransformDirection(AxisUnit(horiz)).normalized;
        Vector3 worldV = _placementAnchor.TransformDirection(AxisUnit(vert)).normalized;
        float hSign = Vector3.Dot(worldH, Vector3.right) >= 0f ? 1f : -1f;
        float vSign = Vector3.Dot(worldV, Vector3.up) >= 0f ? 1f : -1f;

        Vector3 wallCenterWorld = _placementAnchor.TransformPoint(b.center);
        Vector3 cabinHint = wallCenterWorld;
        var ship = GameObject.Find("Environment/HangarShip");
        if (ship != null)
            cabinHint = ship.transform.position;

        Vector3 toCabin = cabinHint - wallCenterWorld;
        if (toCabin.sqrMagnitude < 1e-6f)
            toCabin = _monitorWall != null ? -_monitorWall.forward : Vector3.forward;
        toCabin.Normalize();

        // +local depth direction in world; room-facing (outward) face is the one whose
        // outward normal points toward the cabin.
        Vector3 depthWorld = _placementAnchor.TransformDirection(AxisUnit(depth)).normalized;
        bool maxIsOutward = Vector3.Dot(depthWorld, toCabin) > 0f;
        float outwardSign = maxIsOutward ? 1f : -1f;

        float half = b.extents[depth];
        float centerD = b.center[depth];
        float front = centerD + outwardSign * half;
        float inset = Mathf.Clamp(Plugin.WallInset.Value, -1f, 1.5f);
        // Into the wall = from room-facing face toward center (opposite outward).
        float flushDepth = front - outwardSign * inset;

        wp = new WallPlacement
        {
            HorizontalAxis = horiz,
            VerticalAxis = vert,
            DepthAxis = depth,
            HSign = hSign,
            VSign = vSign,
            OutwardSign = outwardSign,
            FlushDepth = flushDepth,
            LocalBounds = b,
            MaxIsOutward = maxIsOutward
        };

        Plugin.Log.LogInfo(
            $"Wall placement bounds: center={b.center} size={size} extents={b.extents} " +
            $"depthAxis={AxisNames[depth]} (thin) horizAxis={AxisNames[horiz]} (sign={hSign}) " +
            $"vertAxis={AxisNames[vert]} (sign={vSign}) maxIsOutward={maxIsOutward} " +
            $"outwardSign={outwardSign} front={front} flushDepth={flushDepth} inset={inset} " +
            $"cabinHint={cabinHint} wallCenterWorld={wallCenterWorld}");

        return true;
    }

    private static Vector3 AxisUnit(int axis)
    {
        return axis switch
        {
            0 => Vector3.right,
            1 => Vector3.up,
            _ => Vector3.forward
        };
    }

    private List<MeshRenderer> CollectReusableRenderers()
    {
        var result = new List<MeshRenderer>();
        var claimed = new HashSet<int>();

        void TryAdd(MeshRenderer? mr, string label)
        {
            if (mr == null)
                return;
            int id = mr.GetInstanceID();
            if (claimed.Contains(id))
                return;
            if (!IsWhitelistedScreen(mr, out string rejectReason))
            {
                Plugin.Log.LogDebug($"Skipping {label} ({mr.name}): {rejectReason}");
                return;
            }

            claimed.Add(id);
            result.Add(mr);
            Plugin.Log.LogInfo($"Reusable screen (whitelist): {label} -> {GetPath(mr.transform)}");
        }

        if (!Plugin.PreferReuseVanillaScreens.Value)
        {
            Plugin.Log.LogInfo("PreferReuseVanillaScreens=false; building clone grid only (vanilla door/internal cams left alone).");
            return result;
        }

        // Whitelist only known real screens — never auto-scan every MeshRenderer under MonitorWall.
        var single = GameObject.Find(SingleScreenPath);
        TryAdd(single != null ? single.GetComponent<MeshRenderer>() : null, "SingleScreen");

        var sor = StartOfRound.Instance;
        if (sor != null)
        {
            var insideMesh = sor.insideCameraScreen != null ? sor.insideCameraScreen.mesh : null;
            // Do not take inside cam if it is Cube.001 (OBC main panel) or already owned.
            if (insideMesh != null
                && insideMesh.gameObject.name != MainWallCubeName
                && !insideMesh.gameObject.name.Contains("Cube.001"))
            {
                TryAdd(insideMesh, "insideCameraScreen.mesh");
            }
            else if (insideMesh != null)
            {
                Plugin.Log.LogDebug($"Skipping insideCameraScreen.mesh ({insideMesh.name}): Cube.001 / main wall.");
            }

            // Never take securityCameraScreen when it is Cube.001 (OBC default).
            var securityMesh = sor.securityCameraScreen != null ? sor.securityCameraScreen.mesh : null;
            if (securityMesh != null
                && (securityMesh.gameObject.name == MainWallCubeName
                    || securityMesh.gameObject.name.Contains("Cube.001")))
            {
                Plugin.Log.LogDebug($"Skipping securityCameraScreen.mesh ({securityMesh.name}): Cube.001 reserved for OBC.");
            }
            else
            {
                Plugin.Log.LogDebug("securityCameraScreen not reused (whitelist: SingleScreen + insideCameraScreen only).");
            }
        }

        return result;
    }

    private static bool IsWhitelistedScreen(MeshRenderer mr, out string rejectReason)
    {
        rejectReason = "ok";

        if (BodyCamComponent.AnyBodyCamHasReference(mr))
        {
            rejectReason = "already owned by OpenBodyCams body cam";
            return false;
        }

        if (mr.gameObject.name == MainWallCubeName || mr.gameObject.name.Contains("Cube.001"))
        {
            rejectReason = "Cube.001 reserved for OBC main panel";
            return false;
        }

        var map = StartOfRound.Instance?.mapScreen;
        if (map != null && map.mesh == mr)
        {
            rejectReason = "radar map screen";
            return false;
        }

        string name = mr.gameObject.name;
        foreach (var token in RejectNameTokens)
        {
            if (name.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                rejectReason = $"name contains '{token}'";
                return false;
            }
        }

        if (IsUnderCube001Buttons(mr.transform))
        {
            rejectReason = "child of Cube.001 button object";
            return false;
        }

        if (name.IndexOf("Overlay", StringComparison.OrdinalIgnoreCase) >= 0
            || name.IndexOf("BodyCamOverlay", StringComparison.OrdinalIgnoreCase) >= 0)
        {
            rejectReason = "overlay mesh";
            return false;
        }

        return true;
    }

    private static bool IsUnderCube001Buttons(Transform? t)
    {
        while (t != null)
        {
            string n = t.name;
            if (n.IndexOf("CameraMonitorOn", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("CameraMonitorSwitch", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("OnButton", StringComparison.OrdinalIgnoreCase) >= 0
                || (n.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0
                    && t.parent != null
                    && (t.parent.name == MainWallCubeName || t.parent.name.Contains("Cube.001"))))
            {
                return true;
            }

            if (t.parent != null
                && (t.parent.name == MainWallCubeName || t.parent.name.Contains("Cube.001"))
                && (n.StartsWith("Cube (", StringComparison.Ordinal) || n.IndexOf("Button", StringComparison.OrdinalIgnoreCase) >= 0))
            {
                return true;
            }

            t = t.parent;
        }

        return false;
    }

    private void EnsureCloneTemplate(List<MeshRenderer> reusable)
    {
        // NEVER clone Cube.001 (full multi-mat wall). Prefer SingleScreen, then other
        // whitelisted screens, then a simple Quad.
        var single = GameObject.Find(SingleScreenPath);
        if (single != null)
        {
            var mr = single.GetComponent<MeshRenderer>();
            if (mr != null)
            {
                _cloneTemplate = mr;
                Plugin.Log.LogInfo(
                    $"Clone template: SingleScreen ({GetPath(mr.transform)}) " +
                    $"localPos={mr.transform.localPosition} localRot={mr.transform.localRotation.eulerAngles} " +
                    $"localScale={mr.transform.localScale} mats={mr.sharedMaterials?.Length ?? 0}");
                return;
            }
        }

        foreach (var mr in reusable)
        {
            if (mr == null)
                continue;
            if (mr.gameObject.name == MainWallCubeName || mr.gameObject.name.Contains("Cube.001"))
                continue;
            _cloneTemplate = mr;
            Plugin.Log.LogInfo($"Clone template: reusable {mr.name} ({GetPath(mr.transform)})");
            return;
        }

        if (_monitorWall != null)
        {
            foreach (var mr in _monitorWall.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.gameObject.name == MainWallCubeName || mr.gameObject.name.Contains("Cube.001"))
                    continue;
                if (!IsWhitelistedScreen(mr, out _))
                    continue;
                _cloneTemplate = mr;
                Plugin.Log.LogInfo($"Clone template: MonitorWall screen {mr.name} ({GetPath(mr.transform)})");
                return;
            }
        }

        _cloneTemplate = CreateQuadTemplate();
        if (_cloneTemplate != null)
        {
            Plugin.Log.LogInfo($"Clone template: fallback Quad ({GetPath(_cloneTemplate.transform)})");
            return;
        }

        Plugin.Log.LogWarning("No clone template MeshRenderer found (SingleScreen/Quad unavailable).");
    }

    private MeshRenderer? CreateQuadTemplate()
    {
        if (_ownedQuadTemplate != null)
        {
            var existing = _ownedQuadTemplate.GetComponent<MeshRenderer>();
            if (existing != null)
                return existing;
        }

        var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = QuadTemplateName;
        quad.hideFlags = HideFlags.HideAndDontSave;
        quad.SetActive(false);
        foreach (var col in quad.GetComponents<Collider>())
            Destroy(col);

        if (_monitorWall != null)
            quad.transform.SetParent(_monitorWall, worldPositionStays: false);

        _ownedQuadTemplate = quad;
        return quad.GetComponent<MeshRenderer>();
    }

    private GameObject CloneMonitor(int slotIndex, out MeshRenderer? renderer)
    {
        renderer = null;
        if (_cloneTemplate == null || _monitorWall == null)
            return null!;

        // Parent to Cube.001 so localRotation=identity matches the wall; never parent to MonitorWall
        // with Cube.001's rotated frame (that left geometry floating in the cabin).
        Transform parent = _placementAnchor != null ? _placementAnchor : _monitorWall;
        var clone = Instantiate(_cloneTemplate.gameObject, parent);
        clone.name = $"{ClonePrefix}{slotIndex}";
        clone.hideFlags = HideFlags.None;
        clone.SetActive(true);

        // Strip components that would conflict (existing BodyCam, ManualCameraRenderer, etc.).
        foreach (var cam in clone.GetComponentsInChildren<BodyCamComponent>(true))
            Destroy(cam);
        foreach (var mcr in clone.GetComponentsInChildren<ManualCameraRenderer>(true))
            Destroy(mcr);

        // Disable any colliders on the clone so panels do not block the ship.
        foreach (var collider in clone.GetComponentsInChildren<Collider>(true))
            collider.enabled = false;

        float spacing = Plugin.CloneSpacing.Value;
        float scaleFactor = Mathf.Clamp(Plugin.CloneScaleFactor.Value, 0.1f, 1f);
        int cols = Mathf.Clamp(Plugin.GridColumns.Value, 1, 8);
        int col = slotIndex % cols;
        int row = slotIndex / cols;

        // SingleScreen (or Quad) scale: CloneScaleFactor user knob → ~2× at default 0.6.
        float panelMul = scaleFactor * (SingleScreenScaleAtDefaultFactor / DefaultCloneScaleFactor);
        Vector3 baseScale = _cloneTemplate.transform.localScale * panelMul;

        clone.transform.localScale = baseScale;

        Vector3 localPos;
        if (_wallPlacementValid)
        {
            var wp = _wallPlacement;
            float xBase = Plugin.GridHorizontalOffset.Value;
            float yBase = Plugin.GridVerticalOffset.Value;
            float xOffset = xBase + (col - (cols - 1) * 0.5f) * spacing;
            float yOffset = yBase + row * spacing * 0.85f;

            localPos = wp.LocalBounds.center;
            localPos[wp.HorizontalAxis] = wp.LocalBounds.center[wp.HorizontalAxis] + wp.HSign * xOffset;
            localPos[wp.VerticalAxis] = wp.LocalBounds.center[wp.VerticalAxis] + wp.VSign * yOffset;
            localPos[wp.DepthAxis] = wp.FlushDepth;
        }
        else
        {
            // Fallback if bounds unavailable: simple XY grid + small Z inset.
            float xBase = Plugin.GridHorizontalOffset.Value;
            float yBase = Plugin.GridVerticalOffset.Value;
            int colsFb = Mathf.Clamp(Plugin.GridColumns.Value, 1, 8);
            localPos = new Vector3(
                xBase + (col - (colsFb - 1) * 0.5f) * spacing,
                yBase + row * spacing * 0.85f,
                -Plugin.WallInset.Value);
            Plugin.Log.LogWarning($"Clone {slotIndex}: wall placement bounds unavailable; using fallback localPos={localPos}");
        }

        clone.transform.localPosition = localPos;

        // Rotate about local Z (panel plane / ship local Z after parenting to Cube.001).
        clone.transform.localRotation = Quaternion.Euler(0f, 0f, Plugin.GridYawDegrees.Value);

        renderer = clone.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = clone.GetComponentInChildren<MeshRenderer>();

        string depthInfo = _wallPlacementValid
            ? $"depthAxis={AxisNames[_wallPlacement.DepthAxis]} flushDepth={_wallPlacement.FlushDepth} " +
              $"boundsCenter={_wallPlacement.LocalBounds.center} boundsSize={_wallPlacement.LocalBounds.size} " +
              $"maxIsOutward={_wallPlacement.MaxIsOutward}"
            : "depthAxis=n/a";

        Plugin.Log.LogInfo(
            $"Cloned monitor slot {slotIndex}: template={_cloneTemplate.gameObject.name} " +
            $"parent={parent.name} col={col} row={row} " +
            $"localPos={clone.transform.localPosition} localRot={clone.transform.localRotation.eulerAngles} " +
            $"localScale={clone.transform.localScale} worldPos={clone.transform.position} " +
            $"inset={Plugin.WallInset.Value} scaleFactor={scaleFactor} panelMul={panelMul} " +
            $"spacing={spacing} xBase={Plugin.GridHorizontalOffset.Value} yBase={Plugin.GridVerticalOffset.Value} yaw={Plugin.GridYawDegrees.Value} " +
            $"{depthInfo}");
        return clone;
    }

    private static int PickMaterialIndex(MeshRenderer renderer)
    {
        var mats = renderer.sharedMaterials;
        if (mats == null || mats.Length == 0)
            return 0;

        string name = renderer.gameObject.name;
        bool isCube001 = name == MainWallCubeName || name.Contains("Cube.001");

        // OpenBodyCams: Cube.001 screen slot = 2; SingleScreen = 1 (OBC README).
        // Clones are SingleScreen/Quad geometry — never treat ClonePrefix as Cube.001.
        int picked;
        if (isCube001 && mats.Length > 2)
            picked = 2;
        else if (mats.Length > 1)
            picked = 1;
        else
            picked = 0;

        Plugin.Log.LogInfo(
            $"PickMaterialIndex({name}): index={picked} (mats={mats.Length}, isCube001={isCube001})");
        return picked;
    }

    private TextMeshPro? CreateNameplate(GameObject host, MeshRenderer renderer)
    {
        try
        {
            var go = new GameObject("CrewMonitor_Nameplate");
            go.transform.SetParent(host.transform, worldPositionStays: false);
            var bounds = renderer.localBounds;
            go.transform.localPosition = new Vector3(0f, bounds.min.y - 0.05f, bounds.center.z);
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one * 0.02f;

            var tmp = go.AddComponent<TextMeshPro>();
            tmp.text = "";
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.fontSize = 36f;
            tmp.color = new Color(1f, 0.55f, 0.15f, 1f);
            tmp.enableAutoSizing = false;
            tmp.rectTransform.sizeDelta = new Vector2(20f, 4f);

            var font = StartOfRound.Instance?.mapScreenPlayerName?.font;
            if (font != null)
                tmp.font = font;

            return tmp;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Nameplate create failed: {ex.Message}");
            return null;
        }
    }

    private void RefreshAssignments()
    {
        if (!_setupComplete || _slots.Count == 0)
            return;

        var players = GetLivingPlayers();
        bool alwaysShow = Plugin.AlwaysShowMonitors.Value;
        // Hard cap: only as many auto player feeds as monitor slots. No 5th+ / round-robin.
        int autoPlayerSlots = Mathf.Min(players.Count, _slots.Count);

        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot.BodyCam == null)
                continue;

            if (slot.UserCycled)
            {
                if (slot.CycleIndex <= CycleOff)
                {
                    ApplyManualOff(slot, alwaysShow);
                    UpdateInteractionTips(slot);
                    continue;
                }
                if (slot.CycleIndex == CycleExternal)
                {
                    ApplyExternalCam(slot, alwaysShow);
                    UpdateInteractionTips(slot);
                    continue;
                }

                PlayerControllerB? cycled = null;
                if (slot.CycleIndex >= 0 && slot.CycleIndex < players.Count)
                    cycled = players[slot.CycleIndex];
                else if (slot.AssignedPlayer != null && IsLivingPlayer(slot.AssignedPlayer))
                    cycled = slot.AssignedPlayer;

                if (cycled != null)
                {
                    if (slot.ShowMapFeed)
                        ApplyMapFeed(slot, cycled, alwaysShow);
                    else
                        ApplySlotAssignment(slot, cycled, alwaysShow, blankLabel: $"Slot {i + 1}");
                }
                else
                {
                    slot.CycleIndex = CycleOff;
                    slot.ShowMapFeed = false;
                    ApplyManualOff(slot, alwaysShow);
                }
                UpdateInteractionTips(slot);
                continue;
            }

            // Auto from bottom (slot 0): body cams only; extras stay Off.
            if (i < autoPlayerSlots)
            {
                slot.CycleIndex = i;
                slot.ShowMapFeed = false;
                ApplySlotAssignment(slot, players[i], alwaysShow, blankLabel: $"Slot {i + 1}");
            }
            else
            {
                slot.CycleIndex = CycleOff;
                slot.ShowMapFeed = false;
                ApplyManualOff(slot, alwaysShow);
            }
            UpdateInteractionTips(slot);
        }
    }

    /// <summary>
    /// CycleIndex &lt; 0 means the player manually powered this slot Off.
    /// AlwaysShowMonitors still keeps the panel mesh visible; screen stays black.
    /// </summary>
    private static void ApplyManualOff(CrewSlot slot, bool alwaysShow)
    {
        slot.AssignedPlayer = null;
        slot.ShowMapFeed = false;
        StopMapFeedCamera(slot);
        slot.BodyCam.SetTargetToNone();
        SetMonitorOffMaterial(slot.BodyCam, GetBlackScreenMaterial());
        if (slot.BodyCam.IsScreenPowered())
            slot.BodyCam.SetScreenPowered(false);
        else
            slot.BodyCam.UpdateScreenMaterial();
        if (slot.IsClone && slot.Host != null)
        {
            if (alwaysShow && !slot.Host.activeSelf)
                slot.Host.SetActive(true);
            else if (!alwaysShow && slot.Host.activeSelf)
                slot.Host.SetActive(false);
        }

        if (slot.Nameplate != null)
        {
            if (alwaysShow)
            {
                slot.Nameplate.text = "Off";
                slot.Nameplate.gameObject.SetActive(true);
            }
            else
            {
                slot.Nameplate.text = "";
                slot.Nameplate.gameObject.SetActive(false);
            }
        }
    }

    /// <summary>
    /// Show the cloned panel's original feed (vanilla external/ship cam) by powering the
    /// body cam off so OpenBodyCams restores MonitorOffMaterial.
    /// </summary>
    private static void ApplyExternalCam(CrewSlot slot, bool alwaysShow)
    {
        slot.AssignedPlayer = null;
        slot.ShowMapFeed = false;
        StopMapFeedCamera(slot);
        slot.BodyCam.SetTargetToNone();
        if (slot.OriginalOffMaterial != null)
            SetMonitorOffMaterial(slot.BodyCam, slot.OriginalOffMaterial);
        if (slot.BodyCam.IsScreenPowered())
            slot.BodyCam.SetScreenPowered(false);
        else
            slot.BodyCam.UpdateScreenMaterial();
        if (slot.IsClone && slot.Host != null)
        {
            if (alwaysShow && !slot.Host.activeSelf)
                slot.Host.SetActive(true);
            else if (!alwaysShow && slot.Host.activeSelf)
                slot.Host.SetActive(false);
        }
        if (slot.Nameplate != null)
        {
            if (alwaysShow)
            {
                slot.Nameplate.text = "External";
                slot.Nameplate.gameObject.SetActive(true);
            }
            else
            {
                slot.Nameplate.text = "";
                slot.Nameplate.gameObject.SetActive(false);
            }
        }
    }


    /// <summary>
    /// Show a per-slot radar/map camera over AssignedPlayer without stealing mapScreen's target.
    /// </summary>
    private void ApplyMapFeed(CrewSlot slot, PlayerControllerB player, bool alwaysShow)
    {
        slot.AssignedPlayer = player;
        slot.ShowMapFeed = true;
        slot.BodyCam.SetTargetToNone();

        if (slot.IsClone && slot.Host != null)
        {
            if (alwaysShow && !slot.Host.activeSelf)
                slot.Host.SetActive(true);
            else if (!alwaysShow && slot.Host.activeSelf)
                slot.Host.SetActive(false);
        }

        slot.BodyCam.SetScreenPowered(true);
        ApplyScreenBrightness(slot.BodyCam);
        EnsureMapFeedCamera(slot);
        UpdateMapFeedFollow(slot);
        ApplyMapFeedTextureToMonitor(slot);

        if (slot.Nameplate != null)
        {
            string name = string.IsNullOrEmpty(player.playerUsername)
                ? $"Player {player.playerClientId}"
                : player.playerUsername;
            slot.Nameplate.text = $"{name} Map";
            slot.Nameplate.gameObject.SetActive(true);
        }
    }

    /// <summary>
    /// Create (once) a private map follower camera + RenderTexture for this slot.
    /// Does not call SwitchRadarTargetAndSync / mutate mapScreen.targetTransformIndex.
    /// </summary>
    private void EnsureMapFeedCamera(CrewSlot slot)
    {
        var map = StartOfRound.Instance?.mapScreen;
        var src = map?.mapCamera;
        if (src == null)
        {
            Plugin.Log.LogWarning($"Slot {slot.Index}: mapCamera unavailable for map feed.");
            return;
        }

        if (slot.MapFeedCamera != null && slot.MapFeedTexture != null)
        {
            if (!slot.MapFeedCamera.gameObject.activeSelf)
                slot.MapFeedCamera.gameObject.SetActive(true);
            slot.MapFeedCamera.enabled = true;
            return;
        }

        DestroyMapFeedResources(slot);

        Transform parent = _slotRoot != null ? _slotRoot.transform : transform;
        var go = new GameObject($"CrewMapFeedCam_{slot.Index}");
        go.transform.SetParent(parent, false);
        go.transform.SetPositionAndRotation(src.transform.position, src.transform.rotation);
        go.transform.localScale = Vector3.one;

        var cam = go.AddComponent<Camera>();
        CopyMapCameraSettings(cam, src);

        int width = 480;
        int height = 360;
        try
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var bodyCam = typeof(BodyCamComponent).GetField("Camera", flags)?.GetValue(slot.BodyCam) as Camera;
            if (bodyCam?.targetTexture != null)
            {
                width = Mathf.Max(16, bodyCam.targetTexture.width);
                height = Mathf.Max(16, bodyCam.targetTexture.height);
            }
        }
        catch
        {
            // keep defaults
        }

        var rt = new RenderTexture(width, height, 24)
        {
            name = $"CrewMapFeedRT_{slot.Index}",
            filterMode = FilterMode.Bilinear,
            antiAliasing = 1
        };
        rt.Create();
        cam.targetTexture = rt;
        cam.enabled = true;

        TryConfigureHdrpMapCamera(cam, src);

        slot.MapFeedCamera = cam;
        slot.MapFeedTexture = rt;
        Plugin.Log.LogInfo($"Slot {slot.Index}: dedicated map feed camera ready ({width}x{height}).");
    }

    private static void CopyMapCameraSettings(Camera dest, Camera src)
    {
        dest.orthographic = src.orthographic;
        dest.orthographicSize = src.orthographicSize;
        dest.fieldOfView = src.fieldOfView;
        dest.nearClipPlane = src.nearClipPlane;
        dest.farClipPlane = src.farClipPlane;
        dest.cullingMask = src.cullingMask;
        dest.clearFlags = src.clearFlags;
        dest.backgroundColor = src.backgroundColor;
        dest.depth = src.depth - 1f;
        dest.allowHDR = src.allowHDR;
        dest.allowMSAA = src.allowMSAA;
        dest.useOcclusionCulling = src.useOcclusionCulling;
        dest.renderingPath = src.renderingPath;
        dest.aspect = src.aspect;
    }

    private static void TryConfigureHdrpMapCamera(Camera dest, Camera src)
    {
        try
        {
            var srcHd = src.GetComponent("HDAdditionalCameraData");
            if (srcHd == null)
                return;

            var hdType = srcHd.GetType();
            var destHd = dest.GetComponent(hdType) ?? dest.gameObject.AddComponent(hdType);

            foreach (var fieldName in new[]
                     {
                         "volumeLayerMask", "probeLayerMask", "clearColorMode",
                         "backgroundColorHDR", "customRenderingSettings",
                         "volumeAnchorOverride"
                     })
            {
                var f = hdType.GetField(fieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                if (f == null || f.IsInitOnly || f.IsLiteral)
                    continue;
                try { f.SetValue(destHd, f.GetValue(srcHd)); }
                catch { /* ignore unreadable/unwritable */ }
            }

            var hist = hdType.GetField("hasPersistentHistory", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            hist?.SetValue(destHd, true);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogDebug($"HDRP map cam configure skipped: {ex.Message}");
        }
    }

    private void UpdateMapFeedFollow(CrewSlot slot)
    {
        var cam = slot.MapFeedCamera;
        var player = slot.AssignedPlayer;
        if (cam == null || player == null)
            return;

        Vector3 pos;
        if (player.isPlayerDead)
        {
            if (player.redirectToEnemy != null)
                pos = player.redirectToEnemy.transform.position;
            else if (player.deadBody != null && player.deadBody.gameObject.activeSelf)
                pos = player.deadBody.transform.position;
            else
                pos = player.placeOfDeath;
        }
        else
        {
            pos = player.transform.position;
            var mask = StartOfRound.Instance != null
                ? StartOfRound.Instance.collidersAndRoomMask
                : Physics.DefaultRaycastLayers;
            if (Physics.Raycast(
                    player.transform.position + Vector3.up * 0.1f,
                    Vector3.down,
                    out RaycastHit hit,
                    5f,
                    mask,
                    QueryTriggerInteraction.Ignore))
            {
                pos = hit.point + Vector3.up * 0.06f;
            }
        }

        var map = StartOfRound.Instance?.mapScreen;
        float near = map != null ? map.cameraNearPlane : MapCameraNearDefault;
        float far = map != null ? map.cameraFarPlane : MapCameraFarDefault;

        // Mirror ManualCameraRenderer.MapCameraFocusOnPosition clip planes (do not touch radarCanvas).
        if (player.isInHangarShipRoom)
        {
            cam.nearClipPlane = -0.96f;
            cam.farClipPlane = 7.52f;
        }
        else if (!player.isInsideFactory)
        {
            cam.nearClipPlane = near - 18f;
            cam.farClipPlane = far + 18f;
        }
        else
        {
            cam.nearClipPlane = near;
            cam.farClipPlane = far;
        }

        cam.transform.position = new Vector3(pos.x, pos.y + MapCameraYOffset, pos.z);

        var src = map?.mapCamera;
        if (src != null)
            cam.transform.rotation = src.transform.rotation;
    }

    private static void ApplyMapFeedTextureToMonitor(CrewSlot slot)
    {
        if (slot.MapFeedTexture == null || slot.BodyCam == null)
            return;

        try
        {
            var mat = GetMonitorOnMaterial(slot.BodyCam);
            if (mat != null)
                mat.mainTexture = slot.MapFeedTexture;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"ApplyMapFeedTextureToMonitor failed: {ex.Message}");
        }
    }

    private static void StopMapFeedCamera(CrewSlot slot)
    {
        if (slot.MapFeedCamera == null)
            return;
        slot.MapFeedCamera.enabled = false;
        if (slot.MapFeedCamera.gameObject.activeSelf)
            slot.MapFeedCamera.gameObject.SetActive(false);
    }

    private static void DestroyMapFeedResources(CrewSlot slot)
    {
        if (slot.MapFeedCamera != null)
        {
            Destroy(slot.MapFeedCamera.gameObject);
            slot.MapFeedCamera = null;
        }

        if (slot.MapFeedTexture != null)
        {
            if (slot.MapFeedTexture.IsCreated())
                slot.MapFeedTexture.Release();
            Destroy(slot.MapFeedTexture);
            slot.MapFeedTexture = null;
        }
    }

    private static Material? GetMonitorOnMaterial(BodyCamComponent bodyCam)
    {
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        return typeof(BodyCamComponent).GetField("MonitorOnMaterial", flags)?.GetValue(bodyCam) as Material;
    }

    /// <summary>
    /// Map feed swaps MonitorOnMaterial.mainTexture to the radar RT. Leaving map mode must
    /// put the body-cam RenderTexture back or both cycle steps keep showing the map.
    /// </summary>
    private static void RestoreBodyCamTexture(BodyCamComponent bodyCam)
    {
        if (bodyCam == null)
            return;
        try
        {
            var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            var cam = typeof(BodyCamComponent).GetField("Camera", flags)?.GetValue(bodyCam) as Camera;
            var mat = GetMonitorOnMaterial(bodyCam);
            if (mat == null)
                return;
            if (cam != null && cam.targetTexture != null)
                mat.mainTexture = cam.targetTexture;
            try { bodyCam.UpdateScreenMaterial(); } catch { /* ignore */ }
            // UpdateScreenMaterial can leave color/emissive; re-assert bodycam RT after.
            if (cam != null && cam.targetTexture != null)
                mat.mainTexture = cam.targetTexture;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"RestoreBodyCamTexture failed: {ex.Message}");
        }
    }

    private void ApplySlotAssignment(CrewSlot slot, PlayerControllerB? player, bool alwaysShow, string blankLabel)
    {
        if (player != null)
        {
            bool wasMap = slot.ShowMapFeed;
            slot.ShowMapFeed = false;
            if (wasMap)
                StopMapFeedCamera(slot);
            slot.BodyCam.SetTargetToPlayer(player);
            slot.AssignedPlayer = player;

            slot.BodyCam.SetScreenPowered(true);
            ApplyScreenBrightness(slot.BodyCam);
            // Map mode overwrites MonitorOnMaterial.mainTexture; always restore body-cam RT.
            RestoreBodyCamTexture(slot.BodyCam);
            if (wasMap)
                Plugin.Log.LogInfo($"Slot restored body-cam RT after map feed.");
            if (slot.IsClone && slot.Host != null && !slot.Host.activeSelf)
                slot.Host.SetActive(true);

            UpdateNameplate(slot, player);
        }
        else if (alwaysShow)
        {
            slot.AssignedPlayer = null;
            slot.BodyCam.SetTargetToNone();
            slot.BodyCam.SetScreenPowered(true);
            ApplyScreenBrightness(slot.BodyCam);
            if (slot.IsClone && slot.Host != null && !slot.Host.activeSelf)
                slot.Host.SetActive(true);
            if (slot.Nameplate != null)
            {
                slot.Nameplate.text = blankLabel;
                slot.Nameplate.gameObject.SetActive(true);
            }
        }
        else
        {
            slot.AssignedPlayer = null;
            slot.BodyCam.SetTargetToNone();
            slot.BodyCam.SetScreenPowered(false);

            if (slot.IsClone && slot.Host != null && slot.Host.activeSelf)
                slot.Host.SetActive(false);

            if (slot.Nameplate != null)
            {
                slot.Nameplate.text = "";
                slot.Nameplate.gameObject.SetActive(false);
            }
        }
    }

    private static void UpdateNameplate(CrewSlot slot, PlayerControllerB player)
    {
        if (slot.Nameplate == null)
            return;
        slot.Nameplate.gameObject.SetActive(true);
        slot.Nameplate.text = string.IsNullOrEmpty(player.playerUsername)
            ? $"Player {player.playerClientId}"
            : player.playerUsername;
    }

    private static bool IsLivingPlayer(PlayerControllerB p)
    {
        return p != null && p.isPlayerControlled && !p.isPlayerDead;
    }

    private static List<PlayerControllerB> GetLivingPlayers()
    {
        var sor = StartOfRound.Instance;
        if (sor == null || sor.allPlayerScripts == null)
            return new List<PlayerControllerB>();

        IEnumerable<PlayerControllerB> q = sor.allPlayerScripts
            .Where(p => p != null && p.isPlayerControlled && !p.isPlayerDead);

        if (!Plugin.IncludeLocalPlayer.Value)
        {
            var local = GameNetworkManager.Instance?.localPlayerController;
            if (local != null)
                q = q.Where(p => p != local);
        }

        return q.OrderBy(p => p.playerClientId).ToList();
    }

    private void SetupSlotInteractions(CrewSlot slot)
    {
        if (slot.Host == null || slot.Renderer == null)
            return;

        try
        {
            // Focus covers most of the panel (name tip + click-to-focus) but sits behind the cycle
            // button in depth and clears the bottom-right corner so selectors stay clickable.
            slot.FocusTrigger = CreateFocusTrigger(slot);
            slot.CycleButton = CreateCycleButton(slot);
            UpdateInteractionTips(slot);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Slot {slot.Index} interaction setup failed: {ex.Message}");
        }
    }

    private GameObject CreateFocusTrigger(CrewSlot slot)
    {
        var go = new GameObject("CrewMonitor_FocusTrigger");
        go.transform.SetParent(slot.Host.transform, worldPositionStays: false);
        go.tag = "InteractTrigger";

        var vanilla = FindVanillaSwitchButton();
        int layer = go.layer;
        if (vanilla != null)
        {
            var vt = vanilla.GetComponentInChildren<InteractTrigger>(true);
            layer = vt != null ? vt.gameObject.layer : vanilla.layer;
        }
        if (layer <= 0 || layer == 8 || layer == 30)
        {
            int named = LayerMask.NameToLayer("InteractableObject");
            layer = named >= 0 ? named : 9;
        }
        go.layer = layer;

        Bounds b = slot.Renderer.localBounds;
        GetScreenFaceAxes(b, slot.Renderer.transform, out int horiz, out int vert, out int depth);
        float outward = GetOutwardLocalOnAxis(slot.Renderer, depth);
        float depthCenter = AxisComponent(b.center, depth);
        float towardCabin = Mathf.Sign(outward - depthCenter);
        if (towardCabin == 0f)
            towardCabin = 1f;

        // Sit slightly behind the cycle button (button uses +0.12 toward cabin).
        float focusDepth = outward + towardCabin * 0.04f;

        Vector3 center = b.center;
        SetAxisComponent(ref center, depth, (AxisComponent(b.center, depth) + focusDepth) * 0.5f);
        // Nudge center away from bottom-right where the cycle pad lives.
        SetAxisComponent(ref center, horiz, AxisComponent(b.center, horiz) - AxisComponent(b.extents, horiz) * 0.08f);
        SetAxisComponent(ref center, vert, AxisComponent(b.center, vert) + AxisComponent(b.extents, vert) * 0.06f);

        var box = go.AddComponent<BoxCollider>();
        box.center = center;
        Vector3 size = b.size;
        SetAxisComponent(ref size, horiz, Mathf.Max(0.05f, AxisComponent(b.size, horiz) * 0.72f));
        SetAxisComponent(ref size, vert, Mathf.Max(0.05f, AxisComponent(b.size, vert) * 0.68f));
        SetAxisComponent(ref size, depth, Mathf.Max(0.02f, Mathf.Abs(focusDepth - AxisComponent(b.center, depth)) * 0.7f));
        box.size = size;
        box.isTrigger = false;

        var trigger = ConfigureInteractTrigger(go, "Focus");
        trigger.hoverIcon = GetTransparentHoverIcon();
        trigger.disabledHoverIcon = GetTransparentHoverIcon();
        var bridge = go.AddComponent<CrewSlotInteractBridge>();
        bridge.Init(this, slot.Index, isCycleButton: false);
        trigger.onInteract.AddListener(bridge.OnInteract);

        return go;
    }

    /// <summary>
    /// OpenBodyCams CreateBodyCam starts MonitorOnMaterial at _EmissiveColor=white, which blooms into
    /// a near-inverted milky image on small clones. MainBodyCam later applies MonitorEmissiveColor
    /// (default 0.05,0.13,0.05). Match that. ScreenBrightness only scales night-vision fill.
    /// </summary>
    private static void ApplyScreenBrightness(BodyCamComponent bodyCam)
    {
        if (bodyCam == null)
            return;

        float nv = Mathf.Clamp(Plugin.ScreenBrightness.Value, 0f, 1f);
        var flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
        var type = typeof(BodyCamComponent);

        try
        {
            var matField = type.GetField("MonitorOnMaterial", flags);
            var mat = matField?.GetValue(bodyCam) as Material;
            if (mat != null)
            {
                // Albedo stays white so the render texture drives the picture.
                mat.color = Color.white;
                if (mat.HasProperty("_EmissiveColor"))
                    mat.SetColor("_EmissiveColor", GetBodyCamEmissiveColor());
                if (mat.HasProperty("_AlbedoAffectEmissive"))
                    mat.SetFloat("_AlbedoAffectEmissive", 1f);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Screen material emissive tune failed: {ex.Message}");
        }

        try
        {
            var lightField = type.GetField("nightVisionLight", flags);
            var light = lightField?.GetValue(bodyCam) as Light;
            if (light != null)
            {
                // OpenBodyCams stock: intensity = 367 * NightVisionBrightness, range = 12 * NightVisionBrightness.
                light.intensity = 367f * nv;
                light.range = 12f * nv;
                light.enabled = nv > 0.001f;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"ScreenBrightness nightVision tune failed: {ex.Message}");
        }
    }

    private static Color GetBodyCamEmissiveColor()
    {
        try
        {
            var pluginType = typeof(BodyCamComponent).Assembly.GetType("OpenBodyCams.Plugin");
            var method = pluginType?.GetMethod(
                "GetBodyCamEmissiveColor",
                BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            if (method != null && method.Invoke(null, null) is Color c)
                return c;
        }
        catch
        {
            /* fall through */
        }

        // Same default as Zaggy1024.OpenBodyCams MonitorEmissiveColor (alpha 0 per OBC ParseColor).
        return new Color(0.05f, 0.13f, 0.05f, 0f);
    }


    private GameObject CreateCycleButton(CrewSlot slot)
    {
        var renderer = slot.Renderer;
        Bounds localB = renderer.localBounds;
        GetScreenFaceAxes(localB, renderer.transform, out int horiz, out int vert, out int depth);

        float faceW = AxisComponent(localB.size, horiz);
        float faceH = AxisComponent(localB.size, vert);
        float faceMin = Mathf.Min(faceW, faceH);
        float buttonSize = Mathf.Clamp(faceMin * 0.15f, 0.06f, 0.22f);

        Vector3 faceLocal = localB.center;
        SetAxisComponent(ref faceLocal, horiz, AxisComponent(localB.max, horiz) - buttonSize * 0.65f - 0.07f); // left +0.02
        SetAxisComponent(ref faceLocal, vert, AxisComponent(localB.min, vert) + buttonSize * 0.65f + 0.3f);

        float outward = GetOutwardLocalOnAxis(renderer, depth);
        float depthCenter = AxisComponent(localB.center, depth);
        float towardCabin = Mathf.Sign(outward - depthCenter);
        if (towardCabin == 0f)
            towardCabin = 1f;
        SetAxisComponent(ref faceLocal, depth, outward + towardCabin * 0.02f); // back another 0.04

        Vector3 worldPos = renderer.transform.TransformPoint(faceLocal);
        Vector3 hostLocal = slot.Host.transform.InverseTransformPoint(worldPos);

        // World-space direction from button toward cabin (along screen depth).
        Vector3 depthLocalUnit = AxisUnit(depth) * towardCabin;
        Vector3 towardCabinWorld = renderer.transform.TransformDirection(depthLocalUnit).normalized;

        // 50% of prior 0.13 → 0.065
        const float targetWorldSize = 0.065f;

        // Simple root — do NOT clone vanilla switch (its InteractTrigger/colliders fight ours).
        var go = new GameObject("CrewMonitor_CycleButton");
        go.transform.SetParent(slot.Host.transform, worldPositionStays: false);
        go.transform.localPosition = hostLocal;
        go.transform.localRotation = Quaternion.identity;
        var lossy = slot.Host.transform.lossyScale;
        go.transform.localScale = new Vector3(
            targetWorldSize / Mathf.Max(0.001f, Mathf.Abs(lossy.x)),
            targetWorldSize / Mathf.Max(0.001f, Mathf.Abs(lossy.y)),
            targetWorldSize / Mathf.Max(0.001f, Mathf.Abs(lossy.z)));

        // LC interact raycast mask = layers 6/8/9/30, and 8/30 are ignored for hover.
        // Vanilla CameraMonitorSwitchButton root can report layer 0; force a hit-able interact layer.
        var vanilla = FindVanillaSwitchButton();
        int interactLayer = -1;
        if (vanilla != null)
        {
            var vt = vanilla.GetComponentInChildren<InteractTrigger>(true);
            if (vt != null)
                interactLayer = vt.gameObject.layer;
            if (interactLayer <= 0)
                interactLayer = vanilla.layer;
        }
        if (interactLayer <= 0 || interactLayer == 8 || interactLayer == 30)
        {
            int named = LayerMask.NameToLayer("InteractableObject");
            interactLayer = named >= 0 ? named : 9;
        }
        go.layer = interactLayer;
        go.tag = "InteractTrigger";

        // Align local +Y with toward-cabin so cylinders point out of the wall (horizontal nub).
        Quaternion faceOut = Quaternion.FromToRotation(Vector3.up, go.transform.InverseTransformDirection(towardCabinWorld));

        // Grey bezel body (cylinder along +Y = toward cabin).
        var body = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        body.name = "CyclePadBody";
        body.transform.SetParent(go.transform, worldPositionStays: false);
        body.transform.localRotation = faceOut;
        body.transform.localPosition = faceOut * new Vector3(0f, -0.15f, 0f);
        body.transform.localScale = new Vector3(0.85f, 0.35f, 0.85f);
        UnityEngine.Object.Destroy(body.GetComponent<Collider>());
        ApplyBezelGreyMaterial(body);
        foreach (var mr in body.GetComponentsInChildren<MeshRenderer>(true))
        {
            mr.enabled = true;
            mr.forceRenderingOff = false;
        }

        // White circular face at the cabin end of the cylinder.
        var face = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        face.name = "CyclePadFace";
        face.transform.SetParent(go.transform, worldPositionStays: false);
        face.transform.localRotation = faceOut;
        face.transform.localPosition = faceOut * new Vector3(0f, 0.22f, 0f);
        face.transform.localScale = new Vector3(1f, 0.06f, 1f);
        UnityEngine.Object.Destroy(face.GetComponent<Collider>());
        ApplyWhiteButtonMaterial(face);
        foreach (var mr in face.GetComponentsInChildren<MeshRenderer>(true))
        {
            mr.enabled = true;
            mr.forceRenderingOff = false;
        }

        // CRITICAL: LC looks up InteractTrigger on the SAME GameObject as the hit collider
        // (PlayerControllerB.SetHoverTipAndCurrentInteractTrigger → hit.transform.gameObject.GetComponent<InteractTrigger>()).
        var hit = new GameObject("CycleHitbox");
        hit.transform.SetParent(go.transform, worldPositionStays: false);
        hit.transform.localPosition = Vector3.zero;
        hit.transform.localRotation = Quaternion.identity;
        hit.transform.localScale = Vector3.one * 2.5f;
        hit.layer = go.layer;
        hit.tag = "InteractTrigger";
        var hitBox = hit.AddComponent<BoxCollider>();
        hitBox.enabled = true;
        hitBox.isTrigger = false;
        hitBox.size = Vector3.one;

        var trigger = ConfigureInteractTrigger(hit, "Cycle crew cam : [E]");
        var bridge = hit.AddComponent<CrewSlotInteractBridge>();
        bridge.Init(this, slot.Index, isCycleButton: true);
        trigger.onInteract.AddListener(bridge.OnInteract);
        if (trigger.onInteractEarly != null)
            trigger.onInteractEarly.AddListener(bridge.OnInteract);

        var fallback = hit.AddComponent<CrewButtonRaycastFallback>();
        fallback.Init(this, slot.Index, hitBox, trigger);

        Plugin.Log.LogInfo(
            $"Slot {slot.Index} cycle button: worldSize={targetWorldSize:F3} " +
            $"localPos={go.transform.localPosition} worldPos={go.transform.position} " +
            $"towardCabin={towardCabinWorld} layer={go.layer}({LayerMask.LayerToName(go.layer)}) " +
            $"triggerOnHitbox=true host={slot.Host.name}");

        return go;
    }


private static void GetScreenFaceAxes(
        Bounds localBounds,
        Transform t,
        out int horiz,
        out int vert,
        out int depth)
    {
        Vector3 size = localBounds.size;
        depth = 0;
        if (size.y <= size.x && size.y <= size.z)
            depth = 1;
        else if (size.z <= size.x && size.z <= size.y)
            depth = 2;

        int axisA = (depth + 1) % 3;
        int axisB = (depth + 2) % 3;
        Vector3 worldA = t.TransformDirection(AxisUnit(axisA)).normalized;
        Vector3 worldB = t.TransformDirection(AxisUnit(axisB)).normalized;
        if (Mathf.Abs(Vector3.Dot(worldA, Vector3.up)) >= Mathf.Abs(Vector3.Dot(worldB, Vector3.up)))
        {
            vert = axisA;
            horiz = axisB;
        }
        else
        {
            vert = axisB;
            horiz = axisA;
        }
    }

    private static float AxisComponent(Vector3 v, int axis) =>
        axis switch { 0 => v.x, 1 => v.y, _ => v.z };

    private static void SetAxisComponent(ref Vector3 v, int axis, float value)
    {
        switch (axis)
        {
            case 0: v.x = value; break;
            case 1: v.y = value; break;
            default: v.z = value; break;
        }
    }

    /// <summary>
    /// Local depth-axis coordinate of the cabin-facing screen face, nudged slightly outward.
    /// </summary>
    private static float GetOutwardLocalOnAxis(MeshRenderer renderer, int depthAxis)
    {
        Bounds b = renderer.localBounds;
        float min = AxisComponent(b.min, depthAxis);
        float max = AxisComponent(b.max, depthAxis);
        Vector3 c = b.center;
        Vector3 localMin = c;
        Vector3 localMax = c;
        SetAxisComponent(ref localMin, depthAxis, min);
        SetAxisComponent(ref localMax, depthAxis, max);
        Vector3 worldMin = renderer.transform.TransformPoint(localMin);
        Vector3 worldMax = renderer.transform.TransformPoint(localMax);

        Vector3 cabin = worldMin;
        var ship = GameObject.Find("Environment/HangarShip");
        if (ship != null)
            cabin = ship.transform.position;

        bool minCloser = (worldMin - cabin).sqrMagnitude <= (worldMax - cabin).sqrMagnitude;
        float face = minCloser ? min : max;
        float towardCabin = minCloser ? -1f : 1f;
        return face + towardCabin * 0.012f;
    }

    private static GameObject CreateWhiteCyclePadVisual(Transform parent)
    {
        // Desk white buttons are often baked into ControlPanelWTexture (no separate GO).
        // Use a short Cylinder + Unlit off-white so the pad stays visible in the dark cabin.
        var pad = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        pad.name = "CyclePad";
        pad.transform.SetParent(parent, worldPositionStays: false);
        pad.transform.localPosition = Vector3.zero;
        pad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f); // face out like a round nub
        pad.transform.localScale = new Vector3(1f, 0.28f, 1f); // squat button profile
        var padCol = pad.GetComponent<Collider>();
        if (padCol != null)
            UnityEngine.Object.Destroy(padCol);
        ApplyWhiteButtonMaterial(pad);
        foreach (var mr in pad.GetComponentsInChildren<MeshRenderer>(true))
        {
            mr.enabled = true;
            mr.forceRenderingOff = false;
        }
        Plugin.Log.LogInfo("Cycle pad visual: Unlit off-white Cylinder (desk button stand-in).");
        return pad;
    }

    private static void NormalizePadLocalScale(GameObject pad)
    {
        var mfs = pad.GetComponentsInChildren<MeshFilter>(true);
        Bounds? combined = null;
        foreach (var mf in mfs)
        {
            if (mf.sharedMesh == null)
                continue;
            var b = mf.sharedMesh.bounds;
            // Transform bounds roughly by local scale/position of the filter relative to pad.
            var worldCorners = new Vector3[8];
            Vector3 c = b.center;
            Vector3 e = b.extents;
            int i = 0;
            for (int x = -1; x <= 1; x += 2)
            for (int y = -1; y <= 1; y += 2)
            for (int z = -1; z <= 1; z += 2)
                worldCorners[i++] = mf.transform.TransformPoint(c + Vector3.Scale(e, new Vector3(x, y, z)));

            Bounds wb = new Bounds(worldCorners[0], Vector3.zero);
            for (int k = 1; k < 8; k++)
                wb.Encapsulate(worldCorners[k]);

            // Convert to pad-local
            Bounds lb = new Bounds(pad.transform.InverseTransformPoint(wb.center), Vector3.zero);
            // Approximate size in pad local by transforming extents (axis-aligned approx).
            Vector3 localSize = pad.transform.InverseTransformVector(wb.size);
            localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            lb.size = localSize;
            if (combined == null)
                combined = lb;
            else
            {
                var cur = combined.Value;
                cur.Encapsulate(lb);
                combined = cur;
            }
        }

        float maxAxis = 1f;
        if (combined != null)
            maxAxis = Mathf.Max(combined.Value.size.x, combined.Value.size.y, combined.Value.size.z);
        if (maxAxis < 0.0001f)
            maxAxis = 1f;
        float s = 1f / maxAxis;
        pad.transform.localScale = Vector3.one * s;
    }

    /// <summary>
    /// Prefer a real round white/light-grey desk button near the start lever / control panel.
    /// Falls back to CameraMonitorSwitchButton mesh (vanilla white switch), then null.
    /// </summary>
    private static GameObject? FindWhiteConsoleButton()
    {
        // Name heuristics (desk / panel / known round buttons).
        string[] namedPaths =
        {
            "Environment/HangarShip/StartMatchLever/Button",
            "Environment/HangarShip/ControlPanelWTexture/Button",
            "Environment/HangarShip/ControlDesk/Button",
            "Environment/HangarShip/ShipModels2b/Button",
            "WhiteButton",
            "ButtonWhite",
            "RoundButton"
        };
        foreach (var p in namedPaths)
        {
            var go = GameObject.Find(p);
            if (go != null && go.GetComponentInChildren<MeshFilter>(true) != null)
            {
                Plugin.Log.LogInfo($"FindWhiteConsoleButton: name hit '{p}'.");
                return PreferMeshObject(go);
            }
        }

        // Near StartMatchLever: small roughly-spherical/cylinder meshes with light materials.
        StartMatchLever? lever = null;
        try { lever = UnityEngine.Object.FindObjectOfType<StartMatchLever>(); } catch { /* ignore */ }
        Vector3 anchor = Vector3.zero;
        bool haveAnchor = false;
        if (lever != null)
        {
            anchor = lever.transform.position;
            haveAnchor = true;
        }
        else
        {
            var desk = GameObject.Find("Environment/HangarShip/ControlPanelWTexture")
                ?? GameObject.Find("Environment/HangarShip/ControlDesk");
            if (desk != null)
            {
                anchor = desk.transform.position;
                haveAnchor = true;
            }
        }

        var ship = GameObject.Find("Environment/HangarShip");
        if (ship != null)
        {
            GameObject? best = null;
            float bestScore = float.MaxValue;
            foreach (var mr in ship.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr == null || mr.sharedMaterial == null)
                    continue;
                string n = mr.gameObject.name;
                string path = GetPath(mr.transform);
                // Skip monitors, screens, our own clones, big furniture, teleporter interactables.
                if (path.IndexOf("CrewMonitor", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (path.IndexOf("ShipTeleporter", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (path.IndexOf("MonitorWall", System.StringComparison.OrdinalIgnoreCase) >= 0
                    && path.IndexOf("CameraMonitorSwitchButton", System.StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                if (n.IndexOf("Screen", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                Bounds wb = mr.bounds;
                float maxDim = Mathf.Max(wb.size.x, Mathf.Max(wb.size.y, wb.size.z));
                float minDim = Mathf.Min(wb.size.x, Mathf.Min(wb.size.y, wb.size.z));
                // Desk buttons are small (~0.03–0.25m).
                if (maxDim < 0.02f || maxDim > 0.28f)
                    continue;
                // Prefer somewhat round / not extremely flat slabs.
                if (minDim < maxDim * 0.15f)
                    continue;

                if (!IsLightGreyOrWhiteMaterial(mr))
                    continue;

                float dist = haveAnchor ? Vector3.Distance(wb.center, anchor) : 0f;
                // Prefer near lever/desk; also prefer names containing Button.
                float nameBias = (n.IndexOf("Button", System.StringComparison.OrdinalIgnoreCase) >= 0
                                  || path.IndexOf("Button", System.StringComparison.OrdinalIgnoreCase) >= 0)
                    ? -0.5f
                    : 0f;
                float score = dist + nameBias + maxDim * 0.1f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = PreferMeshObject(mr.gameObject);
                }
            }

            if (best != null)
            {
                Plugin.Log.LogInfo(
                    $"FindWhiteConsoleButton: scan hit '{GetPath(best.transform)}' " +
                    $"(score={bestScore:F3}, nearLever={haveAnchor}).");
                return best;
            }
        }

        // Known white round switch on the monitor wall (vanilla camera cycle).
        var switchBtn = FindVanillaSwitchButton();
        if (switchBtn != null)
        {
            var meshChild = PreferMeshObject(switchBtn);
            Plugin.Log.LogInfo(
                $"FindWhiteConsoleButton: falling back to CameraMonitorSwitchButton mesh '{GetPath(meshChild.transform)}'.");
            return meshChild;
        }

        return null;
    }

    private static GameObject PreferMeshObject(GameObject go)
    {
        // Prefer a child that actually has MeshFilter (e.g. Cube under CameraMonitorSwitchButton).
        var mfs = go.GetComponentsInChildren<MeshFilter>(true);
        if (mfs.Length == 0)
            return go;
        // Smallest non-zero mesh among children often is the button nub.
        MeshFilter? best = null;
        float bestSize = float.MaxValue;
        foreach (var mf in mfs)
        {
            if (mf.sharedMesh == null)
                continue;
            var s = mf.sharedMesh.bounds.size;
            float maxDim = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            if (maxDim <= 0.0001f)
                continue;
            if (maxDim < bestSize)
            {
                bestSize = maxDim;
                best = mf;
            }
        }
        return best != null ? best.gameObject : go;
    }

    private static bool IsLightGreyOrWhiteMaterial(MeshRenderer mr)
    {
        var mats = mr.sharedMaterials;
        if (mats == null || mats.Length == 0)
            return false;
        foreach (var mat in mats)
        {
            if (mat == null)
                continue;
            Color c = Color.white;
            if (mat.HasProperty("_BaseColor"))
                c = mat.GetColor("_BaseColor");
            else if (mat.HasProperty("_Color"))
                c = mat.color;
            else
                c = mat.color;

            float max = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
            float min = Mathf.Min(c.r, Mathf.Min(c.g, c.b));
            // Light and low-saturation (white / light grey), reject saturated red/green.
            if (max >= 0.55f && (max - min) <= 0.22f)
                return true;

            string mn = mat.name ?? "";
            if (mn.IndexOf("White", System.StringComparison.OrdinalIgnoreCase) >= 0
                || mn.IndexOf("Light", System.StringComparison.OrdinalIgnoreCase) >= 0
                || mn.IndexOf("Grey", System.StringComparison.OrdinalIgnoreCase) >= 0
                || mn.IndexOf("Gray", System.StringComparison.OrdinalIgnoreCase) >= 0)
                return true;
        }
        return false;
    }

    private static void StripNonVisualBehaviours(GameObject root)
    {
        StripNetworkBehaviours(root);
        foreach (var mb in root.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mb == null)
                continue;
            // Keep nothing behavioural — pad is visual only.
            try { UnityEngine.Object.Destroy(mb); } catch { /* ignore */ }
        }
        foreach (var anim in root.GetComponentsInChildren<Animator>(true))
        {
            try { UnityEngine.Object.Destroy(anim); } catch { /* ignore */ }
        }
        foreach (var rb in root.GetComponentsInChildren<Rigidbody>(true))
        {
            try { UnityEngine.Object.Destroy(rb); } catch { /* ignore */ }
        }
        foreach (var aud in root.GetComponentsInChildren<AudioSource>(true))
        {
            try { UnityEngine.Object.Destroy(aud); } catch { /* ignore */ }
        }
    }


    private static void ApplyBezelGreyMaterial(GameObject go)
    {
        var mr = go.GetComponent<MeshRenderer>();
        if (mr == null)
            return;

        Shader? shader = Shader.Find("Unlit/Color")
            ?? Shader.Find("Legacy Shaders/Unlit/Color")
            ?? Shader.Find("HDRP/Unlit")
            ?? Shader.Find("Standard");
        var mat = shader != null ? new Material(shader) : new Material(mr.sharedMaterial);
        var color = new Color(0.28f, 0.28f, 0.30f, 1f);
        if (mat.HasProperty("_Color"))
            mat.color = color;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_UnlitColor"))
            mat.SetColor("_UnlitColor", color);
        if (mat.HasProperty("_EmissiveColor"))
            mat.SetColor("_EmissiveColor", color * 0.08f);

        mr.sharedMaterial = mat;
        mr.enabled = true;
        mr.forceRenderingOff = false;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private static void ApplyWhiteButtonMaterial(GameObject go)
    {
        var mr = go.GetComponent<MeshRenderer>();
        if (mr == null)
            return;

        // Prefer Unlit so pads stay visible in the dark ship (Lit looked black/invisible).
        Shader? shader = Shader.Find("Unlit/Color")
            ?? Shader.Find("Legacy Shaders/Unlit/Color")
            ?? Shader.Find("HDRP/Unlit")
            ?? Shader.Find("HDRP/Lit")
            ?? Shader.Find("Standard");
        var mat = shader != null ? new Material(shader) : new Material(mr.sharedMaterial);
        var color = new Color(0.92f, 0.92f, 0.90f, 1f); // console off-white
        if (mat.HasProperty("_Color"))
            mat.color = color;
        if (mat.HasProperty("_BaseColor"))
            mat.SetColor("_BaseColor", color);
        if (mat.HasProperty("_UnlitColor"))
            mat.SetColor("_UnlitColor", color);
        if (mat.HasProperty("_EmissiveColor"))
            mat.SetColor("_EmissiveColor", color * 0.35f);
        if (mat.HasProperty("_AlbedoAffectEmissive"))
            mat.SetFloat("_AlbedoAffectEmissive", 1f);

        mr.sharedMaterial = mat;
        mr.enabled = true;
        mr.forceRenderingOff = false;
        mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        mr.receiveShadows = false;
    }

    private static void StripNetworkBehaviours(GameObject root)
    {
        foreach (var nb in root.GetComponentsInChildren<Unity.Netcode.NetworkBehaviour>(true))
        {
            try { UnityEngine.Object.Destroy(nb); } catch { /* ignore */ }
        }
        foreach (var no in root.GetComponentsInChildren<Unity.Netcode.NetworkObject>(true))
        {
            try { UnityEngine.Object.Destroy(no); } catch { /* ignore */ }
        }
    }

    private static GameObject? FindVanillaSwitchButton()
    {
        return GameObject.Find(MonitorWallPath + "/" + MainWallCubeName + "/CameraMonitorSwitchButton")
            ?? GameObject.Find("CameraMonitorSwitchButton");
    }

    private static InteractTrigger ConfigureInteractTrigger(GameObject go, string tip)
    {
        var trigger = go.GetComponent<InteractTrigger>();
        if (trigger == null)
            trigger = go.AddComponent<InteractTrigger>();

        trigger.interactable = true;
        trigger.oneHandedItemAllowed = true;
        trigger.twoHandedItemAllowed = true;
        trigger.holdInteraction = false;
        trigger.touchTrigger = false;
        trigger.triggerOnce = false;
        trigger.interactCooldown = true;
        trigger.cooldownTime = 0.2f;
        trigger.disableTriggerMesh = false;
        trigger.specialCharacterAnimation = false;
        ApplyTransparentHoverIcons(trigger);
        trigger.hoverTip = tip;
        trigger.disabledHoverTip = tip;
        trigger.holdTip = tip;
        trigger.timeToHold = 0.15f;
        trigger.timeToHoldSpeedMultiplier = 1f;
        trigger.onInteract = new InteractEvent();
        trigger.onInteractEarly = new InteractEvent();
        trigger.onInteractEarlyOtherClients = new InteractEvent();
        trigger.onStopInteract = new InteractEvent();
        trigger.onCancelAnimation = new InteractEvent();
        trigger.holdingInteractEvent = new InteractEventFloat();
        return trigger;
    }

    /// <summary>
    /// Local Z on the panel that faces the cabin (for placing interact volumes in front of the mesh).
    /// </summary>
    private static float GetOutwardLocalZ(MeshRenderer renderer)
    {
        Bounds b = renderer.localBounds;
        Vector3 worldMinZ = renderer.transform.TransformPoint(new Vector3(b.center.x, b.center.y, b.min.z));
        Vector3 worldMaxZ = renderer.transform.TransformPoint(new Vector3(b.center.x, b.center.y, b.max.z));
        Vector3 cabin = worldMinZ;
        var ship = GameObject.Find("Environment/HangarShip");
        if (ship != null)
            cabin = ship.transform.position;

        // Face whose world position is closer to cabin is the room-facing side.
        bool minCloser = (worldMinZ - cabin).sqrMagnitude <= (worldMaxZ - cabin).sqrMagnitude;
        float face = minCloser ? b.min.z : b.max.z;
        float towardCabin = minCloser ? -1f : 1f;
        return face + towardCabin * 0.012f;
    }

    internal void HandleCycleInteract(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count)
            return;
        var slot = _slots[slotIndex];
        if (slot.BodyCam == null)
            return;

        slot.UserCycled = true;
        var players = GetLivingPlayers();
        // Cycle: Off -> p0 body -> p0 map -> p1 body -> p1 map -> ... -> External -> Off.
        int playerIdx = -1;
        if (slot.CycleIndex >= 0 && slot.AssignedPlayer != null)
        {
            for (int i = 0; i < players.Count; i++)
            {
                if (players[i] == slot.AssignedPlayer)
                {
                    playerIdx = i;
                    break;
                }
            }
        }
        else if (slot.CycleIndex >= 0 && slot.CycleIndex < players.Count)
            playerIdx = slot.CycleIndex;

        bool alwaysShow = Plugin.AlwaysShowMonitors.Value;

        if (slot.CycleIndex == CycleOff)
        {
            if (players.Count == 0)
            {
                slot.CycleIndex = CycleExternal;
                ApplyExternalCam(slot, alwaysShow);
                Plugin.Log.LogInfo($"Slot {slotIndex} cycled -> External.");
            }
            else
            {
                slot.CycleIndex = 0;
                slot.ShowMapFeed = false;
                ApplySlotAssignment(slot, players[0], alwaysShow, blankLabel: "Slot 1");
                Plugin.Log.LogInfo($"Slot {slotIndex} cycled -> {PlayerLabel(players[0])} body.");
            }
            UpdateInteractionTips(slot);
            return;
        }

        if (slot.CycleIndex == CycleExternal)
        {
            slot.CycleIndex = CycleOff;
            ApplyManualOff(slot, alwaysShow);
            Plugin.Log.LogInfo($"Slot {slotIndex} cycled -> Off.");
            UpdateInteractionTips(slot);
            return;
        }

        // On a player view: body -> map -> next player body (or External).
        if (playerIdx < 0 || players.Count == 0)
        {
            slot.CycleIndex = CycleExternal;
            ApplyExternalCam(slot, alwaysShow);
            Plugin.Log.LogInfo($"Slot {slotIndex} cycled -> External.");
            UpdateInteractionTips(slot);
            return;
        }

        if (!slot.ShowMapFeed)
        {
            slot.ShowMapFeed = true;
            slot.CycleIndex = playerIdx;
            ApplyMapFeed(slot, players[playerIdx], alwaysShow);
            Plugin.Log.LogInfo($"Slot {slotIndex} cycled -> {PlayerLabel(players[playerIdx])} map.");
            UpdateInteractionTips(slot);
            return;
        }

        // Leaving map feed.
        if (playerIdx + 1 >= players.Count)
        {
            slot.CycleIndex = CycleExternal;
            slot.ShowMapFeed = false;
            ApplyExternalCam(slot, alwaysShow);
            Plugin.Log.LogInfo($"Slot {slotIndex} cycled -> External.");
        }
        else
        {
            slot.CycleIndex = playerIdx + 1;
            slot.ShowMapFeed = false;
            ApplySlotAssignment(slot, players[playerIdx + 1], alwaysShow, blankLabel: $"Slot {playerIdx + 2}");
            Plugin.Log.LogInfo($"Slot {slotIndex} cycled -> {PlayerLabel(players[playerIdx + 1])} body.");
        }
        UpdateInteractionTips(slot);
    }

    private static string PlayerLabel(PlayerControllerB player)
    {
        return string.IsNullOrEmpty(player.playerUsername)
            ? $"Player {player.playerClientId}"
            : player.playerUsername;
    }

    internal void HandleFocusInteract(int slotIndex)
    {
        if (slotIndex < 0 || slotIndex >= _slots.Count)
            return;
        var slot = _slots[slotIndex];
        var player = slot.AssignedPlayer;
        if (player == null || !IsLivingPlayer(player))
        {
            Plugin.Log.LogInfo($"Slot {slotIndex} focus: no living assigned player.");
            return;
        }

        var map = StartOfRound.Instance?.mapScreen;
        if (map == null || map.radarTargets == null)
        {
            Plugin.Log.LogWarning($"Slot {slotIndex} focus: mapScreen unavailable.");
            return;
        }

        int radarIndex = FindRadarTargetIndex(map, player);
        if (radarIndex < 0)
        {
            Plugin.Log.LogWarning(
                $"Slot {slotIndex} focus: player '{player.playerUsername}' not found in mapScreen.radarTargets.");
            return;
        }

        // Vanilla path used by terminal "switch", touchscreen, etc.: updates radar map,
        // targetedPlayer (teleporter), and anything synced to mapScreen (e.g. OBC main cam).
        map.SwitchRadarTargetAndSync(radarIndex);

        string name = string.IsNullOrEmpty(player.playerUsername)
            ? $"Player {player.playerClientId}"
            : player.playerUsername;
        Plugin.Log.LogInfo(
            $"Slot {slotIndex} focused main monitor/radar/teleporter on {name} (radarTargets[{radarIndex}]).");
    }

    private static int FindRadarTargetIndex(ManualCameraRenderer map, PlayerControllerB player)
    {
        var targets = map.radarTargets;
        for (int i = 0; i < targets.Count; i++)
        {
            var t = targets[i];
            if (t?.transform == null)
                continue;
            if (t.transform == player.transform)
                return i;
            var pcb = t.transform.GetComponent<PlayerControllerB>();
            if (pcb == player)
                return i;
        }

        return -1;
    }

    private void PowerDownAll()
    {
        foreach (var slot in _slots)
        {
            if (slot.BodyCam == null)
                continue;
            slot.BodyCam.SetTargetToNone();
            slot.BodyCam.SetScreenPowered(false);
            slot.AssignedPlayer = null;
            slot.CycleIndex = CycleOff;
            slot.UserCycled = false;
            slot.ShowMapFeed = false;
            StopMapFeedCamera(slot);
            if (slot.IsClone && slot.Host != null)
                slot.Host.SetActive(false);
            if (slot.Nameplate != null)
                slot.Nameplate.gameObject.SetActive(false);
        }
    }

    private void TeardownSlots()
    {
        foreach (var slot in _slots)
        {
            DestroyMapFeedResources(slot);

            if (slot.BodyCam != null)
            {
                try
                {
                    slot.BodyCam.SetTargetToNone();
                    slot.BodyCam.SetScreenPowered(false);
                }
                catch
                {
                    // ignored during teardown
                }

                Destroy(slot.BodyCam);
            }

            if (slot.Nameplate != null)
                Destroy(slot.Nameplate.gameObject);

            if (slot.CycleButton != null)
            {
                Destroy(slot.CycleButton);
                slot.CycleButton = null;
            }

            if (slot.FocusTrigger != null)
            {
                Destroy(slot.FocusTrigger);
                slot.FocusTrigger = null;
            }

            if (slot.IsClone && slot.Host != null)
                Destroy(slot.Host);
        }

        _slots.Clear();

        if (_slotRoot != null)
        {
            Destroy(_slotRoot);
            _slotRoot = null;
        }
    }

    private static string GetPath(Transform t)
    {
        var parts = new Stack<string>();
        while (t != null)
        {
            parts.Push(t.name);
            t = t.parent;
        }

        return string.Join("/", parts);
    }

    private struct WallPlacement
    {
        public int HorizontalAxis;
        public int VerticalAxis;
        public int DepthAxis;
        public float HSign;
        public float VSign;
        public float OutwardSign;
        public float FlushDepth;
        public Bounds LocalBounds;
        public bool MaxIsOutward;
    }


    private static readonly BindingFlags BodyCamFlags =
        BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

    private static Material? _blackScreenMaterial;

    private static Material GetBlackScreenMaterial()
    {
        if (_blackScreenMaterial != null)
            return _blackScreenMaterial;
        Shader? shader = Shader.Find("HDRP/Unlit")
            ?? Shader.Find("Unlit/Color")
            ?? Shader.Find("Legacy Shaders/Unlit/Color");
        _blackScreenMaterial = shader != null ? new Material(shader) : new Material(Shader.Find("Sprites/Default"));
        _blackScreenMaterial.name = "CrewMonitor_BlackOff";
        var black = Color.black;
        if (_blackScreenMaterial.HasProperty("_Color"))
            _blackScreenMaterial.color = black;
        if (_blackScreenMaterial.HasProperty("_BaseColor"))
            _blackScreenMaterial.SetColor("_BaseColor", black);
        if (_blackScreenMaterial.HasProperty("_UnlitColor"))
            _blackScreenMaterial.SetColor("_UnlitColor", black);
        if (_blackScreenMaterial.HasProperty("_EmissiveColor"))
            _blackScreenMaterial.SetColor("_EmissiveColor", black);
        return _blackScreenMaterial;
    }

    private static Material? GetMonitorOffMaterial(BodyCamComponent bodyCam)
    {
        try
        {
            return typeof(BodyCamComponent).GetField("MonitorOffMaterial", BodyCamFlags)?.GetValue(bodyCam) as Material;
        }
        catch
        {
            return null;
        }
    }

    private static void SetMonitorOffMaterial(BodyCamComponent bodyCam, Material? mat)
    {
        if (mat == null)
            return;
        try
        {
            typeof(BodyCamComponent).GetField("MonitorOffMaterial", BodyCamFlags)?.SetValue(bodyCam, mat);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"SetMonitorOffMaterial failed: {ex.Message}");
        }
    }


    private static Sprite? _transparentHoverIcon;

    private static Sprite GetTransparentHoverIcon()
    {
        if (_transparentHoverIcon != null)
            return _transparentHoverIcon;
        var tex = new Texture2D(1, 1, TextureFormat.ARGB32, mipChain: false);
        tex.SetPixel(0, 0, Color.clear);
        tex.Apply(updateMipmaps: false, makeNoLongerReadable: false);
        tex.name = "CrewMonitor_ClearHover";
        _transparentHoverIcon = Sprite.Create(
            tex,
            new Rect(0f, 0f, 1f, 1f),
            new Vector2(0.5f, 0.5f),
            100f);
        _transparentHoverIcon.name = "CrewMonitor_ClearHoverSprite";
        return _transparentHoverIcon;
    }

    private static void ApplyTransparentHoverIcons(InteractTrigger dest)
    {
        var clear = GetTransparentHoverIcon();
        dest.hoverIcon = clear;
        dest.disabledHoverIcon = clear;
    }

    private static void ApplyVanillaHoverIcons(InteractTrigger dest)
    {
        InteractTrigger? src = null;
        var vanilla = FindVanillaSwitchButton();
        if (vanilla != null)
            src = vanilla.GetComponentInChildren<InteractTrigger>(true);
        if (src == null || src.hoverIcon == null)
        {
            var onBtn = GameObject.Find("CameraMonitorOn")
                ?? GameObject.Find("Environment/HangarShip/ShipModels2b/MonitorWall/CameraMonitorOn");
            if (onBtn != null)
                src = onBtn.GetComponentInChildren<InteractTrigger>(true);
        }
        if (src != null && src.hoverIcon != null)
        {
            dest.hoverIcon = src.hoverIcon;
            dest.disabledHoverIcon = src.disabledHoverIcon != null ? src.disabledHoverIcon : src.hoverIcon;
            return;
        }

        ApplyTransparentHoverIcons(dest);
    }

    private void UpdateInteractionTips(CrewSlot slot)
    {
        bool hasPlayer = slot.AssignedPlayer != null && IsLivingPlayer(slot.AssignedPlayer);
        bool isOff = slot.CycleIndex <= CycleOff || (!hasPlayer && slot.CycleIndex != CycleExternal);
        bool isExternal = slot.CycleIndex == CycleExternal;

        string nameTip;
        if (isOff)
            nameTip = "Off";
        else if (isExternal)
            nameTip = "External";
        else if (hasPlayer)
        {
            nameTip = string.IsNullOrEmpty(slot.AssignedPlayer!.playerUsername)
                ? $"Player {slot.AssignedPlayer.playerClientId}"
                : slot.AssignedPlayer.playerUsername;
            if (slot.ShowMapFeed)
                nameTip = $"{nameTip} Map";
        }
        else
            nameTip = "Off";

        if (slot.FocusTrigger != null)
        {
            var focus = slot.FocusTrigger.GetComponent<InteractTrigger>();
            if (focus != null)
            {
                // No mouseover for Off or External.
                if (isOff || isExternal)
                {
                    focus.interactable = false;
                    focus.hoverTip = "";
                    focus.disabledHoverTip = "";
                    focus.holdTip = "";
                    ApplyTransparentHoverIcons(focus);
                }
                else
                {
                    focus.interactable = true;
                    string tip = hasPlayer ? $"{nameTip} : [E]" : nameTip;
                    focus.hoverTip = tip;
                    focus.disabledHoverTip = tip;
                    focus.holdTip = tip;
                    ApplyTransparentHoverIcons(focus);
                }
            }
        }

        if (slot.CycleButton != null)
        {
            var cycle = slot.CycleButton.GetComponentInChildren<InteractTrigger>(true);
            if (cycle != null)
            {
                cycle.interactable = true;
                cycle.hoverTip = $"Cycle ({nameTip}) : [E]";
                cycle.disabledHoverTip = cycle.hoverTip;
                cycle.holdTip = cycle.hoverTip;
                ApplyVanillaHoverIcons(cycle);
            }
        }

        // Map feed RT is re-applied in LateUpdate via the dedicated follower camera.
    }


    private sealed class CrewSlot
    {
        public int Index;
        public GameObject Host = null!;
        public MeshRenderer Renderer = null!;
        public BodyCamComponent BodyCam = null!;
        public Material? OriginalOffMaterial;
        public bool IsClone;
        public PlayerControllerB? AssignedPlayer;
        /// <summary>
        /// Per-slot cycle cursor. -1 = manually Off (RefreshAssignments must not auto-assign).
        /// &gt;= 0 tracks the living-player index last chosen by the cycle button.
        /// Starts at 0 so first refresh can auto-assign normally.
        /// </summary>
        public int CycleIndex;
        /// <summary>True after the player uses this slot's cycle button; auto bottom-up fill skips it.</summary>
        public bool UserCycled;
        /// <summary>When true with a living AssignedPlayer, show that player's radar/map feed instead of body cam.</summary>
        public bool ShowMapFeed;
        /// <summary>Private map follower camera for this slot (independent of mapScreen target).</summary>
        public Camera? MapFeedCamera;
        /// <summary>Private RenderTexture assigned to MonitorOnMaterial while ShowMapFeed.</summary>
        public RenderTexture? MapFeedTexture;
        public TextMeshPro? Nameplate;
        public GameObject? CycleButton;
        public GameObject? FocusTrigger;
    }
}

/// <summary>
/// Bridges InteractTrigger.onInteract to CrewMonitorManager cycle/focus handlers.
/// </summary>
internal sealed class CrewSlotInteractBridge : MonoBehaviour
{
    private CrewMonitorManager? _manager;
    private int _slotIndex;
    private bool _isCycleButton;

    public void Init(CrewMonitorManager manager, int slotIndex, bool isCycleButton)
    {
        _manager = manager;
        _slotIndex = slotIndex;
        _isCycleButton = isCycleButton;
    }

    public void OnInteract(PlayerControllerB player)
    {
        if (_manager == null)
            return;
        if (_isCycleButton)
            _manager.HandleCycleInteract(_slotIndex);
        else
            _manager.HandleFocusInteract(_slotIndex);
    }
}




internal sealed class CrewButtonRaycastFallback : MonoBehaviour
{
    private CrewMonitorManager? _manager;
    private int _slotIndex;
    private Collider? _col;
    private InteractTrigger? _trigger;
    private float _cooldown;

    public void Init(CrewMonitorManager manager, int slotIndex, Collider col, InteractTrigger? trigger = null)
    {
        _manager = manager;
        _slotIndex = slotIndex;
        _col = col;
        _trigger = trigger;
    }

    private void Update()
    {
        if (_manager == null || _col == null)
            return;
        _cooldown -= Time.deltaTime;
        if (_cooldown > 0f)
            return;

        var player = GameNetworkManager.Instance?.localPlayerController;
        if (player == null || player.isPlayerDead || !player.isPlayerControlled)
            return;

        // If vanilla already has our trigger hovered, Interact_performed will fire onInteract — do not double-cycle.
        if (_trigger != null && player.hoveringOverTrigger == _trigger)
            return;

        var cam = player.gameplayCamera != null ? player.gameplayCamera : Camera.main;
        if (cam == null)
            return;

        var ray = new Ray(cam.transform.position, cam.transform.forward);
        if (!Physics.Raycast(ray, out RaycastHit hit, 4.5f, ~0, QueryTriggerInteraction.Ignore))
            return;
        if (hit.collider != _col)
            return;
        if (!WasInteractPressed(player))
            return;

        _cooldown = 0.25f;
        _manager.HandleCycleInteract(_slotIndex);
        Plugin.Log.LogInfo($"Slot {_slotIndex} cycle via raycast fallback.");
    }

    private static bool WasInteractPressed(PlayerControllerB player)
    {
        // Reflect into IngamePlayerSettings / InputAction so we do not hard-ref Unity.InputSystem.
        try
        {
            var settingsType = System.Type.GetType("IngamePlayerSettings, Assembly-CSharp");
            var instanceProp = settingsType?.GetProperty("Instance", BindingFlags.Public | BindingFlags.Static);
            var settings = instanceProp?.GetValue(null);
            var playerInput = settings?.GetType().GetField("playerInput")?.GetValue(settings)
                ?? settings?.GetType().GetProperty("playerInput")?.GetValue(settings);
            var actions = playerInput?.GetType().GetProperty("actions")?.GetValue(playerInput);
            if (actions != null)
            {
                var find = actions.GetType().GetMethod("FindAction", new[] { typeof(string), typeof(bool) });
                var interact = find?.Invoke(actions, new object[] { "Interact", false });
                var wasPressed = interact?.GetType().GetMethod("WasPressedThisFrame");
                if (wasPressed != null && wasPressed.Invoke(interact, null) is bool pressed && pressed)
                    return true;
            }
        }
        catch
        {
            /* ignore */
        }

        try
        {
            if (player.playerActions != null)
            {
                var movement = player.playerActions.Movement;
                var interactProp = movement.GetType().GetProperty("Interact");
                if (interactProp != null)
                {
                    var action = interactProp.GetValue(movement);
                    var wasPressed = action?.GetType().GetMethod("WasPressedThisFrame");
                    if (wasPressed != null && wasPressed.Invoke(action, null) is bool b && b)
                        return true;
                }
            }
        }
        catch
        {
            /* ignore */
        }

        return false;
    }
}


