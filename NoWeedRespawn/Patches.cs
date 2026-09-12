using System.Collections.Generic;
using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace NoWeedRespawn;

internal static class OutdoorSession
{
    /// <summary>True after outdoor mold was fully cleared this moon; GenerateMold is blocked.</summary>
    internal static bool OutdoorCleared;

    /// <summary>True once any outdoor mold destroy happened this moon.</summary>
    internal static bool AnyDestroyedThisMoon;

    internal static readonly HashSet<int> DestroyedIndices = new();

    internal static void Reset()
    {
        OutdoorCleared = false;
        AnyDestroyedThisMoon = false;
        DestroyedIndices.Clear();
        Plugin.VLog("Outdoor session flags reset.");
    }
}

internal static class IndoorHelpers
{
    internal static bool IsServer(NetworkBehaviour nb)
    {
        if (nb != null && nb.IsServer)
            return true;
        var nm = NetworkManager.Singleton;
        return nm != null && nm.IsServer;
    }

    internal static void ForceEradicateEmptyTiles(CadaverGrowthAI growth)
    {
        if (!Plugin.IndoorActive || growth == null)
            return;
        if (!IsServer(growth))
            return;
        if (growth.GrowthTiles == null)
            return;

        for (var i = 0; i < growth.GrowthTiles.Count; i++)
            TryForceEradicateTile(growth.GrowthTiles[i], i);
    }

    internal static void TryForceEradicateTile(TileWithGrowth? tile, int tileIndex)
    {
        if (tile == null)
            return;
        if (tile.plantsInTile > 0)
            return;
        if (tile.plantPositions != null && tile.plantPositions.Count > 0)
            return;

        if (!tile.eradicated)
        {
            tile.eradicated = true;
            tile.eradicatedAtTime = Time.realtimeSinceStartup;
            Plugin.VLog($"Forced eradicated on empty tile={tileIndex}");
        }
        else if (tile.eradicatedAtTime <= 0f)
        {
            tile.eradicatedAtTime = Time.realtimeSinceStartup;
        }
    }
}

// ---------------------------------------------------------------------------
// Indoor cadaver / TileWithGrowth
// ---------------------------------------------------------------------------

/// <summary>
/// Vanilla lifts eradication via this RPC so growth can resume.
/// Skipping it keeps cleared tiles marked eradicated for the rest of the moon.
/// </summary>
[HarmonyPatch(typeof(CadaverGrowthAI), nameof(CadaverGrowthAI.SyncRemoveEradicationFromTileRpc))]
internal static class SyncRemoveEradicationPatch
{
    [HarmonyPrefix]
    private static bool Prefix(int tileIndex)
    {
        if (!Plugin.IndoorActive)
            return true;

        Plugin.VLog($"Blocked SyncRemoveEradicationFromTileRpc tile={tileIndex}");
        return false;
    }
}

/// <summary>
/// Block plant spawn syncs onto eradicated tiles.
/// </summary>
[HarmonyPatch(typeof(CadaverGrowthAI), nameof(CadaverGrowthAI.SyncSpawnPlantRpc))]
internal static class SyncSpawnPlantPatch
{
    [HarmonyPrefix]
    private static bool Prefix(CadaverGrowthAI __instance, int tileIndex)
    {
        if (!Plugin.IndoorActive)
            return true;

        if (__instance?.GrowthTiles == null)
            return true;
        if (tileIndex < 0 || tileIndex >= __instance.GrowthTiles.Count)
            return true;

        var tile = __instance.GrowthTiles[tileIndex];
        if (tile != null && tile.eradicated)
        {
            Plugin.VLog($"Blocked SyncSpawnPlantRpc on eradicated tile={tileIndex}");
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(CadaverGrowthAI), nameof(CadaverGrowthAI.DestroyPlantAtPosition))]
internal static class DestroyPlantAtPositionPatch
{
    [HarmonyPostfix]
    private static void Postfix(CadaverGrowthAI __instance)
    {
        IndoorHelpers.ForceEradicateEmptyTiles(__instance);
    }
}

[HarmonyPatch(typeof(CadaverGrowthAI), nameof(CadaverGrowthAI.RemoveWeedFromTile))]
internal static class RemoveWeedFromTilePatch
{
    [HarmonyPostfix]
    private static void Postfix(CadaverGrowthAI __instance, int tileIndex)
    {
        if (!Plugin.IndoorActive || __instance == null)
            return;
        if (!IndoorHelpers.IsServer(__instance))
            return;
        if (__instance.GrowthTiles == null || tileIndex < 0 || tileIndex >= __instance.GrowthTiles.Count)
            return;

        IndoorHelpers.TryForceEradicateTile(__instance.GrowthTiles[tileIndex], tileIndex);
    }
}

// ---------------------------------------------------------------------------
// Outdoor mold / vain shrouds
// ---------------------------------------------------------------------------

[HarmonyPatch(typeof(MoldSpreadManager), nameof(MoldSpreadManager.AddToDestroyedMoldList))]
internal static class AddToDestroyedMoldListPatch
{
    [HarmonyPostfix]
    private static void Postfix(MoldSpreadManager __instance, int index)
    {
        if (!Plugin.OutdoorActive || __instance == null)
            return;

        OutdoorSession.AnyDestroyedThisMoon = true;
        OutdoorSession.DestroyedIndices.Add(index);
        OutdoorHelpers.MaybeMarkCleared(__instance);
        Plugin.VLog($"AddToDestroyedMoldList index={index} cleared={OutdoorSession.OutdoorCleared}");
    }
}

[HarmonyPatch(typeof(MoldSpreadManager), nameof(MoldSpreadManager.DestroyMoldAtIndex))]
internal static class DestroyMoldAtIndexPatch
{
    [HarmonyPostfix]
    private static void Postfix(MoldSpreadManager __instance, bool __result)
    {
        if (!Plugin.OutdoorActive || __instance == null || !__result)
            return;

        OutdoorSession.AnyDestroyedThisMoon = true;
        OutdoorHelpers.MaybeMarkCleared(__instance);
    }
}

[HarmonyPatch(typeof(MoldSpreadManager), nameof(MoldSpreadManager.DestroyMoldAtPosition))]
internal static class DestroyMoldAtPositionPatch
{
    [HarmonyPostfix]
    private static void Postfix(MoldSpreadManager __instance)
    {
        if (!Plugin.OutdoorActive || __instance == null)
            return;

        OutdoorSession.AnyDestroyedThisMoon = true;
        OutdoorHelpers.MaybeMarkCleared(__instance);
    }
}

/// <summary>
/// Refuse further outdoor mold generation once the moon's weeds have been cleared.
/// Initial generation (before any destroy) still runs.
/// </summary>
[HarmonyPatch(typeof(MoldSpreadManager), nameof(MoldSpreadManager.GenerateMold))]
internal static class GenerateMoldPatch
{
    [HarmonyPrefix]
    private static bool Prefix(MoldSpreadManager __instance)
    {
        if (!Plugin.OutdoorActive)
            return true;

        if (OutdoorSession.OutdoorCleared)
        {
            Plugin.VLog("Skipped GenerateMold (OutdoorCleared).");
            return false;
        }

        if (!OutdoorSession.AnyDestroyedThisMoon)
            return true;

        OutdoorHelpers.MaybeMarkCleared(__instance);
        if (OutdoorSession.OutdoorCleared)
        {
            Plugin.VLog("Skipped GenerateMold (no living weeds after destroy).");
            return false;
        }

        return true;
    }
}

[HarmonyPatch(typeof(MoldSpreadManager), nameof(MoldSpreadManager.ResetMoldData))]
internal static class ResetMoldDataPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        OutdoorSession.Reset();
    }
}

[HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ResetMoldStates))]
internal static class ResetMoldStatesPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        OutdoorSession.Reset();
    }
}

[HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.StartGame))]
internal static class StartGamePatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        OutdoorSession.Reset();
    }
}

internal static class OutdoorHelpers
{
    internal static void MaybeMarkCleared(MoldSpreadManager msm)
    {
        if (msm == null || OutdoorSession.OutdoorCleared)
            return;
        if (!OutdoorSession.AnyDestroyedThisMoon)
            return;

        var noGenerated = msm.generatedMold == null || msm.generatedMold.Count == 0;
        var noWeeds = false;
        try
        {
            noWeeds = !msm.GetWeeds();
        }
        catch
        {
            // GetWeeds may not be safe mid-destroy; fall back to generatedMold count.
        }

        if (noGenerated || noWeeds)
        {
            OutdoorSession.OutdoorCleared = true;
            Plugin.Log.LogInfo("Outdoor mold fully cleared for this moon; further GenerateMold blocked.");
        }
    }
}
