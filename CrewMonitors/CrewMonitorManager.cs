using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using GameNetcodeStuff;
using OpenBodyCams;
using OpenBodyCams.API;
using TMPro;
using UnityEngine;

namespace CrewMonitors;

/// <summary>
/// Creates per-player OpenBodyCams body cams on ship monitors (reusing unused
/// vanilla screens first, then cloning panels) and keeps assignments fresh.
/// </summary>
public sealed class CrewMonitorManager : MonoBehaviour
{
    private const string MonitorWallPath = "Environment/HangarShip/ShipModels2b/MonitorWall";
    private const string SingleScreenPath = MonitorWallPath + "/SingleScreen";
    private const string MainWallCubeName = "Cube.001";
    private const string ClonePrefix = "CrewMonitor_Clone_";
    private const string SlotRootName = "CrewMonitors_Root";

    internal static CrewMonitorManager? Instance { get; private set; }

    private readonly List<CrewSlot> _slots = new();
    private Transform? _monitorWall;
    private MeshRenderer? _cloneTemplate;
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

        var renderers = CollectReusableRenderers();
        EnsureCloneTemplate(renderers);

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

                int cloneOrdinal = i - renderers.Count;
                host = CloneMonitor(cloneOrdinal, out renderer);
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

            bodyCam.SetTargetToNone();
            bodyCam.SetScreenPowered(false);

            var slot = new CrewSlot
            {
                Index = i,
                Host = host,
                Renderer = renderer,
                BodyCam = bodyCam,
                IsClone = isClone,
                Nameplate = Plugin.ShowNameplates.Value ? CreateNameplate(host, renderer) : null
            };
            _slots.Add(slot);
        }
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
            if (IsRendererForbidden(mr))
            {
                Plugin.Log.LogDebug($"Skipping {label} ({mr.name}): claimed or reserved.");
                return;
            }

            claimed.Add(id);
            result.Add(mr);
            Plugin.Log.LogInfo($"Reusable screen: {label} -> {GetPath(mr.transform)}");
        }

        if (Plugin.PreferReuseVanillaScreens.Value)
        {
            var single = GameObject.Find(SingleScreenPath);
            TryAdd(single != null ? single.GetComponent<MeshRenderer>() : null, "SingleScreen");

            var sor = StartOfRound.Instance;
            if (sor != null)
            {
                TryAdd(sor.insideCameraScreen != null ? sor.insideCameraScreen.mesh : null, "insideCameraScreen.mesh");
                TryAdd(sor.securityCameraScreen != null ? sor.securityCameraScreen.mesh : null, "securityCameraScreen.mesh");
            }

            // Any other MonitorWall MeshRenderers that aren't the multi-material main cube.
            if (_monitorWall != null)
            {
                foreach (var mr in _monitorWall.GetComponentsInChildren<MeshRenderer>(true))
                {
                    if (mr.gameObject.name.StartsWith(ClonePrefix, StringComparison.Ordinal))
                        continue;
                    if (mr.gameObject.name == MainWallCubeName || mr.gameObject.name.Contains("Cube.001"))
                        continue;
                    TryAdd(mr, $"MonitorWall/{mr.name}");
                }
            }
        }

        return result;
    }

    private static bool IsRendererForbidden(MeshRenderer mr)
    {
        // OpenBodyCams MainBodyCam / any existing body cam already owns this renderer.
        if (BodyCamComponent.AnyBodyCamHasReference(mr))
            return true;

        // Never steal Cube.001 (OBC default main monitor wall panel).
        if (mr.gameObject.name == MainWallCubeName || mr.gameObject.name.Contains("Cube.001"))
            return true;

        // Do not steal the radar map screen.
        var map = StartOfRound.Instance?.mapScreen;
        if (map != null && map.mesh == mr)
            return true;

        return false;
    }

    private void EnsureCloneTemplate(List<MeshRenderer> reusable)
    {
        if (reusable.Count > 0)
        {
            _cloneTemplate = reusable[0];
            return;
        }

        var single = GameObject.Find(SingleScreenPath);
        if (single != null)
        {
            _cloneTemplate = single.GetComponent<MeshRenderer>();
            if (_cloneTemplate != null)
                return;
        }

        if (_monitorWall != null)
        {
            foreach (var mr in _monitorWall.GetComponentsInChildren<MeshRenderer>(true))
            {
                if (mr.gameObject.name == MainWallCubeName)
                    continue;
                _cloneTemplate = mr;
                return;
            }

            // Last resort: Cube.001 as visual template only (we clone; do not attach to original).
            var cube = _monitorWall.Find(MainWallCubeName);
            if (cube != null)
                _cloneTemplate = cube.GetComponent<MeshRenderer>();
        }
    }

    private GameObject CloneMonitor(int slotIndex, out MeshRenderer? renderer)
    {
        renderer = null;
        if (_cloneTemplate == null || _monitorWall == null)
            return null!;

        var clone = Instantiate(_cloneTemplate.gameObject, _monitorWall);
        clone.name = $"{ClonePrefix}{slotIndex}";

        // Strip components that would conflict (existing BodyCam, ManualCameraRenderer, etc.).
        foreach (var cam in clone.GetComponents<BodyCamComponent>())
            Destroy(cam);
        foreach (var mcr in clone.GetComponents<ManualCameraRenderer>())
            Destroy(mcr);

        // Grid: extension row above the wall, then sideways.
        float spacing = Plugin.CloneSpacing.Value;
        int col = slotIndex % 4;
        int row = slotIndex / 4;
        var local = _cloneTemplate.transform.localPosition;
        // Prefer hanging above / beside: bump Y for new rows, X for columns.
        clone.transform.localPosition = local + new Vector3(col * spacing, (row + 1) * spacing * 0.85f, 0f);
        clone.transform.localRotation = _cloneTemplate.transform.localRotation;
        clone.transform.localScale = _cloneTemplate.transform.localScale;

        renderer = clone.GetComponent<MeshRenderer>();
        if (renderer == null)
            renderer = clone.GetComponentInChildren<MeshRenderer>();

        clone.SetActive(true);
        Plugin.Log.LogInfo($"Cloned monitor slot {slotIndex} at local {clone.transform.localPosition}");
        return clone;
    }

    private static int PickMaterialIndex(MeshRenderer renderer)
    {
        var mats = renderer.sharedMaterials;
        if (mats == null || mats.Length == 0)
            return 0;
        // OpenBodyCams README uses index 1 for SingleScreen; fall back to 0 if only one material.
        if (mats.Length > 1)
            return 1;
        return 0;
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

            // Prefer the ship's map name TMP font when available.
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
        for (int i = 0; i < _slots.Count; i++)
        {
            var slot = _slots[i];
            if (slot.BodyCam == null)
                continue;

            if (i < players.Count)
            {
                var player = players[i];
                if (slot.AssignedPlayer != player)
                {
                    slot.BodyCam.SetTargetToPlayer(player);
                    slot.AssignedPlayer = player;
                }

                slot.BodyCam.SetScreenPowered(true);
                if (slot.IsClone && slot.Host != null && !slot.Host.activeSelf)
                    slot.Host.SetActive(true);

                if (slot.Nameplate != null)
                {
                    slot.Nameplate.gameObject.SetActive(true);
                    slot.Nameplate.text = string.IsNullOrEmpty(player.playerUsername)
                        ? $"Player {player.playerClientId}"
                        : player.playerUsername;
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

    private void PowerDownAll()
    {
        foreach (var slot in _slots)
        {
            if (slot.BodyCam == null)
                continue;
            slot.BodyCam.SetTargetToNone();
            slot.BodyCam.SetScreenPowered(false);
            slot.AssignedPlayer = null;
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

    private sealed class CrewSlot
    {
        public int Index;
        public GameObject Host = null!;
        public MeshRenderer Renderer = null!;
        public BodyCamComponent BodyCam = null!;
        public bool IsClone;
        public PlayerControllerB? AssignedPlayer;
        public TextMeshPro? Nameplate;
    }
}
