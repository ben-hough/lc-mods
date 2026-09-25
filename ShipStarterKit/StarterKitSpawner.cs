using System;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using Object = UnityEngine.Object;

namespace ShipStarterKit;

/// <summary>
/// Host-only networked spawn of store equipment piles on the ship floor.
/// Mirrors vanilla <c>StartOfRound.LoadShipGrabbableItems</c> /
/// <c>ItemDropship.OpenShipDoorsOnServer</c>:
/// Instantiate(item.spawnPrefab) → setup GrabbableObject → NetworkObject.Spawn(false).
/// </summary>
internal static class StarterKitSpawner
{
    private static bool _sessionAttempted;

    public static void ResetSession()
    {
        _sessionAttempted = false;
    }

    public static void TrySupplyOnShipStart()
    {
        if (_sessionAttempted)
            return;
        _sessionAttempted = true;

        if (Plugin.Enabled == null || !Plugin.Enabled.Value)
        {
            Plugin.V("Skip supply: Enabled=false.");
            return;
        }

        var nm = NetworkManager.Singleton;
        if (nm == null || !(nm.IsServer || nm.IsHost))
        {
            Plugin.V("Skip supply: not host/server.");
            return;
        }

        if (!HostModGate.FeaturesActive)
        {
            Plugin.V("Skip supply: HostModGate inactive.");
            return;
        }

        var sor = StartOfRound.Instance;
        if (sor == null)
        {
            Plugin.Log.LogWarning("Skip supply: StartOfRound.Instance null.");
            return;
        }

        var saveName = Es3SaveGate.CurrentSaveName();
        if (string.IsNullOrEmpty(saveName))
        {
            Plugin.Log.LogWarning("Skip supply: currentSaveFileName unavailable.");
            return;
        }

        var once = Plugin.SpawnOncePerSave == null || Plugin.SpawnOncePerSave.Value;
        if (once && Es3SaveGate.AlreadySupplied(saveName!))
        {
            Plugin.Log.LogInfo($"ShipStarterKit: save '{saveName}' already supplied — skipping.");
            return;
        }

        var terminal = Object.FindObjectOfType<Terminal>();
        if (terminal == null || terminal.buyableItemsList == null || terminal.buyableItemsList.Length == 0)
        {
            Plugin.Log.LogWarning("Skip supply: Terminal.buyableItemsList unavailable.");
            return;
        }

        ItemCountConfig.EnsureBuyableEntries(terminal.buyableItemsList);

        var plan = BuildSpawnPlan(terminal.buyableItemsList);
        if (plan.Count == 0)
        {
            Plugin.Log.LogInfo("ShipStarterKit: all ItemCounts are 0 — nothing to spawn.");
            if (once)
                Es3SaveGate.MarkSupplied(saveName!);
            return;
        }

        var piles = BuildPileCenters(sor, plan.Count);
        var spawned = 0;

        for (var i = 0; i < plan.Count; i++)
        {
            var (item, count) = plan[i];
            var center = piles[i];
            Plugin.Log.LogInfo($"Spawning pile '{item.itemName}' x{count} at {center}");
            spawned += SpawnPile(sor, item, count, center);
        }

        if (once)
            Es3SaveGate.MarkSupplied(saveName!);

        Plugin.Log.LogInfo($"ShipStarterKit: spawned {spawned} store items across {plan.Count} piles on '{saveName}'.");
    }

    private static List<(Item item, int count)> BuildSpawnPlan(Item[] buyable)
    {
        var plan = new List<(Item, int)>();
        var claimed = new HashSet<Item>();

        foreach (var item in buyable)
        {
            if (item == null || item.spawnPrefab == null)
                continue;
            if (item.isScrap)
                continue;
            if (claimed.Contains(item))
                continue;

            var count = ItemCountConfig.GetCountForItem(item);
            if (count <= 0)
                continue;

            if (ItemCountConfig.TryGetKnownKey(item.itemName, out var key))
                Plugin.V($"Matched store item '{item.itemName}' → config '{key}' count={count}");
            else
                Plugin.V($"Matched store item '{item.itemName}' → dynamic count={count}");

            plan.Add((item, count));
            claimed.Add(item);
        }

        return plan;
    }

    /// <summary>
    /// Separate pile centers spread across the ship interior floor.
    /// Anchored to playerSpawnPositions[0] (or elevatorTransform), with a ring layout.
    /// </summary>
    private static Vector3[] BuildPileCenters(StartOfRound sor, int pileCount)
    {
        var centers = new Vector3[pileCount];
        Vector3 origin;
        if (sor.playerSpawnPositions != null && sor.playerSpawnPositions.Length > 0
            && sor.playerSpawnPositions[0] != null)
        {
            origin = sor.playerSpawnPositions[0].position;
        }
        else if (sor.elevatorTransform != null)
        {
            origin = sor.elevatorTransform.position;
        }
        else
        {
            origin = Vector3.zero;
        }

        // Slightly toward ship center / storage area relative to spawn.
        origin += new Vector3(0f, 0.15f, 1.2f);

        if (pileCount == 1)
        {
            centers[0] = origin;
            return centers;
        }

        // Two rows of piles along ship length (Z) and width (X).
        var cols = Mathf.Clamp(Mathf.CeilToInt(Mathf.Sqrt(pileCount)), 2, 4);
        var rows = Mathf.CeilToInt(pileCount / (float)cols);
        var spacingX = 1.15f;
        var spacingZ = 1.05f;
        var startX = -0.5f * (cols - 1) * spacingX;
        var startZ = -0.35f * (rows - 1) * spacingZ;

        for (var i = 0; i < pileCount; i++)
        {
            var col = i % cols;
            var row = i / cols;
            centers[i] = origin + new Vector3(startX + col * spacingX, 0f, startZ + row * spacingZ);
        }

        return centers;
    }

    private static int SpawnPile(StartOfRound sor, Item item, int count, Vector3 center)
    {
        var parent = sor.elevatorTransform != null ? sor.elevatorTransform : sor.propsContainer;
        var spawned = 0;
        var rng = new System.Random((item.itemName?.GetHashCode() ?? 0) ^ (count * 397) ^ ((int)(center.x * 100) * 31) ^ (int)(center.z * 100));

        for (var n = 0; n < count; n++)
        {
            try
            {
                var ox = (float)(rng.NextDouble() * 0.55 - 0.275);
                var oz = (float)(rng.NextDouble() * 0.55 - 0.275);
                var oy = 0.05f + (float)(rng.NextDouble() * 0.08);
                var pos = center + new Vector3(ox, oy, oz);

                // Clamp into shipBounds if available (same idea as LoadShipGrabbableItems).
                if (sor.shipBounds != null)
                {
                    var bounds = sor.shipBounds.bounds;
                    if (!bounds.Contains(pos))
                    {
                        pos = center;
                        pos.y = center.y + 0.1f;
                        Plugin.V($"Pile item '{item.itemName}' #{n} outside shipBounds; snapped to pile center.");
                    }
                }

                var go = Object.Instantiate(item.spawnPrefab, pos, Quaternion.identity, parent);
                var grab = go.GetComponent<GrabbableObject>();
                if (grab == null)
                {
                    Plugin.Log.LogWarning($"Prefab for '{item.itemName}' has no GrabbableObject — destroying.");
                    Object.Destroy(go);
                    continue;
                }

                grab.fallTime = 0f;
                grab.scrapPersistedThroughRounds = true;
                grab.isInElevator = true;
                grab.isInShipRoom = true;

                if (grab.radarIcon != null)
                    Object.Destroy(grab.radarIcon.gameObject);

                var net = grab.NetworkObject;
                if (net == null)
                {
                    Plugin.Log.LogWarning($"Prefab for '{item.itemName}' has no NetworkObject — destroying.");
                    Object.Destroy(go);
                    continue;
                }

                net.Spawn(false);
                spawned++;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogError($"Failed spawning '{item.itemName}' #{n}: {ex}");
            }
        }

        return spawned;
    }
}
