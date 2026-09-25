using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using GameNetcodeStuff;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.InputSystem;

namespace MapClickCodes;

/// <summary>
/// Precise click-to-activate for TerminalAccessibleObject map radar code markers
/// on the MAIN ship map screen only (StartOfRound.mapScreen).
/// Clients hit-test locally then ask the host to run CallFunctionFromTerminal.
/// </summary>
internal static class MapCodeClickController
{
    private static float _cooldown;
    private static float _gateWarnCooldown;
    private static readonly Vector3[] CornerBuf = new Vector3[4];
    private static readonly RaycastHit[] RayHitBuf = new RaycastHit[24];
    private static readonly List<(Vector2 uv, string tag)> UvCandidates = new(24);
    private static bool _hasPhysicsUv;
    private static Vector2 _physicsUv;
    private static string _physicsUvTag = "none";
    private static bool _ensuredMeshColliderMesh;
    private static bool _ensuredMeshColliderMesh2;
    // 1.0.11: skip CPU mesh paths / runtime MeshCollider cook when mesh is not readable.
    private static bool _loggedNonReadableSkip;

    // 1.0.10: tip → E sync — last successful peek (hover tip) so activate uses the same object.
    private static TerminalAccessibleObject? _peekCacheTao;
    private static string _peekCacheCode = "";
    private static float _peekCacheTime = -999f;
    private static ulong _peekCacheNetId;
    private const float PeekCacheMaxAge = 0.25f;

    // Soft CrewMonitors reflection cache (null / failed → skip forever this session).
    private static bool _crewReflectTried;
    private static bool _crewReflectOk;
    private static Type? _crewManagerType;
    private static PropertyInfo? _crewInstanceProp;
    private static FieldInfo? _crewSlotsField;
    private static FieldInfo? _crewSlotMapFeedCam;
    private static FieldInfo? _crewSlotMapFeedTex;
    private static FieldInfo? _crewSlotHost;
    private static FieldInfo? _crewSlotRenderer;
    private static FieldInfo? _crewSlotShowMapFeed;

    /// <summary>
    /// Backup path: Interact press while looking at the main mesh, even if vanilla
    /// does not call SwitchRadarTargetForward (e.g. odd hover state).
    /// </summary>
    internal static void Tick()
    {
        HostModGate.EnsureRegistered();
        if (Plugin.Enabled == null || !Plugin.Enabled.Value)
            return;

        _cooldown -= Time.unscaledDeltaTime;
        _gateWarnCooldown -= Time.unscaledDeltaTime;

        var gnm = GameNetworkManager.Instance;
        var player = gnm != null ? gnm.localPlayerController : null;
        if (player == null || player.isPlayerDead || !player.isPlayerControlled)
            return;

        if (!player.isInHangarShipRoom)
            return;

        if (!WasInteractPressed(player))
            return;

        if (!HostModGate.FeaturesActive)
        {
            LogGateBlocked();
            return;
        }

        if (_cooldown > 0f)
        {
            Plugin.Log.LogInfo("MapClick: interact edge ignored (cooldown).");
            return;
        }

        // Force map context when looking near the main/crew map screen, or hovering a
        // map/radar/monitor interact — NOT Terminal or other ship triggers.
        bool nearMap = IsLookingNearMapScreen(player);
        bool hoveringMap = IsMapRelatedInteractTrigger(player.hoveringOverTrigger);
        bool force = nearMap || hoveringMap;

        TryActivateFromCurrentLook(requireInteractPress: false, forceMapContext: force, alwaysLog: true);
    }


    /// <summary>
    /// Hit-test only: same resolve + UV + marker match as activate, but no cooldown,
    /// no ClickConsumed, and no CallFunctionFromTerminal. Used for hover cursor/tip.
    /// </summary>
    internal static bool TryPeekMarkerUnderLook(out TerminalAccessibleObject matched, out string objectCode)
    {
        matched = null!;
        objectCode = "";

        if (Plugin.Enabled == null || !Plugin.Enabled.Value)
            return false;
        if (!HostModGate.FeaturesActive)
            return false;

        var gnm = GameNetworkManager.Instance;
        var player = gnm != null ? gnm.localPlayerController : null;
        if (player == null || player.isPlayerDead || !player.isPlayerControlled)
            return false;
        if (!player.isInHangarShipRoom)
            return false;

        // Peek: never force on Terminal hover; never Info-log misses (alwaysLog false).
        bool forcePeek = IsLookingNearMapScreen(player)
                         || IsMapRelatedInteractTrigger(player.hoveringOverTrigger);
        if (!TryResolveLookMarker(
                player,
                forceMapContext: forcePeek,
                alwaysLog: false,
                allowCenterFallback: false,
                requireMeshUv: false,
                out matched,
                out _,
                out _,
                out _,
                out _,
                out _))
            return false;

        objectCode = matched.objectCode ?? "?";
        CachePeekMarker(matched, objectCode);
        return true;
    }

    /// <summary>
    /// Raycast player camera → main mapScreen mesh → UV → closest mapRadarObject rect.
    /// Called from Update (with interact edge) and from SwitchRadarTargetForward / InteractTrigger prefixes.
    /// </summary>
    /// <param name="forceMapContext">
    /// When true (SwitchRadarTargetForward on mapScreen / near-map interact), still attempt UV→marker
    /// even if the first physics hit was not the mesh (bounds / projection fallback).
    /// </param>
    /// <param name="alwaysLog">When true, LogInfo every outcome including resolve miss (never silent).</param>
    internal static bool TryActivateFromCurrentLook(
        bool requireInteractPress,
        bool forceMapContext = false,
        bool alwaysLog = false)
    {
        HostModGate.EnsureRegistered();
        if (Plugin.Enabled == null || !Plugin.Enabled.Value)
            return false;

        if (!HostModGate.FeaturesActive)
        {
            if (requireInteractPress || alwaysLog || WasInteractPressed(GameNetworkManager.Instance?.localPlayerController))
                LogGateBlocked();
            return false;
        }

        if (_cooldown > 0f)
            return false;

        var gnm = GameNetworkManager.Instance;
        var player = gnm != null ? gnm.localPlayerController : null;
        if (player == null || player.isPlayerDead || !player.isPlayerControlled)
            return false;

        if (!player.isInHangarShipRoom)
            return false;

        if (requireInteractPress && !WasInteractPressed(player))
            return false;

        int markers = CountActiveMarkers();

        // 1.0.10: tip → E sync — if hover tip just resolved a marker, activate that same object.
        if (TryGetFreshPeekCache(out var cachedTao, out string cachedCode))
        {
            Plugin.Log.LogInfo($"MapClick: via=peek-cache code={cachedCode}");
            _cooldown = 0.28f;
            Plugin.ClickConsumedThisFrame = true;
            ClearPeekCache();
            bool cachedRan = ActivateCode(
                cachedTao, 0f, "via=peek-cache", Vector2.zero, "peek-cache", markers, "peek-cache");
            return cachedRan;
        }

        // Never use centerFallback on activate — low-confidence must not activate.
        // requireMeshUv false (1.0.8): same UV→marker path as hover tip (prefer mesh UV, else bounds).
        if (!TryResolveLookMarker(
                player,
                forceMapContext,
                alwaysLog,
                allowCenterFallback: false,
                requireMeshUv: false,
                out var matched,
                out float score,
                out Vector2 bestUv,
                out string uvTag,
                out string matchHow,
                out string hitDiag))
        {
            // UV messy / ss-uv-no-aabb — still activate whatever the tip just showed.
            if (TryGetFreshPeekCache(out cachedTao, out cachedCode))
            {
                Plugin.Log.LogInfo($"MapClick: via=peek-cache code={cachedCode} (after resolve miss)");
                _cooldown = 0.28f;
                Plugin.ClickConsumedThisFrame = true;
                ClearPeekCache();
                return ActivateCode(
                    cachedTao, 0f, "via=peek-cache", Vector2.zero, "peek-cache", markers, "peek-cache");
            }
            return false;
        }

        // High-confidence only: aabb / gvp / bary / phys. center / ray / centerFallback = miss.
        if (!IsHighConfidenceMatch(matchHow))
        {
            // Tip may have cached a center/peek match a frame earlier — retry peek-cache once.
            if (TryGetFreshPeekCache(out cachedTao, out cachedCode))
            {
                Plugin.Log.LogInfo($"MapClick: via=peek-cache code={cachedCode} (after low-conf {matchHow})");
                _cooldown = 0.28f;
                Plugin.ClickConsumedThisFrame = true;
                ClearPeekCache();
                return ActivateCode(
                    cachedTao, 0f, "via=peek-cache", bestUv, "peek-cache", markers, "peek-cache");
            }

            LogClickAttempt(hitDiag, bestUv, uvTag, markers,
                $"ignore-low-conf '{matched.objectCode}' via={matchHow} score={score:F4} (no activate)");
            Plugin.V($"MapClick: low-confidence match via={matchHow} — no activate.");
            return false;
        }

        // Do not Info-claim success until CallFunctionFromTerminal actually ran.
        Plugin.V($"MapClick: matched '{matched.objectCode}' via={matchHow} uvTag={uvTag} vp=({bestUv.x:F3},{bestUv.y:F3}) {hitDiag}");

        _cooldown = 0.28f;
        Plugin.ClickConsumedThisFrame = true; // dedupe InteractTrigger only; radar never suppressed
        ClearPeekCache();
        bool ran = ActivateCode(matched!, score, hitDiag, bestUv, uvTag, markers, matchHow);
        return ran;
    }

    /// <summary>
    /// Matches that may activate a code. center / centerFallback / loose ray are too loose
    /// (false positives on empty map) and must never activate. Radar is never suppressed (1.0.7).
    /// aabb may come from mesh UV or bounds UV (same path as hover tip, 1.0.8).
    /// </summary>
    private static bool IsHighConfidenceMatch(string matchHow)
    {
        if (string.IsNullOrEmpty(matchHow))
            return false;
        // aabb = padded rect hit (mesh or bounds UV); bary/phys if tagged as how.
        // ray removed in 1.0.7 — loose viewport-ray must not activate.
        return matchHow == "aabb"
               || matchHow == "gvp"
               || matchHow == "bary"
               || matchHow == "phys"
               || matchHow == "peek-cache";
    }

    private static void CachePeekMarker(TerminalAccessibleObject tao, string code)
    {
        _peekCacheTao = tao;
        _peekCacheCode = code ?? "";
        _peekCacheTime = Time.unscaledTime;
        _peekCacheNetId = 0UL;
        try
        {
            if (tao != null && tao.NetworkObject != null)
                _peekCacheNetId = tao.NetworkObject.NetworkObjectId;
        }
        catch
        {
            /* NetworkObject may be unset */
        }
    }

    private static void ClearPeekCache()
    {
        _peekCacheTao = null;
        _peekCacheCode = "";
        _peekCacheTime = -999f;
        _peekCacheNetId = 0UL;
    }

    /// <summary>
    /// True when hover tip cached a marker within PeekCacheMaxAge and it is still valid/active.
    /// </summary>
    private static bool TryGetFreshPeekCache(out TerminalAccessibleObject tao, out string code)
    {
        tao = null!;
        code = "";
        if (_peekCacheTao == null)
            return false;
        if (Time.unscaledTime - _peekCacheTime > PeekCacheMaxAge)
            return false;
        // Unity fake-null for destroyed objects.
        if (_peekCacheTao == null)
            return false;
        try
        {
            if (!_peekCacheTao.isActiveAndEnabled && !_peekCacheTao.gameObject.activeInHierarchy)
                return false;
            if (_peekCacheTao.mapRadarObject != null && !_peekCacheTao.mapRadarObject.activeInHierarchy)
                return false;
        }
        catch
        {
            return false;
        }

        tao = _peekCacheTao;
        code = !string.IsNullOrEmpty(_peekCacheCode) ? _peekCacheCode : (_peekCacheTao.objectCode ?? "?");
        return true;
    }

    /// <summary>
    /// Shared look → mesh → UV → marker resolve used by activate and hover peek.
    /// </summary>
    private static bool TryResolveLookMarker(
        PlayerControllerB player,
        bool forceMapContext,
        bool alwaysLog,
        bool allowCenterFallback,
        bool requireMeshUv,
        out TerminalAccessibleObject matched,
        out float score,
        out Vector2 bestUv,
        out string uvTag,
        out string matchHow,
        out string hitDiag)
    {
        matched = null!;
        score = float.MaxValue;
        bestUv = default;
        uvTag = "none";
        matchHow = "none";
        hitDiag = "via=none";
        _hasPhysicsUv = false;
        _physicsUv = default;
        _physicsUvTag = "none";

        var sor = StartOfRound.Instance;
        var map = sor != null ? sor.mapScreen : null;
        if (map == null || map.mapCamera == null)
        {
            if (alwaysLog)
                Plugin.Log.LogInfo("MapClick: no mapScreen / mapCamera.");
            return false;
        }

        var cam = player.gameplayCamera != null ? player.gameplayCamera : Camera.main;
        if (cam == null)
            return false;

        float range = Plugin.InteractRange != null ? Plugin.InteractRange.Value : 4.5f;
        var ray = new Ray(cam.transform.position, cam.transform.forward);

        Camera markerCam = map.mapCamera;
        MeshRenderer screenMesh;
        Vector3 hitPoint;
        string crewDiag = "crew=skip";
        bool resolved = false;

        // Main ship mapScreen wins when both could match; CrewMonitors only if main fails.
        // Main path never requires CrewMonitors (optional soft-dep for map-feed panels only).
        if (TryResolveMapScreenHit(ray, range, map, forceMapContext, out screenMesh, out hitPoint, out hitDiag))
        {
            // main mapScreen path — markerCam stays map.mapCamera (MeshCollider look UV already enriched)
            resolved = true;
        }
        else
        {
            try
            {
                if (TryResolveCrewMonitorMapHit(ray, range, out screenMesh, out hitPoint, out Camera crewCam, out crewDiag))
                {
                    markerCam = crewCam;
                    hitDiag = crewDiag;
                    // Crew panels: also MeshCollider look-ray for textureCoord when possible.
                    if (!_hasPhysicsUv)
                    {
                        var enrichSb = new StringBuilder(24);
                        TryEnrichMeshColliderLookUv(ray, Mathf.Max(range, 10f), screenMesh, ref hitPoint, enrichSb);
                        hitDiag += enrichSb.ToString();
                    }
                    resolved = true;
                }
            }
            catch (Exception ex)
            {
                Plugin.V($"CrewMonitors resolve skipped: {ex.Message}");
                crewDiag = "crew=skip";
                resolved = false;
            }
        }

        if (!resolved)
        {
            if (!string.IsNullOrEmpty(crewDiag) && crewDiag != "crew=skip")
                hitDiag = (hitDiag ?? "via=none") + " | " + crewDiag;
            // Activate path logs on alwaysLog; peek (alwaysLog=false) must not spam even under force.
            if (alwaysLog)
                Plugin.Log.LogInfo($"MapClick: resolve-fail {hitDiag}");
            else
                Plugin.V($"Map look miss (no mesh / out of range). {hitDiag}");
            return false;
        }

        if (markerCam == null)
        {
            if (alwaysLog)
                Plugin.Log.LogInfo($"MapClick: resolve-fail {hitDiag} (markerCam null)");
            return false;
        }

        if (!TryPickBestMarker(player, markerCam, hitPoint, screenMesh, requireMeshUv, out matched,
                out score, out bestUv, out uvTag, out matchHow))
        {
            if (allowCenterFallback && TryPickCenterFallback(markerCam, out matched, out score, out bestUv, out uvTag, out matchHow))
                return true;

            int markerCount = CountActiveMarkers();
            string why = DescribeMissWhy(uvTag, _hasPhysicsUv);
            // Peek: never Info-log every frame. Activate edge: Info only when markers>0; else Verbose.
            if (alwaysLog && markerCount > 0)
                LogClickAttempt(hitDiag, bestUv, uvTag ?? "none", markerCount, $"miss why={why}");
            else
                Plugin.V($"MapClick: miss why={why} markers={markerCount} {hitDiag} uvTag={uvTag}");
            return false;
        }

        return true;
    }

    /// <summary>
    /// True when the player's look ray is near map.mesh / mesh2 or a CrewMonitors map-feed panel
    /// (for Tick / InteractTrigger forceMapContext).
    /// </summary>
    internal static bool IsLookingNearMapScreen(PlayerControllerB player)
    {
        var sor = StartOfRound.Instance;
        var map = sor != null ? sor.mapScreen : null;
        if (map == null)
            return false;

        var cam = player.gameplayCamera != null ? player.gameplayCamera : Camera.main;
        if (cam == null)
            return false;

        float range = Plugin.InteractRange != null ? Plugin.InteractRange.Value : 4.5f;
        var ray = new Ray(cam.transform.position, cam.transform.forward);

        MeshRenderer? m1 = map.mesh;
        MeshRenderer? m2 = map.mesh2;
        if (m1 != null && (IsWithinMapScreenRange(ray.origin, m1, range) || m1.bounds.IntersectRay(ray)))
            return true;
        if (m2 != null && (IsWithinMapScreenRange(ray.origin, m2, range) || m2.bounds.IntersectRay(ray)))
            return true;

        // Soft: looking at a CrewMonitors map-feed monitor panel (optional; never required).
        try
        {
            if (TryResolveCrewMonitorMapHit(ray, range, out _, out _, out _, out _))
                return true;
        }
        catch (Exception ex)
        {
            Plugin.V($"CrewMonitors near-check skipped: {ex.Message}");
        }

        return false;
    }

    /// <summary>
    /// Prefer a RaycastAll / Collider.Raycast hit owned by map.mesh / mesh2 (even if not first).
    /// Under force/near: Bounds.IntersectRay → Plane face → ClosestBounds last (AABB invents UV junk).
    /// </summary>
    private static bool TryResolveMapScreenHit(
        Ray ray,
        float range,
        ManualCameraRenderer map,
        bool forceMapContext,
        out MeshRenderer screenMesh,
        out Vector3 hitPoint,
        out string diag)
    {
        screenMesh = null!;
        hitPoint = default;
        var sb = new StringBuilder(160);

        // Optionally add MeshCollider so Collider.Raycast + textureCoord work behind Terminal blockers.
        EnsureRuntimeMapMeshCollider(map.mesh, ref _ensuredMeshColliderMesh);
        EnsureRuntimeMapMeshCollider(map.mesh2, ref _ensuredMeshColliderMesh2);

        bool meshHasMc = HasMeshCollider(map.mesh);
        bool mesh2HasMc = HasMeshCollider(map.mesh2);
        sb.Append("mc=");
        sb.Append(meshHasMc ? "1" : "0");
        sb.Append('/');
        sb.Append(mesh2HasMc ? "1" : "0");
        sb.Append(' ');

        int hitCount = Physics.RaycastNonAlloc(ray, RayHitBuf, range, ~0, QueryTriggerInteraction.Ignore);
        // Sort by distance (NonAlloc does not guarantee order).
        for (int i = 0; i < hitCount - 1; i++)
        {
            for (int j = i + 1; j < hitCount; j++)
            {
                if (RayHitBuf[j].distance < RayHitBuf[i].distance)
                {
                    var tmp = RayHitBuf[i];
                    RayHitBuf[i] = RayHitBuf[j];
                    RayHitBuf[j] = tmp;
                }
            }
        }

        int logN = Mathf.Min(hitCount, 5);
        sb.Append("hits=");
        sb.Append(hitCount);
        sb.Append('[');
        for (int i = 0; i < logN; i++)
        {
            if (i > 0) sb.Append(',');
            var c = RayHitBuf[i].collider;
            sb.Append(c != null ? c.name : "?");
            sb.Append('@');
            sb.Append(RayHitBuf[i].distance.ToString("F2"));
        }
        if (hitCount > logN) sb.Append(",...");
        sb.Append(']');

        // 1) Prefer any RaycastAll hit owned by mesh/mesh2.
        for (int i = 0; i < hitCount; i++)
        {
            if (IsMainMapScreenHit(RayHitBuf[i], map, out MeshRenderer mr))
            {
                screenMesh = mr;
                hitPoint = RayHitBuf[i].point;
                RememberPhysicsUv(RayHitBuf[i], "phys|RaycastAll");
                // OwnsRenderer may match a non-MeshCollider; always MeshCollider.Raycast look ray for textureCoord.
                TryEnrichMeshColliderLookUv(ray, Mathf.Max(range, 10f), mr, ref hitPoint, sb);
                sb.Append(" via=RaycastAll#");
                sb.Append(i);
                diag = sb.ToString();
                return true;
            }
        }

        // 2) Direct Collider.Raycast on mesh/mesh2 (bypass Terminal blockers). Try InteractRange + 8–12m.
        float meshCastRange = Mathf.Max(range, 10f);
        if (TryColliderRaycastMesh(ray, meshCastRange, map.mesh, out hitPoint, out screenMesh))
        {
            TryEnrichMeshColliderLookUv(ray, meshCastRange, screenMesh, ref hitPoint, sb);
            sb.Append(" via=Collider.Raycast(mesh)");
            diag = sb.ToString();
            return true;
        }

        if (TryColliderRaycastMesh(ray, meshCastRange, map.mesh2, out hitPoint, out screenMesh))
        {
            TryEnrichMeshColliderLookUv(ray, meshCastRange, screenMesh, ref hitPoint, sb);
            sb.Append(" via=Collider.Raycast(mesh2)");
            diag = sb.ToString();
            return true;
        }

        MeshRenderer? prefer = map.mesh != null ? map.mesh : map.mesh2;
        if (prefer == null)
        {
            diag = sb.Append(" via=none").ToString();
            return false;
        }

        bool nearScreen = IsWithinMapScreenRange(ray.origin, prefer, range)
                          || (map.mesh2 != null && IsWithinMapScreenRange(ray.origin, map.mesh2, range));

        if (!forceMapContext && !nearScreen)
        {
            diag = sb.Append(" via=none").ToString();
            return false;
        }

        // 3) Bounds.IntersectRay — real ray∩AABB (better than ClosestBounds phantom).
        float intersectSlack = Mathf.Max(range + 0.5f, 12f);
        if (TryBoundsIntersectRay(ray, prefer, intersectSlack, out hitPoint))
        {
            screenMesh = prefer;
            TryEnrichMeshColliderLookUv(ray, intersectSlack, prefer, ref hitPoint, sb);
            sb.Append(forceMapContext ? " via=IntersectRay(force)" : " via=IntersectRay(near)");
            diag = sb.ToString();
            return true;
        }

        if (map.mesh2 != null && !ReferenceEquals(map.mesh2, prefer)
            && TryBoundsIntersectRay(ray, map.mesh2, intersectSlack, out hitPoint))
        {
            screenMesh = map.mesh2;
            TryEnrichMeshColliderLookUv(ray, intersectSlack, map.mesh2, ref hitPoint, sb);
            sb.Append(forceMapContext ? " via=IntersectRay2(force)" : " via=IntersectRay2(near)");
            diag = sb.ToString();
            return true;
        }

        // 4) Plane projection on mesh face BEFORE ClosestBounds (AABB invents UV≈0,0 misses).
        if (TryProjectOntoMeshPlane(ray, prefer, out hitPoint))
        {
            screenMesh = prefer;
            TryEnrichMeshColliderLookUv(ray, 12f, prefer, ref hitPoint, sb);
            sb.Append(forceMapContext ? " via=plane(force)" : " via=plane(near)");
            diag = sb.ToString();
            return true;
        }

        if (map.mesh2 != null && !ReferenceEquals(map.mesh2, prefer)
            && TryProjectOntoMeshPlane(ray, map.mesh2, out hitPoint))
        {
            screenMesh = map.mesh2;
            TryEnrichMeshColliderLookUv(ray, 12f, map.mesh2, ref hitPoint, sb);
            sb.Append(forceMapContext ? " via=plane2(force)" : " via=plane2(near)");
            diag = sb.ToString();
            return true;
        }

        // 5) ClosestBounds last resort under force only — UV must come from bary/phys later.
        if (forceMapContext)
        {
            if (TryClosestBoundsOnRay(ray, prefer, out hitPoint))
            {
                screenMesh = prefer;
                TryEnrichMeshColliderLookUv(ray, 12f, prefer, ref hitPoint, sb);
                sb.Append(" via=ClosestBounds(force)");
                diag = sb.ToString();
                return true;
            }

            if (map.mesh2 != null && !ReferenceEquals(map.mesh2, prefer)
                && TryClosestBoundsOnRay(ray, map.mesh2, out hitPoint))
            {
                screenMesh = map.mesh2;
                TryEnrichMeshColliderLookUv(ray, 12f, map.mesh2, ref hitPoint, sb);
                sb.Append(" via=ClosestBounds2(force)");
                diag = sb.ToString();
                return true;
            }
        }

        diag = sb.Append(" via=none").ToString();
        return false;
    }

    /// <summary>
    /// True when hoveringOverTrigger looks like the map / radar / monitor interact (not Terminal).
    /// </summary>
    internal static bool IsMapRelatedInteractTrigger(InteractTrigger? trigger)
    {
        if (trigger == null)
            return false;
        string n = trigger.gameObject != null ? (trigger.gameObject.name ?? "") : "";
        string tip = trigger.hoverTip ?? "";
        // Concatenate common tip fields if present.
        try
        {
            if (trigger.hoverTip == null && tip.Length == 0)
                tip = "";
        }
        catch { /* ignore */ }

        return ContainsMapInteractKeyword(n) || ContainsMapInteractKeyword(tip);
    }

    private static bool ContainsMapInteractKeyword(string s)
    {
        if (string.IsNullOrEmpty(s))
            return false;
        return s.IndexOf("Map", StringComparison.OrdinalIgnoreCase) >= 0
               || s.IndexOf("Switch", StringComparison.OrdinalIgnoreCase) >= 0
               || s.IndexOf("Radar", StringComparison.OrdinalIgnoreCase) >= 0
               || s.IndexOf("monitor", StringComparison.OrdinalIgnoreCase) >= 0
               || s.IndexOf("Monitor", StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool HasMeshCollider(MeshRenderer? mesh)
    {
        if (mesh == null)
            return false;
        if (mesh.GetComponent<MeshCollider>() != null)
            return true;
        return mesh.GetComponentInChildren<MeshCollider>() != null;
    }

    /// <summary>
    /// Once per mesh: add a non-convex MeshCollider from MeshFilter so Collider.Raycast + textureCoord
    /// work when the vanilla map mesh has no physics collider (Terminal occludes RaycastAll).
    /// </summary>
    private static void EnsureRuntimeMapMeshCollider(MeshRenderer? mesh, ref bool ensuredFlag)
    {
        if (ensuredFlag || mesh == null)
            return;
        ensuredFlag = true;

        if (HasMeshCollider(mesh))
            return;

        var filter = mesh.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            return;

        // Non-readable meshes cannot be safely cooked into a new MeshCollider without Unity errors.
        if (!IsMeshCpuReadable(filter.sharedMesh))
        {
            LogNonReadableSkipOnce(filter.sharedMesh, "MeshCollider-add");
            return;
        }

        try
        {
            var mc = mesh.gameObject.AddComponent<MeshCollider>();
            mc.convex = false;
            mc.sharedMesh = filter.sharedMesh;
            Plugin.V($"MapClick: added runtime MeshCollider on '{mesh.name}'.");
        }
        catch (Exception ex)
        {
            Plugin.V($"MapClick: MeshCollider add failed on '{mesh.name}': {ex.Message}");
        }
    }

    private static bool TryBoundsIntersectRay(Ray ray, MeshRenderer mesh, float maxDist, out Vector3 hitPoint)
    {
        hitPoint = default;
        if (mesh == null)
            return false;
        Bounds b = mesh.bounds;
        if (!b.IntersectRay(ray, out float dist))
            return false;
        if (dist < 0f || dist > maxDist)
            return false;
        hitPoint = ray.GetPoint(dist);
        return true;
    }

    /// <summary>
    /// Project bounds center onto the ray, then ClosestPoint on AABB — guarantees a hitPoint when mesh exists.
    /// </summary>
    private static bool TryClosestBoundsOnRay(Ray ray, MeshRenderer mesh, out Vector3 hitPoint)
    {
        hitPoint = default;
        if (mesh == null)
            return false;

        Bounds b = mesh.bounds;
        Vector3 dir = ray.direction.sqrMagnitude > 1e-8f ? ray.direction.normalized : Vector3.forward;
        float t = Vector3.Dot(b.center - ray.origin, dir);
        if (t < 0f)
            t = 0f;
        if (t > 20f)
            t = 20f;

        Vector3 onRay = ray.origin + dir * t;
        hitPoint = b.ClosestPoint(onRay);

        // If ClosestPoint collapsed to a far corner while looking past the screen, fall back to center.
        if (Vector3.Distance(hitPoint, onRay) > b.size.magnitude + 0.5f)
            hitPoint = b.center;

        // Snap AABB phantom points onto the MeshCollider surface when possible (helps UV).
        var mc = mesh.GetComponent<MeshCollider>();
        if (mc == null)
            mc = mesh.GetComponentInChildren<MeshCollider>();
        if (mc != null)
        {
            try
            {
                Vector3 snapped = mc.ClosestPoint(hitPoint);
                if ((snapped - hitPoint).sqrMagnitude < 0.25f)
                    hitPoint = snapped;
            }
            catch
            {
                /* ClosestPoint may throw on some non-convex setups */
            }
        }

        return true;
    }

    private static bool TryColliderRaycastMesh(
        Ray ray,
        float range,
        MeshRenderer? mesh,
        out Vector3 hitPoint,
        out MeshRenderer screenMesh)
    {
        hitPoint = default;
        screenMesh = null!;
        if (mesh == null)
            return false;

        var cols = mesh.GetComponentsInChildren<Collider>(true);
        float bestDist = float.MaxValue;
        bool any = false;
        Vector3 bestPoint = default;
        RaycastHit bestHit = default;

        for (int i = 0; i < cols.Length; i++)
        {
            var col = cols[i];
            if (col == null || !col.enabled)
                continue;
            if (col.Raycast(ray, out RaycastHit hit, range) && hit.distance < bestDist)
            {
                bestDist = hit.distance;
                bestPoint = hit.point;
                bestHit = hit;
                any = true;
            }
        }

        var parentCols = mesh.GetComponentsInParent<Collider>(true);
        for (int i = 0; i < parentCols.Length; i++)
        {
            var col = parentCols[i];
            if (col == null || !col.enabled)
                continue;
            if (col.Raycast(ray, out RaycastHit hit, range) && hit.distance < bestDist)
            {
                bestDist = hit.distance;
                bestPoint = hit.point;
                bestHit = hit;
                any = true;
            }
        }

        if (!any)
            return false;

        hitPoint = bestPoint;
        screenMesh = mesh;
        RememberPhysicsUv(bestHit, "phys|ColliderRaycast");
        return true;
    }

    private static void RememberPhysicsUv(RaycastHit hit, string tag)
    {
        // MeshCollider provides textureCoord; other colliders often return (0,0).
        if (hit.collider is MeshCollider)
        {
            _physicsUv = hit.textureCoord;
            _physicsUvTag = tag;
            _hasPhysicsUv = true;
        }
    }

    /// <summary>
    /// MeshCollider.Raycast the player look ray for textureCoord (1.0.8).
    /// RaycastAll often hits a non-MeshCollider owned by the map — RememberPhysicsUv no-ops then.
    /// Updates hitPoint to the MeshCollider surface for better barycentric.
    /// </summary>
    private static void TryEnrichMeshColliderLookUv(
        Ray lookRay,
        float range,
        MeshRenderer screenMesh,
        ref Vector3 hitPoint,
        StringBuilder? sb)
    {
        if (screenMesh == null)
            return;

        var mc = GetOrAddMapMeshCollider(screenMesh);
        if (mc == null)
        {
            sb?.Append(" mcLook=0");
            return;
        }

        if (!mc.Raycast(lookRay, out RaycastHit hit, range))
        {
            sb?.Append(" mcLook=0");
            return;
        }

        hitPoint = hit.point;
        RememberPhysicsUv(hit, "phys|MeshColliderLook");
        sb?.Append(" mcLook=1");
    }

    /// <summary>Return existing MeshCollider or add a non-convex one from MeshFilter (once).</summary>
    private static MeshCollider? GetOrAddMapMeshCollider(MeshRenderer screenMesh)
    {
        var mc = screenMesh.GetComponent<MeshCollider>();
        if (mc == null)
            mc = screenMesh.GetComponentInChildren<MeshCollider>();
        if (mc != null)
            return mc;

        var filter = screenMesh.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            return null;

        if (!IsMeshCpuReadable(filter.sharedMesh))
        {
            LogNonReadableSkipOnce(filter.sharedMesh, "MeshCollider-look");
            return null;
        }

        try
        {
            mc = screenMesh.gameObject.AddComponent<MeshCollider>();
            mc.convex = false;
            mc.sharedMesh = filter.sharedMesh;
            Plugin.V($"MapClick: added runtime MeshCollider on '{screenMesh.name}' (look-ray enrich).");
            return mc;
        }
        catch (Exception ex)
        {
            Plugin.V($"MapClick: MeshCollider add failed on '{screenMesh.name}': {ex.Message}");
            return null;
        }
    }

    private static string DescribeMissWhy(string? uvTag, bool havePhysicsUv)
    {
        if (uvTag != null && uvTag.IndexOf("ss-only", StringComparison.Ordinal) >= 0)
            return "ss-uv-no-aabb";
        if (uvTag != null && uvTag.IndexOf("bounds-only", StringComparison.Ordinal) >= 0)
            return "bounds-uv-no-aabb";
        if (uvTag != null && uvTag.IndexOf("junkUV", StringComparison.Ordinal) >= 0)
            return "junk-uv";
        if (!havePhysicsUv && (uvTag == null || uvTag.StartsWith("tried:", StringComparison.Ordinal)
                               || (uvTag != null && uvTag.IndexOf("bounds", StringComparison.Ordinal) >= 0)))
            return "no-mesh-uv-no-aabb";
        return "no-aabb";
    }

    private static bool IsWithinMapScreenRange(Vector3 origin, MeshRenderer mesh, float range)
    {
        Bounds b = mesh.bounds;
        float dist = Vector3.Distance(origin, b.ClosestPoint(origin));
        return dist <= range + 0.35f;
    }

    private static bool TryProjectOntoMeshPlane(Ray ray, MeshRenderer mesh, out Vector3 hitPoint)
    {
        hitPoint = default;
        Transform t = mesh.transform;
        Plane plane = new Plane(t.forward, t.position);
        var filter = mesh.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
        {
            Bounds lb = filter.sharedMesh.bounds;
            Vector3 size = lb.size;
            Vector3 localNormal;
            if (size.x <= size.y && size.x <= size.z)
                localNormal = Vector3.right;
            else if (size.y <= size.x && size.y <= size.z)
                localNormal = Vector3.up;
            else
                localNormal = Vector3.forward;

            Vector3 worldNormal = t.TransformDirection(localNormal).normalized;
            Vector3 center = t.TransformPoint(lb.center);
            plane = new Plane(worldNormal, center);

            if (Vector3.Dot(plane.normal, ray.origin - center) < 0f)
                plane = new Plane(-plane.normal, center);
        }
        else
        {
            if (Vector3.Dot(plane.normal, ray.origin - t.position) < 0f)
                plane = new Plane(-plane.normal, t.position);
        }

        if (!plane.Raycast(ray, out float enter) || enter < 0f || enter > 12f)
            return false;

        hitPoint = ray.GetPoint(enter);

        Bounds wb = mesh.bounds;
        wb.Expand(0.08f);
        if (!wb.Contains(hitPoint))
        {
            Vector3 closest = wb.ClosestPoint(hitPoint);
            if (Vector3.Distance(closest, hitPoint) > 0.15f)
                return false;
            hitPoint = closest;
        }

        return true;
    }

    private static void LogClickAttempt(string hitDiag, Vector2 uv, string flipUsed, int markerCount, string best)
    {
        string markerNote = markerCount == 0 ? " " + ExplainZeroMarkers() : "";
        Plugin.Log.LogInfo(
            $"MapClick: {hitDiag} UV=({uv.x:F3},{uv.y:F3}) flip={flipUsed} markers={markerCount}{markerNote} → {best}");
    }

    private static int CountActiveMarkers()
    {
        int n = 0;
        var objs = UnityEngine.Object.FindObjectsOfType<TerminalAccessibleObject>();
        for (int i = 0; i < objs.Length; i++)
        {
            var tao = objs[i];
            if (tao != null && tao.mapRadarObject != null && tao.mapRadarObject.activeInHierarchy)
                n++;
        }
        return n;
    }

    /// <summary>Why markers=0: orbit/ship (no dungeon codes) vs TAOs without active mapRadarObject.</summary>
    private static string ExplainZeroMarkers()
    {
        var objs = UnityEngine.Object.FindObjectsOfType<TerminalAccessibleObject>();
        int total = objs.Length;
        int withRadarObj = 0;
        int withRadarText = 0;
        for (int i = 0; i < objs.Length; i++)
        {
            var tao = objs[i];
            if (tao == null)
                continue;
            if (tao.mapRadarObject != null)
                withRadarObj++;
            try
            {
                if (tao.mapRadarText != null && !string.IsNullOrEmpty(tao.mapRadarText.text))
                    withRadarText++;
            }
            catch
            {
                /* publicized field; ignore */
            }
        }

        if (total == 0)
            return "(why: no TerminalAccessibleObject in scene — orbit/ship phase or not landed)";
        return $"(why: tao={total} withMapRadarObj={withRadarObj} withMapRadarText={withRadarText}; none active — orbit/ship or radar UI not shown)";
    }

    private static void LogGateBlocked()
    {
        if (_gateWarnCooldown > 0f)
            return;
        _gateWarnCooldown = 2.5f;
        if (HostModGate.WaitingForHostHello)
            Plugin.Log.LogWarning("Map click blocked: HostModGate waiting for host hello (retrying sync).");
        else
            Plugin.Log.LogWarning("Map click blocked: HostModGate FeaturesActive=false (host missing mod or disabled).");
    }

    private static bool IsMainMapScreenHit(RaycastHit hit, ManualCameraRenderer map, out MeshRenderer screenMesh)
    {
        screenMesh = null!;
        if (hit.collider == null)
            return false;

        if (OwnsRenderer(hit, map.mesh))
        {
            screenMesh = map.mesh;
            return true;
        }

        if (OwnsRenderer(hit, map.mesh2))
        {
            screenMesh = map.mesh2!;
            return true;
        }

        return false;
    }

    private static bool OwnsRenderer(RaycastHit hit, MeshRenderer? mesh)
    {
        if (mesh == null)
            return false;

        var t = hit.collider.transform;
        return t == mesh.transform
               || t.IsChildOf(mesh.transform)
               || mesh.transform.IsChildOf(t)
               || hit.collider.GetComponent<MeshRenderer>() == mesh
               || hit.collider.GetComponentInParent<MeshRenderer>() == mesh;
    }

    /// <summary>
    /// Build UV candidates: screen-space monitor UV (1.0.9 primary), plus phys/tex/bary bonus,
    /// else bounds orientations. Score markers across candidates; pick global best aabb.
    /// Peek + activate share this path. Prefer ss over bounds-axis guesses.
    /// </summary>
    private static bool TryPickBestMarker(
        PlayerControllerB player,
        Camera mapCamera,
        Vector3 hitPoint,
        MeshRenderer screenMesh,
        bool requireMeshUv,
        out TerminalAccessibleObject matched,
        out float bestScore,
        out Vector2 bestUv,
        out string bestUvTag,
        out string matchHow)
    {
        matched = null!;
        bestScore = float.MaxValue;
        bestUv = default;
        bestUvTag = "none";
        matchHow = "none";

        // 1.0.10: gameplay-viewport marker distance — prefer over broken ss AABB (V≈0 / U≈0).
        if (TryMatchMarkersByGameplayViewport(
                player, mapCamera, hitPoint, screenMesh,
                out matched, out bestScore, out bestUv, out bestUvTag, out matchHow))
        {
            Plugin.V($"MapClick: GVP win how={matchHow} score={bestScore:F4} tag={bestUvTag}");
            return true;
        }

        CollectUvCandidates(player, hitPoint, screenMesh, UvCandidates);
        if (UvCandidates.Count == 0)
            return false;

        bool anyMeshUv = false;
        bool anySs = false;
        for (int i = 0; i < UvCandidates.Count; i++)
        {
            string t = UvCandidates[i].tag;
            if (t.StartsWith("phys", StringComparison.Ordinal)
                || t.StartsWith("tex", StringComparison.Ordinal)
                || t.StartsWith("bary", StringComparison.Ordinal))
                anyMeshUv = true;
            if (t.StartsWith("ss", StringComparison.Ordinal))
                anySs = true;
        }

        // Prefer config FlipUvV as a mild tie-break (still try all flips/orientations).
        bool preferFlipV = Plugin.FlipUvV == null || Plugin.FlipUvV.Value;

        TerminalAccessibleObject? best = null;
        float bestRank = float.MaxValue;
        Vector2 bestUvLocal = default;
        string bestTag = "none";
        string bestHow = "none";

        for (int u = 0; u < UvCandidates.Count; u++)
        {
            var (uv, tag) = UvCandidates[u];
            bool isMeshUv = tag.StartsWith("phys", StringComparison.Ordinal)
                            || tag.StartsWith("tex", StringComparison.Ordinal)
                            || tag.StartsWith("bary", StringComparison.Ordinal);
            bool isSs = tag.StartsWith("ss", StringComparison.Ordinal);
            bool isBounds = tag.IndexOf("bounds", StringComparison.Ordinal) >= 0;

            // Screen-space or mesh UV present → never let U=0 bounds-only win.
            if ((anySs || anyMeshUv) && isBounds)
                continue;
            if (requireMeshUv && !isMeshUv && !isSs && anyMeshUv)
                continue;

            // Weak ss-only edge UV (V≈0 / U≈0) must not win over mesh UV — GVP already tried.
            if (isSs && IsEdgeUv(uv) && anyMeshUv)
                continue;
            // Soft-skip edge ss when only ss exists (GVP already preferred; don't force false aabb).
            if (isSs && IsEdgeUv(uv) && !anyMeshUv)
                continue;

            // Prefer ss / phys/tex/bary over noisy bounds-axis guesses; FlipUvV mild tie-break.
            float tagBias = 0f;
            if (isSs) tagBias -= 0.55f;
            if (isMeshUv) tagBias -= 0.45f;
            if (isBounds) tagBias += 0.35f;
            if (isBounds && IsEdgeUv(uv)) tagBias += 0.5f;
            bool hasFlipV = tag.IndexOf("vFlip", StringComparison.Ordinal) >= 0;
            bool hasRawV = tag.IndexOf("vRaw", StringComparison.Ordinal) >= 0;
            if (preferFlipV && hasFlipV) tagBias -= 0.001f;
            else if (!preferFlipV && hasRawV) tagBias -= 0.001f;

            if (!TryScoreMarkersAtViewport(mapCamera, uv, out var m, out float score, out string how, out float rank))
                continue;

            float adjusted = rank + tagBias;
            if (adjusted < bestRank)
            {
                bestRank = adjusted;
                best = m;
                bestScore = score;
                bestUvLocal = uv;
                bestTag = tag;
                bestHow = how;
            }
        }

        if (best == null)
        {
            bestUv = UvCandidates[0].uv;
            if (anySs && !anyMeshUv)
                bestUvTag = "ss-only|tried:" + UvCandidates.Count;
            else if (!anyMeshUv && !anySs)
                bestUvTag = "bounds-only|tried:" + UvCandidates.Count;
            else
                bestUvTag = IsCornerJunkUv(bestUv)
                    ? "junkUV|tried:" + UvCandidates.Count
                    : "tried:" + UvCandidates.Count;
            return false;
        }

        matched = best;
        bestUv = bestUvLocal;
        bestUvTag = (anySs || anyMeshUv) ? bestTag : bestTag + "|picked";
        matchHow = bestHow;
        Plugin.V($"MapClick: UV win tag={bestUvTag} how={bestHow} score={bestScore:F4} ss={anySs} meshUv={anyMeshUv}");
        return true;
    }

    private static void CollectUvCandidates(
        PlayerControllerB player,
        Vector3 worldPoint,
        MeshRenderer screenMesh,
        List<(Vector2 uv, string tag)> into)
    {
        into.Clear();

        bool haveMeshUv = false;
        bool haveSs = false;

        // 1.0.9 primary: screen-space UV of what the player sees on the glass (tip + activate).
        if (TryScreenSpaceMonitorUv(player, screenMesh, worldPoint, out Vector2 ssUv) && !IsCornerJunkUv(ssUv))
        {
            AddUvVariants(into, ssUv, "ss");
            haveSs = true;
        }

        // Bonus: physics / tex / bary when MeshCollider UV works.
        if (_hasPhysicsUv && !IsCornerJunkUv(_physicsUv))
        {
            AddUvVariants(into, _physicsUv, _physicsUvTag);
            haveMeshUv = true;
        }

        if (TryMeshColliderTextureUv(screenMesh, worldPoint, out Vector2 texUv) && !IsCornerJunkUv(texUv))
        {
            AddUvVariants(into, texUv, "tex");
            haveMeshUv = true;
        }

        if (TryBarycentricMeshUv(screenMesh, worldPoint, out Vector2 baryUv, out float baryDist)
            && !IsCornerJunkUv(baryUv))
        {
            AddUvVariants(into, baryUv, $"bary|d{baryDist:F3}");
            haveMeshUv = true;
        }

        // Bounds-axis guesses only when screen-space AND mesh UV both failed.
        if (!haveSs && !haveMeshUv)
        {
            var filter = screenMesh.GetComponent<MeshFilter>();
            if (filter == null || filter.sharedMesh == null)
                return;

            Bounds lb = filter.sharedMesh.bounds;
            Vector3 local = screenMesh.transform.InverseTransformPoint(worldPoint);

            float sx = lb.size.x, sy = lb.size.y, sz = lb.size.z;
            if (sx <= sy && sx <= sz)
            {
                AddBoundsOrientation(into, lb, local, 1, 2, "yz");
                AddBoundsOrientation(into, lb, local, 0, 2, "xz");
                AddBoundsOrientation(into, lb, local, 0, 1, "xy");
            }
            else if (sy <= sx && sy <= sz)
            {
                AddBoundsOrientation(into, lb, local, 0, 2, "xz");
                AddBoundsOrientation(into, lb, local, 1, 2, "yz");
                AddBoundsOrientation(into, lb, local, 0, 1, "xy");
            }
            else
            {
                AddBoundsOrientation(into, lb, local, 0, 1, "xy");
                AddBoundsOrientation(into, lb, local, 1, 2, "yz");
                AddBoundsOrientation(into, lb, local, 0, 2, "xz");
            }

            if (!HasNonJunkUv(into))
                into.Clear();
        }
    }

    private static bool IsEdgeUv(Vector2 uv)
    {
        const float e = 0.02f;
        return uv.x <= e || uv.x >= 1f - e || uv.y <= e || uv.y >= 1f - e;
    }

    /// <summary>True for AABB phantom UV at exact corners like (0,0) / (1,1).</summary>
    private static bool IsCornerJunkUv(Vector2 uv)
    {
        const float e = 0.005f;
        bool uEdge = uv.x <= e || uv.x >= 1f - e;
        bool vEdge = uv.y <= e || uv.y >= 1f - e;
        return uEdge && vEdge;
    }

    private static bool HasNonJunkUv(List<(Vector2 uv, string tag)> list)
    {
        for (int i = 0; i < list.Count; i++)
        {
            if (!IsCornerJunkUv(list[i].uv))
                return true;
        }
        return false;
    }

    private static bool TryMeshColliderTextureUv(MeshRenderer screenMesh, Vector3 worldPoint, out Vector2 uv)
    {
        uv = default;
        var mc = screenMesh.GetComponent<MeshCollider>();
        if (mc == null)
            mc = screenMesh.GetComponentInChildren<MeshCollider>();
        if (mc == null)
            return false;

        Vector3[] dirs =
        {
            screenMesh.transform.forward,
            -screenMesh.transform.forward,
            screenMesh.transform.up,
            -screenMesh.transform.up,
            screenMesh.transform.right,
            -screenMesh.transform.right,
        };

        for (int i = 0; i < dirs.Length; i++)
        {
            Vector3 dir = dirs[i];
            if (dir.sqrMagnitude < 1e-8f)
                continue;
            dir.Normalize();
            var ray = new Ray(worldPoint - dir * 0.2f, dir);
            if (mc.Raycast(ray, out RaycastHit hit, 0.5f))
            {
                uv = hit.textureCoord;
                if (uv.x > 0.001f || uv.y > 0.001f || uv.x + uv.y > 0.001f)
                    return true;
                // Zero UV can still be valid at a corner — accept if hit is close.
                if (hit.distance < 0.45f)
                    return true;
            }
        }

        return false;
    }


    /// <summary>
    /// True when Mesh.vertices / .uv / .triangles are safe to read on the CPU.
    /// Non-readable meshes (import Read/Write off) must never be touched — Unity Error spam.
    /// </summary>
    private static bool IsMeshCpuReadable(Mesh? mesh)
    {
        return mesh != null && mesh.isReadable;
    }

    private static void LogNonReadableSkipOnce(Mesh mesh, string path)
    {
        if (_loggedNonReadableSkip)
            return;
        _loggedNonReadableSkip = true;
        Plugin.V($"MapClick: skip CPU mesh path '{path}' — mesh '{mesh.name}' isReadable=false (use ss/gvp/phys/bounds).");
    }

    private static bool TryBarycentricMeshUv(
        MeshRenderer screenMesh,
        Vector3 worldPoint,
        out Vector2 uv,
        out float dist)
    {
        uv = default;
        dist = float.MaxValue;

        var filter = screenMesh.GetComponent<MeshFilter>();
        if (filter == null || filter.sharedMesh == null)
            return false;

        Mesh mesh = filter.sharedMesh;
        // Cube.001 (ship mapScreen) and many LC meshes are not CPU-readable; accessing
        // vertices/uv/triangles logs endless Unity Errors. Prefer ss/gvp/phys/bounds.
        if (!IsMeshCpuReadable(mesh))
        {
            LogNonReadableSkipOnce(mesh, "bary");
            return false;
        }

        Vector3[] verts = mesh.vertices;
        Vector2[] meshUv = mesh.uv;
        int[] tris = mesh.triangles;
        if (verts == null || meshUv == null || tris == null || meshUv.Length == 0 || tris.Length < 3)
            return false;
        if (meshUv.Length < verts.Length)
            return false;

        Transform t = screenMesh.transform;
        Vector3 local = t.InverseTransformPoint(worldPoint);

        float bestDistSq = float.MaxValue;
        Vector2 bestUv = default;
        bool found = false;

        // Cap triangle walk for huge meshes (ship screen is tiny).
        int triCount = tris.Length / 3;
        int step = triCount > 4000 ? 2 : 1;

        for (int ti = 0; ti < triCount; ti += step)
        {
            int i = ti * 3;
            int ia = tris[i];
            int ib = tris[i + 1];
            int ic = tris[i + 2];
            if (ia >= verts.Length || ib >= verts.Length || ic >= verts.Length)
                continue;
            if (ia >= meshUv.Length || ib >= meshUv.Length || ic >= meshUv.Length)
                continue;

            Vector3 a = verts[ia];
            Vector3 b = verts[ib];
            Vector3 c = verts[ic];
            Vector3 p = ClosestPointOnTriangle(local, a, b, c);
            float dSq = (p - local).sqrMagnitude;
            if (dSq >= bestDistSq)
                continue;

            if (!TryBarycentricWeights(p, a, b, c, out float w0, out float w1, out float w2))
                continue;

            bestDistSq = dSq;
            bestUv = meshUv[ia] * w0 + meshUv[ib] * w1 + meshUv[ic] * w2;
            found = true;
        }

        if (!found)
            return false;

        dist = Mathf.Sqrt(bestDistSq);
        // Reject AABB-corner phantoms far from the actual mesh surface.
        if (dist > 0.12f)
            return false;

        uv = bestUv;
        return true;
    }

    private static Vector3 ClosestPointOnTriangle(Vector3 p, Vector3 a, Vector3 b, Vector3 c)
    {
        Vector3 ab = b - a;
        Vector3 ac = c - a;
        Vector3 ap = p - a;

        float d1 = Vector3.Dot(ab, ap);
        float d2 = Vector3.Dot(ac, ap);
        if (d1 <= 0f && d2 <= 0f)
            return a;

        Vector3 bp = p - b;
        float d3 = Vector3.Dot(ab, bp);
        float d4 = Vector3.Dot(ac, bp);
        if (d3 >= 0f && d4 <= d3)
            return b;

        float vc = d1 * d4 - d3 * d2;
        if (vc <= 0f && d1 >= 0f && d3 <= 0f)
        {
            float v = d1 / (d1 - d3);
            return a + ab * v;
        }

        Vector3 cp = p - c;
        float d5 = Vector3.Dot(ab, cp);
        float d6 = Vector3.Dot(ac, cp);
        if (d6 >= 0f && d5 <= d6)
            return c;

        float vb = d5 * d2 - d1 * d6;
        if (vb <= 0f && d2 >= 0f && d6 <= 0f)
        {
            float w = d2 / (d2 - d6);
            return a + ac * w;
        }

        float va = d3 * d6 - d5 * d4;
        if (va <= 0f && (d4 - d3) >= 0f && (d5 - d6) >= 0f)
        {
            float w = (d4 - d3) / ((d4 - d3) + (d5 - d6));
            return b + (c - b) * w;
        }

        float denom = 1f / (va + vb + vc);
        float v2 = vb * denom;
        float w2 = vc * denom;
        return a + ab * v2 + ac * w2;
    }

    private static bool TryBarycentricWeights(
        Vector3 p, Vector3 a, Vector3 b, Vector3 c,
        out float w0, out float w1, out float w2)
    {
        Vector3 v0 = b - a;
        Vector3 v1 = c - a;
        Vector3 v2 = p - a;
        float d00 = Vector3.Dot(v0, v0);
        float d01 = Vector3.Dot(v0, v1);
        float d11 = Vector3.Dot(v1, v1);
        float d20 = Vector3.Dot(v2, v0);
        float d21 = Vector3.Dot(v2, v1);
        float denom = d00 * d11 - d01 * d01;
        if (Mathf.Abs(denom) < 1e-12f)
        {
            w0 = w1 = w2 = 0f;
            return false;
        }

        w1 = (d11 * d20 - d01 * d21) / denom;
        w2 = (d00 * d21 - d01 * d20) / denom;
        w0 = 1f - w1 - w2;
        return true;
    }

    /// <summary>
    /// Screen-space UV on the visible monitor face (1.0.9).
    /// Build a world quad from mesh/renderer bounds (thinnest axis = normal, face toward gameplay camera),
    /// project corners + look sample with WorldToViewportPoint, then inverse-bilinear to [0,1] UV.
    /// This matches what the player sees under the crosshair — tip code and activate share it.
    /// </summary>
    private static bool TryScreenSpaceMonitorUv(
        PlayerControllerB player,
        MeshRenderer screenMesh,
        Vector3 hitPoint,
        out Vector2 uv)
    {
        uv = default;
        if (player == null || screenMesh == null)
            return false;

        var cam = player.gameplayCamera != null ? player.gameplayCamera : Camera.main;
        if (cam == null)
            return false;

        if (!TryBuildMonitorFaceWorldCorners(screenMesh, cam.transform.position, out Vector3 c0, out Vector3 c1, out Vector3 c2, out Vector3 c3))
            return false;

        // Look sample: prefer projected hitPoint; fallback to gameplay viewport center (crosshair).
        Vector2 sample;
        Vector3 hitVp = cam.WorldToViewportPoint(hitPoint);
        if (hitVp.z > 0f && !float.IsNaN(hitVp.x) && !float.IsNaN(hitVp.y))
            sample = new Vector2(hitVp.x, hitVp.y);
        else
            sample = new Vector2(0.5f, 0.5f);

        // 1.0.10: try multiple corner windings — inverse bilinear can collapse to V≈0/U≈0
        // with a bad winding while the hit is clearly on the glass.
        Vector3[][] windings =
        {
            new[] { c0, c1, c2, c3 }, // BL,BR,TR,TL
            new[] { c0, c3, c2, c1 }, // BL,TL,TR,BR (swap U/V)
            new[] { c1, c0, c3, c2 }, // BR,BL,TL,TR (flip U)
            new[] { c3, c2, c1, c0 }, // TL,TR,BR,BL (flip V)
            new[] { c1, c2, c3, c0 }, // rotate 90
            new[] { c3, c0, c1, c2 }, // rotate -90
        };

        float bestInterior = -1f;
        Vector2 bestUv = default;
        bool any = false;

        for (int w = 0; w < windings.Length; w++)
        {
            var corners = windings[w];
            Vector3 wv0 = cam.WorldToViewportPoint(corners[0]);
            Vector3 wv1 = cam.WorldToViewportPoint(corners[1]);
            Vector3 wv2 = cam.WorldToViewportPoint(corners[2]);
            Vector3 wv3 = cam.WorldToViewportPoint(corners[3]);
            if (wv0.z <= 0f || wv1.z <= 0f || wv2.z <= 0f || wv3.z <= 0f)
                continue;

            Vector2 a = new Vector2(wv0.x, wv0.y);
            Vector2 b = new Vector2(wv1.x, wv1.y);
            Vector2 c = new Vector2(wv2.x, wv2.y);
            Vector2 d = new Vector2(wv3.x, wv3.y);

            if (!TryInverseBilinear2D(sample, a, b, c, d, out Vector2 cand))
                continue;

            cand.x = Mathf.Clamp01(cand.x);
            cand.y = Mathf.Clamp01(cand.y);
            // Distance from nearest UV edge — prefer interior solutions over V≈0/U≈0.
            float interior = Mathf.Min(cand.x, 1f - cand.x, cand.y, 1f - cand.y);
            if (!any || interior > bestInterior + 1e-4f)
            {
                any = true;
                bestInterior = interior;
                bestUv = cand;
            }
        }

        if (!any)
            return false;

        uv = bestUv;
        return true;
    }

    /// <summary>
    /// Four world corners of the monitor face whose normal best faces the camera.
    /// Order: u0v0, u1v0, u1v1, u0v1 (BL, BR, TR, TL in local UV sense).
    /// </summary>
    private static bool TryBuildMonitorFaceWorldCorners(
        MeshRenderer screenMesh,
        Vector3 cameraWorldPos,
        out Vector3 c0,
        out Vector3 c1,
        out Vector3 c2,
        out Vector3 c3)
    {
        c0 = c1 = c2 = c3 = default;
        Transform t = screenMesh.transform;

        Bounds lb;
        var filter = screenMesh.GetComponent<MeshFilter>();
        if (filter != null && filter.sharedMesh != null)
            lb = filter.sharedMesh.bounds;
        else
        {
            // Renderer world bounds → approximate local box.
            Bounds wb = screenMesh.bounds;
            Vector3 localCenter = t.InverseTransformPoint(wb.center);
            Vector3 localSize = t.InverseTransformVector(wb.size);
            localSize = new Vector3(Mathf.Abs(localSize.x), Mathf.Abs(localSize.y), Mathf.Abs(localSize.z));
            lb = new Bounds(localCenter, localSize);
        }

        Vector3 size = lb.size;
        if (size.sqrMagnitude < 1e-10f)
            return false;

        // Thinnest local axis = screen normal.
        int nAxis;
        int uAxis;
        int vAxis;
        if (size.x <= size.y && size.x <= size.z)
        {
            nAxis = 0; uAxis = 2; vAxis = 1; // yz face
        }
        else if (size.y <= size.x && size.y <= size.z)
        {
            nAxis = 1; uAxis = 0; vAxis = 2; // xz face
        }
        else
        {
            nAxis = 2; uAxis = 0; vAxis = 1; // xy face
        }

        Vector3 localN = Vector3.zero;
        localN[nAxis] = 1f;
        Vector3 worldN = t.TransformDirection(localN).normalized;
        Vector3 center = t.TransformPoint(lb.center);
        Vector3 toCam = cameraWorldPos - center;
        float sign = Vector3.Dot(worldN, toCam) >= 0f ? 1f : -1f;

        float nFace = lb.center[nAxis] + sign * lb.extents[nAxis];
        float uMin = lb.min[uAxis];
        float uMax = lb.max[uAxis];
        float vMin = lb.min[vAxis];
        float vMax = lb.max[vAxis];

        Vector3 L(float u, float v)
        {
            Vector3 p = lb.center;
            p[nAxis] = nFace;
            p[uAxis] = u;
            p[vAxis] = v;
            return t.TransformPoint(p);
        }

        // BL, BR, TR, TL
        c0 = L(uMin, vMin);
        c1 = L(uMax, vMin);
        c2 = L(uMax, vMax);
        c3 = L(uMin, vMax);
        return true;
    }

    /// <summary>
    /// Inverse bilinear map of point p inside viewport quad a,b,c,d (BL,BR,TR,TL) → UV in ~[0,1].
    /// iq-style; falls back to barycentric on two triangles.
    /// </summary>
    private static bool TryInverseBilinear2D(
        Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 uv)
    {
        uv = default;

        // https://iquilezles.org/articles/ibilinear/ — a=BL, b=BR, c=TR, d=TL
        Vector2 e = b - a;
        Vector2 f = d - a;
        Vector2 g = a - b + c - d;
        Vector2 h = p - a;

        float k2 = Cross2(g, f);
        float k1 = Cross2(e, f) + Cross2(h, g);
        float k0 = Cross2(h, e);

        if (Mathf.Abs(k2) < 1e-8f)
        {
            // Degenerate to linear (parallelogram / trapezoid edge case).
            if (Mathf.Abs(k1) < 1e-8f)
                return TryBarycentricQuad2D(p, a, b, c, d, out uv);
            float v = -k0 / k1;
            float uDenomX = e.x + g.x * v;
            float uDenomY = e.y + g.y * v;
            float u = Mathf.Abs(uDenomX) > Mathf.Abs(uDenomY)
                ? (h.x - f.x * v) / uDenomX
                : (Mathf.Abs(uDenomY) > 1e-8f ? (h.y - f.y * v) / uDenomY : 0f);
            if (u >= -0.05f && u <= 1.05f && v >= -0.05f && v <= 1.05f)
            {
                uv = new Vector2(u, v);
                return true;
            }
            return TryBarycentricQuad2D(p, a, b, c, d, out uv);
        }

        float disc = k1 * k1 - 4f * k0 * k2;
        if (disc < 0f)
            return TryBarycentricQuad2D(p, a, b, c, d, out uv);

        float sqrtD = Mathf.Sqrt(disc);
        float v0 = (-k1 - sqrtD) / (2f * k2);
        float u0;
        {
            float uDenomX = e.x + g.x * v0;
            float uDenomY = e.y + g.y * v0;
            u0 = Mathf.Abs(uDenomX) > Mathf.Abs(uDenomY)
                ? (h.x - f.x * v0) / (Mathf.Abs(uDenomX) > 1e-8f ? uDenomX : 1e-8f)
                : (h.y - f.y * v0) / (Mathf.Abs(uDenomY) > 1e-8f ? uDenomY : 1e-8f);
        }

        if (u0 >= -0.05f && u0 <= 1.05f && v0 >= -0.05f && v0 <= 1.05f)
        {
            uv = new Vector2(u0, v0);
            return true;
        }

        float v1 = (-k1 + sqrtD) / (2f * k2);
        float u1;
        {
            float uDenomX = e.x + g.x * v1;
            float uDenomY = e.y + g.y * v1;
            u1 = Mathf.Abs(uDenomX) > Mathf.Abs(uDenomY)
                ? (h.x - f.x * v1) / (Mathf.Abs(uDenomX) > 1e-8f ? uDenomX : 1e-8f)
                : (h.y - f.y * v1) / (Mathf.Abs(uDenomY) > 1e-8f ? uDenomY : 1e-8f);
        }

        if (u1 >= -0.05f && u1 <= 1.05f && v1 >= -0.05f && v1 <= 1.05f)
        {
            uv = new Vector2(u1, v1);
            return true;
        }

        return TryBarycentricQuad2D(p, a, b, c, d, out uv);
    }

    private static float Cross2(Vector2 u, Vector2 v) => u.x * v.y - u.y * v.x;

    private static bool TryBarycentricQuad2D(
        Vector2 p, Vector2 a, Vector2 b, Vector2 c, Vector2 d, out Vector2 uv)
    {
        uv = default;
        const float eps = -0.02f;
        // Triangle a-b-c → UV corners (0,0),(1,0),(1,1)
        if (TryBarycentricWeights2D(p, a, b, c, out float w0, out float w1, out float w2)
            && w0 >= eps && w1 >= eps && w2 >= eps)
        {
            uv = new Vector2(w1 + w2, w2);
            return true;
        }
        // Triangle a-c-d → UV corners (0,0),(1,1),(0,1)
        if (TryBarycentricWeights2D(p, a, c, d, out w0, out w1, out w2)
            && w0 >= eps && w1 >= eps && w2 >= eps)
        {
            uv = new Vector2(w1, w1 + w2);
            return true;
        }
        // Alternate diagonal: a-b-d and b-c-d
        if (TryBarycentricWeights2D(p, a, b, d, out w0, out w1, out w2)
            && w0 >= eps && w1 >= eps && w2 >= eps)
        {
            uv = new Vector2(w1, w2);
            return true;
        }
        if (TryBarycentricWeights2D(p, b, c, d, out w0, out w1, out w2)
            && w0 >= eps && w1 >= eps && w2 >= eps)
        {
            // b=(1,0), c=(1,1), d=(0,1)
            uv = new Vector2(w0 + w1, w1 + w2);
            return true;
        }
        return false;
    }

    private static bool TryBarycentricWeights2D(
        Vector2 p, Vector2 a, Vector2 b, Vector2 c,
        out float w0, out float w1, out float w2)
    {
        Vector2 v0 = b - a;
        Vector2 v1 = c - a;
        Vector2 v2 = p - a;
        float d00 = Vector2.Dot(v0, v0);
        float d01 = Vector2.Dot(v0, v1);
        float d11 = Vector2.Dot(v1, v1);
        float d20 = Vector2.Dot(v2, v0);
        float d21 = Vector2.Dot(v2, v1);
        float denom = d00 * d11 - d01 * d01;
        if (Mathf.Abs(denom) < 1e-12f)
        {
            w0 = w1 = w2 = 0f;
            return false;
        }
        w1 = (d11 * d20 - d01 * d21) / denom;
        w2 = (d00 * d21 - d01 * d20) / denom;
        w0 = 1f - w1 - w2;
        return true;
    }

    private static void AddBoundsOrientation(
        List<(Vector2 uv, string tag)> into,
        Bounds lb,
        Vector3 local,
        int axisU,
        int axisV,
        string orientName)
    {
        float u = Mathf.InverseLerp(lb.min[axisU], lb.max[axisU], local[axisU]);
        float v = Mathf.InverseLerp(lb.min[axisV], lb.max[axisV], local[axisV]);
        u = Mathf.Clamp01(u);
        v = Mathf.Clamp01(v);
        AddUvVariants(into, new Vector2(u, v), orientName + "|bounds");
    }

    private static void AddUvVariants(List<(Vector2 uv, string tag)> into, Vector2 raw, string prefix)
    {
        void Add(Vector2 uv, string tag)
        {
            for (int i = 0; i < into.Count; i++)
            {
                if (Vector2.Distance(into[i].uv, uv) < 0.0005f)
                    return;
            }
            into.Add((uv, tag));
        }

        Add(raw, prefix + "|uRaw|vRaw");
        Add(new Vector2(raw.x, 1f - raw.y), prefix + "|uRaw|vFlip");
        Add(new Vector2(1f - raw.x, raw.y), prefix + "|uFlip|vRaw");
        Add(new Vector2(1f - raw.x, 1f - raw.y), prefix + "|uFlip|vFlip");
    }

    /// <summary>
    /// 1.0.10: Match markers by projecting each mapRadarObject through the monitor glass into
    /// gameplay viewport space, then comparing to the look sample. Bypasses broken ss AABB
    /// when inverse-bilinear UV collapses to V≈0/U≈0.
    /// how=gvp — high confidence for both peek and activate.
    /// </summary>
    private static bool TryMatchMarkersByGameplayViewport(
        PlayerControllerB player,
        Camera markerCam,
        Vector3 hitPoint,
        MeshRenderer screenMesh,
        out TerminalAccessibleObject matched,
        out float bestScore,
        out Vector2 bestUv,
        out string bestUvTag,
        out string matchHow)
    {
        matched = null!;
        bestScore = float.MaxValue;
        bestUv = default;
        bestUvTag = "none";
        matchHow = "none";

        if (player == null || markerCam == null || screenMesh == null)
            return false;

        var gameplayCam = player.gameplayCamera != null ? player.gameplayCamera : Camera.main;
        if (gameplayCam == null)
            return false;

        if (!TryBuildMonitorFaceWorldCorners(
                screenMesh, gameplayCam.transform.position,
                out Vector3 c0, out Vector3 c1, out Vector3 c2, out Vector3 c3))
            return false;

        // Look sample in gameplay viewport (crosshair / hit on glass).
        Vector3 hitVp3 = gameplayCam.WorldToViewportPoint(hitPoint);
        Vector2 lookSample;
        if (hitVp3.z > 0f && !float.IsNaN(hitVp3.x) && !float.IsNaN(hitVp3.y))
            lookSample = new Vector2(hitVp3.x, hitVp3.y);
        else
            lookSample = new Vector2(0.5f, 0.5f);

        // ~0.04–0.06 viewport threshold (tight-ish, matches rayViewportMax band).
        const float gvpMaxDist = 0.05f;
        bool preferFlipV = Plugin.FlipUvV == null || Plugin.FlipUvV.Value;

        TerminalAccessibleObject? best = null;
        float bestDist = float.MaxValue;
        Vector2 bestMapUv = default;
        string bestFlip = "none";

        var objs = UnityEngine.Object.FindObjectsOfType<TerminalAccessibleObject>();
        for (int i = 0; i < objs.Length; i++)
        {
            var tao = objs[i];
            if (tao == null || tao.mapRadarObject == null || !tao.mapRadarObject.activeInHierarchy)
                continue;

            Vector3 markerWorld = tao.mapRadarObject.transform.position;
            var rt = tao.mapRadarObject.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.GetWorldCorners(CornerBuf);
                markerWorld = (CornerBuf[0] + CornerBuf[1] + CornerBuf[2] + CornerBuf[3]) * 0.25f;
            }

            Vector3 mvp = markerCam.WorldToViewportPoint(markerWorld);
            if (mvp.z <= 0f)
                continue;

            // Flip variants of map-RT UV.
            Vector2[] uvs =
            {
                new Vector2(mvp.x, mvp.y),
                new Vector2(mvp.x, 1f - mvp.y),
                new Vector2(1f - mvp.x, mvp.y),
                new Vector2(1f - mvp.x, 1f - mvp.y),
            };
            string[] flipTags = { "uRaw|vRaw", "uRaw|vFlip", "uFlip|vRaw", "uFlip|vFlip" };

            for (int f = 0; f < uvs.Length; f++)
            {
                Vector2 mapUv = uvs[f];
                // Bilinear map (u,v) onto monitor world quad (BL,BR,TR,TL).
                Vector3 onGlass = BilinearQuadPoint(c0, c1, c2, c3, mapUv.x, mapUv.y);
                Vector3 glassVp3 = gameplayCam.WorldToViewportPoint(onGlass);
                if (glassVp3.z <= 0f)
                    continue;

                Vector2 glassVp = new Vector2(glassVp3.x, glassVp3.y);
                float dist = Vector2.Distance(lookSample, glassVp);
                if (dist > gvpMaxDist)
                    continue;

                // Mild FlipUvV preference among near-ties.
                float adj = dist;
                bool hasFlipV = flipTags[f].IndexOf("vFlip", StringComparison.Ordinal) >= 0;
                if (preferFlipV && hasFlipV) adj -= 0.0005f;
                else if (!preferFlipV && !hasFlipV) adj -= 0.0005f;

                if (adj < bestDist)
                {
                    bestDist = adj;
                    best = tao;
                    bestMapUv = mapUv;
                    bestFlip = flipTags[f];
                }
            }
        }

        if (best == null)
            return false;

        matched = best;
        bestScore = bestDist;
        bestUv = bestMapUv;
        bestUvTag = "gvp|" + bestFlip;
        matchHow = "gvp";
        return true;
    }

    /// <summary>Bilinear interpolate on world quad BL,BR,TR,TL at (u,v) in [0,1].</summary>
    private static Vector3 BilinearQuadPoint(Vector3 bl, Vector3 br, Vector3 tr, Vector3 tl, float u, float v)
    {
        Vector3 bottom = Vector3.Lerp(bl, br, u);
        Vector3 top = Vector3.Lerp(tl, tr, u);
        return Vector3.Lerp(bottom, top, v);
    }

    /// <summary>
    /// Score markers at a viewport UV: (a) actual RectTransform AABB + small pad,
    /// (b) nearest-center for hover peek only, (c) tight ray-to-marker (not high-conf for activate).
    /// Rank tiers: AABB=0, center=1, ray=2. No minHalf inflate (1.0.7).
    /// </summary>
    private static bool TryScoreMarkersAtViewport(
        Camera mapCamera,
        Vector2 clickUv,
        out TerminalAccessibleObject matched,
        out float bestScore,
        out string matchHow,
        out float rank)
    {
        matched = null!;
        bestScore = float.MaxValue;
        matchHow = "none";
        rank = float.MaxValue;

        float pad = Plugin.HitRadius != null ? Plugin.HitRadius.Value : 0.012f;
        // Hover-only nearest-center (ignored by IsHighConfidenceMatch for activate).
        float nearestCenterMax = 0.045f;
        const float rayWorldMax = 4f;
        // 1.0.7: tightened from 0.1 — loose ray must not feel like a huge hitbox.
        const float rayViewportMax = 0.04f;

        TerminalAccessibleObject? aabbMatch = null;
        float aabbScore = float.MaxValue;
        TerminalAccessibleObject? centerMatch = null;
        float centerDist = float.MaxValue;
        TerminalAccessibleObject? rayMatch = null;
        float rayDist = float.MaxValue;
        float rayVpDist = float.MaxValue;

        Ray markerRay = mapCamera.ViewportPointToRay(new Vector3(clickUv.x, clickUv.y, 0f));

        var objs = UnityEngine.Object.FindObjectsOfType<TerminalAccessibleObject>();
        for (int i = 0; i < objs.Length; i++)
        {
            var tao = objs[i];
            if (tao == null || tao.mapRadarObject == null || !tao.mapRadarObject.activeInHierarchy)
                continue;

            var rt = tao.mapRadarObject.GetComponent<RectTransform>();
            Vector3 markerWorld = tao.mapRadarObject.transform.position;
            if (rt != null)
            {
                rt.GetWorldCorners(CornerBuf);
                markerWorld = (CornerBuf[0] + CornerBuf[1] + CornerBuf[2] + CornerBuf[3]) * 0.25f;
            }

            // (b) Ray-to-marker — require tight viewport proximity (no loose world-only hits).
            float worldAlong = Vector3.Dot(markerWorld - markerRay.origin, markerRay.direction.normalized);
            Vector3 closestOnRay = worldAlong > 0f
                ? markerRay.origin + markerRay.direction.normalized * worldAlong
                : markerRay.origin;
            float wd = Vector3.Distance(markerWorld, closestOnRay);

            Vector3 centerVp3 = mapCamera.WorldToViewportPoint(markerWorld);
            float vpDist = centerVp3.z > 0f
                ? Vector2.Distance(clickUv, new Vector2(centerVp3.x, centerVp3.y))
                : float.MaxValue;

            if (vpDist <= rayViewportMax && wd <= rayWorldMax && wd < rayDist)
            {
                rayDist = wd;
                rayVpDist = vpDist;
                rayMatch = tao;
            }

            if (rt == null)
                continue;

            float minX = float.PositiveInfinity;
            float minY = float.PositiveInfinity;
            float maxX = float.NegativeInfinity;
            float maxY = float.NegativeInfinity;
            bool anyInFront = false;

            for (int c = 0; c < 4; c++)
            {
                Vector3 vp = mapCamera.WorldToViewportPoint(CornerBuf[c]);
                if (vp.z > 0f)
                    anyInFront = true;
                if (vp.x < minX) minX = vp.x;
                if (vp.x > maxX) maxX = vp.x;
                if (vp.y < minY) minY = vp.y;
                if (vp.y > maxY) maxY = vp.y;
            }

            if (!anyInFront)
                continue;

            Vector3 centerVp = mapCamera.WorldToViewportPoint(rt.position);
            if (centerVp.z > 0f)
            {
                float cd = Vector2.Distance(clickUv, new Vector2(centerVp.x, centerVp.y));
                if (cd < centerDist && cd <= nearestCenterMax)
                {
                    centerDist = cd;
                    centerMatch = tao;
                }
            }

            float w = maxX - minX;
            float h = maxY - minY;
            if (w < 0.008f || h < 0.008f)
            {
                // Tiny markers: modest radius only — no 0.028+ minHalf floor.
                if (centerVp.z <= 0f)
                    continue;
                float r = Mathf.Max(0.01f, pad);
                minX = centerVp.x - r;
                maxX = centerVp.x + r;
                minY = centerVp.y - r;
                maxY = centerVp.y + r;
            }
            else
            {
                // Actual RectTransform viewport AABB + small pad only (do NOT expand to minHalf).
                minX -= pad;
                maxX += pad;
                minY -= pad;
                maxY += pad;
            }

            if (clickUv.x < minX || clickUv.x > maxX || clickUv.y < minY || clickUv.y > maxY)
                continue;

            float scx = (minX + maxX) * 0.5f;
            float scy = (minY + maxY) * 0.5f;
            float score = Vector2.Distance(clickUv, new Vector2(scx, scy));
            if (score < aabbScore)
            {
                aabbScore = score;
                aabbMatch = tao;
            }
        }

        // Tiered pick: AABB > nearest-center (hover) > tight ray. Activate uses high-conf only.
        if (aabbMatch != null)
        {
            matched = aabbMatch;
            bestScore = aabbScore;
            matchHow = "aabb";
            rank = 0f * 10f + aabbScore;
            return true;
        }

        if (centerMatch != null)
        {
            matched = centerMatch;
            bestScore = centerDist;
            matchHow = "center";
            rank = 1f * 10f + centerDist;
            Plugin.V($"Marker match via nearest-center dist={centerDist:F4} (max={nearestCenterMax:F3}).");
            return true;
        }

        if (rayMatch != null)
        {
            matched = rayMatch;
            bestScore = rayDist;
            matchHow = "ray";
            rank = 2f * 10f + (rayDist / rayWorldMax);
            Plugin.V($"Marker match via ray-to-marker worldDist={rayDist:F3}m vpDist={rayVpDist:F4}.");
            return true;
        }

        return false;
    }

    /// <summary>
    /// Host/local: Terminal.CallFunctionInAccessibleTerminalObject (all TAOs with code) + broadcast.
    /// Client: request host; local SFX only. Returns true when CallFunction ran (host/local) or request sent (client).
    /// </summary>
    private static bool ActivateCode(
        TerminalAccessibleObject matched,
        float score,
        string hitDiag,
        Vector2 bestUv,
        string uvTag,
        int markers,
        string matchHow)
    {
        string code = matched.objectCode ?? "?";
        ulong netId = 0UL;
        try
        {
            if (matched.NetworkObject != null)
                netId = matched.NetworkObject.NetworkObjectId;
        }
        catch
        {
            /* NetworkObject may be unset on some objects */
        }

        var nm = NetworkManager.Singleton;
        bool isClientOnly = nm != null && nm.IsConnectedClient && !nm.IsServer && !nm.IsHost;

        if (isClientOnly)
        {
            Plugin.Log.LogInfo(
                $"Client map click → host CallFunction '{code}' netId={netId} score={score:F4} via={matchHow} uvTag={uvTag} {hitDiag}");
            HostModGate.SendActivateRequest(netId, code);
            // Local feedback only; host runs CallFunctionInAccessibleTerminalObject.
            TerminalCodeActivation.TryPlayBroadcastEffect();
            try
            {
                if (HUDManager.Instance != null)
                    HUDManager.Instance.DisplayTip("Map code", $"Requested {code}");
            }
            catch (Exception ex)
            {
                Plugin.V($"Activate tip skipped: {ex.Message}");
            }
            return true;
        }

        bool ran = TerminalCodeActivation.Activate(matched, source: "map-click", score: score);
        if (ran)
        {
            LogClickAttempt(hitDiag, bestUv, uvTag, markers,
                $"activated '{code}' score={score:F4} via={matchHow} vp=({bestUv.x:F3},{bestUv.y:F3})");
        }
        return ran;
    }

    /// <summary>
    /// Last-resort under forceMapContext: among active mapRadarObjects, pick the marker whose
    /// viewport center is closest to a click UV candidate, only if distance ≤ 0.05.
    /// Precise AABB / center / ray matching remains primary.
    /// </summary>
    private static bool TryPickCenterFallback(
        Camera mapCamera,
        out TerminalAccessibleObject matched,
        out float bestScore,
        out Vector2 bestUv,
        out string bestUvTag,
        out string matchHow)
    {
        matched = null!;
        bestScore = float.MaxValue;
        bestUv = default;
        bestUvTag = "none";
        matchHow = "none";

        if (UvCandidates.Count == 0 || mapCamera == null)
            return false;

        float pad = Plugin.HitRadius != null ? Plugin.HitRadius.Value : 0.022f;
        // Tightened 1.0.5: 0.12 → 0.05, also require HitRadius-style proximity.
        float maxDist = Mathf.Min(0.05f, Mathf.Max(0.035f, pad * 2.25f));
        TerminalAccessibleObject? best = null;
        float bestDist = float.MaxValue;
        Vector2 bestUvLocal = default;
        string bestTag = "none";

        var objs = UnityEngine.Object.FindObjectsOfType<TerminalAccessibleObject>();
        for (int u = 0; u < UvCandidates.Count; u++)
        {
            var (clickUv, tag) = UvCandidates[u];
            // Skip noisy bounds-edge UVs in the already-loose fallback.
            if (tag.IndexOf("bounds", System.StringComparison.Ordinal) >= 0 && IsEdgeUv(clickUv))
                continue;

            for (int i = 0; i < objs.Length; i++)
            {
                var tao = objs[i];
                if (tao == null || tao.mapRadarObject == null || !tao.mapRadarObject.activeInHierarchy)
                    continue;

                Vector3 markerWorld = tao.mapRadarObject.transform.position;
                var rt = tao.mapRadarObject.GetComponent<RectTransform>();
                if (rt != null)
                {
                    rt.GetWorldCorners(CornerBuf);
                    markerWorld = (CornerBuf[0] + CornerBuf[1] + CornerBuf[2] + CornerBuf[3]) * 0.25f;
                }

                Vector3 vp = mapCamera.WorldToViewportPoint(markerWorld);
                if (vp.z <= 0f)
                    continue;

                float dist = Vector2.Distance(clickUv, new Vector2(vp.x, vp.y));
                if (dist > maxDist || dist >= bestDist)
                    continue;

                bestDist = dist;
                best = tao;
                bestUvLocal = clickUv;
                bestTag = tag + "|fb0.05";
            }
        }

        if (best == null)
            return false;

        matched = best;
        bestScore = bestDist;
        bestUv = bestUvLocal;
        bestUvTag = bestTag;
        matchHow = "centerFallback";
        Plugin.Log.LogInfo(
            $"MapClick: centerFallback dist={bestDist:F4} (max={maxDist:F2}) UV=({bestUv.x:F3},{bestUv.y:F3}) code='{best.objectCode}'");
        return true;
    }

    /// <summary>
    /// Soft-detect CrewMonitors map-feed panels. Matches ray hits whose material mainTexture is a
    /// RenderTexture named CrewMapFeedRT_* or whose parent chain contains "CrewMonitor".
    /// Uses the slot's MapFeedCamera via reflection when available; if the assembly is absent, skips.
    /// Fallback: UV from hit renderer bounds + StartOfRound.mapScreen.mapCamera.
    /// </summary>
    private static bool TryResolveCrewMonitorMapHit(
        Ray ray,
        float range,
        out MeshRenderer screenMesh,
        out Vector3 hitPoint,
        out Camera markerCam,
        out string diag)
    {
        screenMesh = null!;
        hitPoint = default;
        markerCam = null!;
        diag = "crew=skip";

        // Soft-dep: never throw into main mapScreen path if CrewMonitors assembly is missing.
        try
        {
            if (!EnsureCrewMonitorsReflection())
                return false;
        }
        catch (Exception ex)
        {
            Plugin.V($"CrewMonitors soft-dep skip: {ex.Message}");
            diag = "crew=skip";
            return false;
        }

        try
        {
        int hitCount = Physics.RaycastNonAlloc(ray, RayHitBuf, range, ~0, QueryTriggerInteraction.Ignore);
        // Sort by distance (same as main resolve).
        for (int i = 0; i < hitCount - 1; i++)
        {
            for (int j = i + 1; j < hitCount; j++)
            {
                if (RayHitBuf[j].distance < RayHitBuf[i].distance)
                {
                    var tmp = RayHitBuf[i];
                    RayHitBuf[i] = RayHitBuf[j];
                    RayHitBuf[j] = tmp;
                }
            }
        }

        for (int i = 0; i < hitCount; i++)
        {
            var hit = RayHitBuf[i];
            if (hit.collider == null)
                continue;

            if (!TryIdentifyCrewMapPanel(hit, out MeshRenderer panel, out RenderTexture? hitRt, out string how))
                continue;

            // Resolve MapFeedCamera for this panel via CrewMonitorManager slots.
            Camera? feedCam = null;
            string slotInfo = "slot=?";
            bool rtMatched = hitRt != null;
            if (TryFindCrewSlotCamera(panel, hitRt, out feedCam, out slotInfo) && feedCam != null)
            {
                screenMesh = panel;
                hitPoint = hit.point;
                markerCam = feedCam;
                diag = $"hits={hitCount} via=CrewMonitor#{i}({how}) {slotInfo} cam={feedCam.name}";
                return true;
            }

            // RT-named map feed but camera missing — fall back to main mapCamera (imperfect; feed cams follow players).
            // Name-only hits without MapFeedCamera are skipped (likely body-cam panels, not map feeds).
            if (rtMatched)
            {
                var map = StartOfRound.Instance != null ? StartOfRound.Instance.mapScreen : null;
                if (map != null && map.mapCamera != null)
                {
                    screenMesh = panel;
                    hitPoint = hit.point;
                    markerCam = map.mapCamera;
                    diag = $"hits={hitCount} via=CrewMonitor#{i}({how},mapCamFallback) {slotInfo}";
                    Plugin.Log.LogInfo(
                        $"MapClick: CrewMonitors RT panel without MapFeedCamera — using mapScreen.mapCamera. {diag}");
                    return true;
                }
            }
        }

        diag = hitCount > 0 ? $"crew=noPanel hits={hitCount}" : "crew=noHits";
        return false;
        }
        catch (Exception ex)
        {
            Plugin.V($"CrewMonitors hit resolve failed soft: {ex.Message}");
            screenMesh = null!;
            hitPoint = default;
            markerCam = null!;
            diag = "crew=skip";
            return false;
        }
    }

    private static bool TryIdentifyCrewMapPanel(
        RaycastHit hit,
        out MeshRenderer panel,
        out RenderTexture? hitRt,
        out string how)
    {
        panel = null!;
        hitRt = null;
        how = "none";

        var t = hit.collider.transform;

        // Parent name contains CrewMonitor (clones, focus triggers, etc.).
        bool nameHint = false;
        Transform? walk = t;
        while (walk != null)
        {
            if (walk.name.IndexOf("CrewMonitor", StringComparison.OrdinalIgnoreCase) >= 0)
            {
                nameHint = true;
                break;
            }
            walk = walk.parent;
        }

        // Material mainTexture is CrewMapFeedRT_*
        var renderers = hit.collider.GetComponentsInParent<MeshRenderer>(true);
        for (int r = 0; r < renderers.Length; r++)
        {
            var mr = renderers[r];
            if (mr == null)
                continue;

            RenderTexture? rt = null;
            try
            {
                var mats = mr.sharedMaterials;
                if (mats != null)
                {
                    for (int m = 0; m < mats.Length; m++)
                    {
                        var mat = mats[m];
                        if (mat == null)
                            continue;
                        var tex = mat.mainTexture as RenderTexture;
                        if (tex != null && tex.name != null &&
                            tex.name.StartsWith("CrewMapFeedRT_", StringComparison.Ordinal))
                        {
                            rt = tex;
                            break;
                        }
                    }
                }
            }
            catch
            {
                /* material access can throw on some render pipelines */
            }

            if (rt != null)
            {
                panel = mr;
                hitRt = rt;
                how = "rt:" + rt.name;
                return true;
            }

            if (nameHint)
            {
                panel = mr;
                hitRt = null;
                how = "name:" + (walk != null ? walk.name : mr.name);
                return true;
            }
        }

        return false;
    }

    private static bool EnsureCrewMonitorsReflection()
    {
        if (_crewReflectTried)
            return _crewReflectOk;

        try
        {
            _crewManagerType = Type.GetType("CrewMonitors.CrewMonitorManager, CrewMonitors");
            if (_crewManagerType == null)
            {
                foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
                {
                    if (!string.Equals(asm.GetName().Name, "CrewMonitors", StringComparison.OrdinalIgnoreCase))
                        continue;
                    _crewManagerType = asm.GetType("CrewMonitors.CrewMonitorManager");
                    break;
                }
            }

            if (_crewManagerType == null)
            {
                // Assembly may load later — do not lock failure; soft-skip this frame.
                Plugin.V("CrewMonitors assembly/type not found — map-panel clicks skipped this frame.");
                return false;
            }

            // Type found — from here, permanent success or permanent failure.
            _crewReflectTried = true;
            _crewReflectOk = false;

            const BindingFlags staticFlags = BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic;
            const BindingFlags instFlags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;

            _crewInstanceProp = _crewManagerType.GetProperty("Instance", staticFlags);
            _crewSlotsField = _crewManagerType.GetField("_slots", instFlags);
            if (_crewInstanceProp == null || _crewSlotsField == null)
            {
                Plugin.V("CrewMonitors Instance/_slots reflection failed.");
                return false;
            }

            // CrewSlot is a private nested type — discover field types from the list element.
            Type? slotType = null;
            var slotsFieldType = _crewSlotsField.FieldType;
            if (slotsFieldType.IsGenericType)
                slotType = slotsFieldType.GetGenericArguments()[0];
            if (slotType == null)
                slotType = _crewManagerType.GetNestedType("CrewSlot", BindingFlags.NonPublic);

            if (slotType == null)
            {
                Plugin.V("CrewMonitors.CrewSlot type not found.");
                return false;
            }

            _crewSlotMapFeedCam = slotType.GetField("MapFeedCamera", instFlags);
            _crewSlotMapFeedTex = slotType.GetField("MapFeedTexture", instFlags);
            _crewSlotHost = slotType.GetField("Host", instFlags);
            _crewSlotRenderer = slotType.GetField("Renderer", instFlags);
            _crewSlotShowMapFeed = slotType.GetField("ShowMapFeed", instFlags);

            if (_crewSlotMapFeedCam == null || _crewSlotHost == null)
            {
                Plugin.V("CrewMonitors.CrewSlot MapFeedCamera/Host fields missing.");
                return false;
            }

            _crewReflectOk = true;
            Plugin.V("CrewMonitors reflection ready for map-panel clicks.");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.V($"CrewMonitors reflection init failed: {ex.Message}");
            // If we already decided the type exists, lock the failure; otherwise allow retry.
            if (_crewManagerType != null)
            {
                _crewReflectTried = true;
                _crewReflectOk = false;
            }
            return false;
        }
    }

    private static bool TryFindCrewSlotCamera(
        MeshRenderer panel,
        RenderTexture? hitRt,
        out Camera? feedCam,
        out string slotInfo)
    {
        feedCam = null;
        slotInfo = "slot=?";

        try
        {
            if (!EnsureCrewMonitorsReflection() || _crewInstanceProp == null || _crewSlotsField == null)
                return false;

            var manager = _crewInstanceProp.GetValue(null);
            if (manager == null)
            {
                slotInfo = "slot=noInstance";
                return false;
            }

            if (_crewSlotsField.GetValue(manager) is not System.Collections.IList slots || slots.Count == 0)
            {
                slotInfo = "slot=empty";
                return false;
            }

            object? bestSlot = null;
            int bestIndex = -1;

            for (int i = 0; i < slots.Count; i++)
            {
                var slot = slots[i];
                if (slot == null)
                    continue;

                bool showMap = _crewSlotShowMapFeed != null && _crewSlotShowMapFeed.GetValue(slot) is bool b && b;
                var tex = _crewSlotMapFeedTex?.GetValue(slot) as RenderTexture;
                var host = _crewSlotHost?.GetValue(slot) as GameObject;
                var renderer = _crewSlotRenderer?.GetValue(slot) as MeshRenderer;

                bool match = false;
                if (hitRt != null && tex != null && ReferenceEquals(hitRt, tex))
                    match = true;
                else if (hitRt != null && tex != null &&
                         string.Equals(hitRt.name, tex.name, StringComparison.Ordinal))
                    match = true;
                else if (renderer != null && ReferenceEquals(renderer, panel))
                    match = true;
                else if (host != null &&
                         (panel.transform == host.transform || panel.transform.IsChildOf(host.transform) ||
                          host.transform.IsChildOf(panel.transform)))
                    match = true;

                if (!match)
                    continue;

                // Prefer slots currently showing map feed.
                if (bestSlot == null || showMap)
                {
                    bestSlot = slot;
                    bestIndex = i;
                    if (showMap && (hitRt == null || tex != null))
                        break;
                }
            }

            if (bestSlot == null)
            {
                slotInfo = "slot=unmatched";
                return false;
            }

            feedCam = _crewSlotMapFeedCam?.GetValue(bestSlot) as Camera;
            slotInfo = $"slot={bestIndex} cam={(feedCam != null ? feedCam.name : "null")}";
            return feedCam != null;
        }
        catch (Exception ex)
        {
            slotInfo = "slot=err:" + ex.Message;
            return false;
        }
    }

    /// <summary>
    /// Interact edge detection: IngamePlayerSettings actions, playerActions.Movement.Interact,
    /// then Keyboard.current.eKey as last-resort hangar fallback.
    /// </summary>
    private static bool WasInteractPressed(PlayerControllerB? player)
    {
        if (player == null)
            return false;

        // 1) IngamePlayerSettings.Instance.playerInput.actions["Interact"]
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
                if (interact == null)
                {
                    find = actions.GetType().GetMethod("FindAction", new[] { typeof(string) });
                    interact = find?.Invoke(actions, new object[] { "Interact" });
                }

                // Also try indexer actions["Interact"]
                if (interact == null)
                {
                    var indexer = actions.GetType().GetProperty("Item", new[] { typeof(string) });
                    interact = indexer?.GetValue(actions, new object[] { "Interact" });
                }

                var wasPressed = interact?.GetType().GetMethod("WasPressedThisFrame");
                if (wasPressed != null && wasPressed.Invoke(interact, null) is bool pressed && pressed)
                    return true;
            }
        }
        catch
        {
            /* ignore */
        }

        // 2) player.playerActions.Movement.Interact
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

        // 3) Last resort: Keyboard E — only meaningful while Tick already confirmed hangar.
        // Documented fallback when rebound Interact is unreachable via reflection.
        try
        {
            if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame)
                return true;
        }
        catch
        {
            /* Input System may be unavailable in some hosts */
        }

        return false;
    }
}
