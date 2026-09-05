using System;
using System.Linq;
using HarmonyLib;

namespace NoMineshaft.Patches;

/// <summary>
/// Strip Mineshaft from the current moon's dungeon flow weights before floor generation.
/// Host-side; clients follow host generation.
/// </summary>
[HarmonyPatch(typeof(RoundManager), nameof(RoundManager.GenerateNewFloor))]
internal static class GenerateNewFloorPatch
{
    private static void Prefix(RoundManager __instance)
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return;

        try
        {
            var level = __instance.currentLevel;
            if (level?.dungeonFlowTypes == null || level.dungeonFlowTypes.Length == 0)
                return;

            var mineshaftId = MineshaftId.Resolve(__instance);
            var filtered = level.dungeonFlowTypes
                .Where(flow => flow.id != mineshaftId)
                .ToArray();

            if (filtered.Length == level.dungeonFlowTypes.Length)
                return;

            if (filtered.Length == 0)
            {
                Plugin.Log.LogWarning(
                    $"Skipping Mineshaft removal on {level.name}: it would leave no interiors.");
                return;
            }

            level.dungeonFlowTypes = filtered;
            Plugin.Log.LogDebug(
                $"Removed Mineshaft (id {mineshaftId}) from dungeon flows on {level.name}.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to remove Mineshaft: {ex.Message}");
        }
    }
}

/// <summary>
/// If the rolled map seed still predicts Mineshaft (e.g. race with other mods),
/// reroll until a non-Mineshaft interior is selected.
/// </summary>
[HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ChooseNewRandomMapSeed))]
internal static class ChooseNewRandomMapSeedPatch
{
    private const int MaxAttempts = 500;
    private const int MaxSeed = 100_000_000;
    private static readonly Random Rng = new();

    private static void Postfix(StartOfRound __instance)
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return;

        try
        {
            var manager = RoundManager.Instance;
            if (manager?.currentLevel?.dungeonFlowTypes == null)
                return;

            var flows = manager.currentLevel.dungeonFlowTypes;
            if (flows.Length == 0)
                return;

            var mineshaftId = MineshaftId.Resolve(manager);

            var withoutMineshaft = flows
                .Where(flow => flow.id != mineshaftId)
                .ToArray();
            if (withoutMineshaft.Length > 0 && withoutMineshaft.Length != flows.Length)
            {
                manager.currentLevel.dungeonFlowTypes = withoutMineshaft;
                flows = withoutMineshaft;
            }

            if (flows.All(flow => flow.id != mineshaftId))
                return;

            if (PredictInteriorId(__instance.randomMapSeed, manager) != mineshaftId)
                return;

            manager.hasInitializedLevelRandomSeed = false;
            manager.InitializeRandomNumberGenerators();

            for (var i = 0; i < MaxAttempts; i++)
            {
                var candidate = Rng.Next(1, MaxSeed);
                var predicted = PredictInteriorId(candidate, manager);
                if (predicted is null || predicted == mineshaftId)
                    continue;

                __instance.randomMapSeed = candidate;
                Plugin.Log.LogInfo(
                    $"Rerolled map seed to {candidate} to avoid Mineshaft (attempt {i + 1}).");
                return;
            }

            Plugin.Log.LogWarning($"Could not find a non-Mineshaft seed after {MaxAttempts} attempts.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Seed reroll failed: {ex.Message}");
        }
    }

    private static int? PredictInteriorId(int seed, RoundManager manager)
    {
        var flows = manager.currentLevel.dungeonFlowTypes;
        if (flows == null || flows.Length == 0)
            return null;

        var rnd = new Random(seed);
        var weights = flows.Select(flow => flow.rarity).ToArray();
        var index = manager.GetRandomWeightedIndex(weights, rnd);
        if (index < 0 || index >= flows.Length)
            return null;

        return flows[index].id;
    }
}

/// <summary>
/// Resolves Mineshaft's flow catalog id by DunGen asset name, with vanilla id fallback.
/// </summary>
internal static class MineshaftId
{
    private const string MineshaftFlowName = "Level3Flow";
    private const int VanillaFallbackId = 4;

    private static int? _cachedId;

    internal static int Resolve(RoundManager manager)
    {
        if (_cachedId.HasValue)
            return _cachedId.Value;

        try
        {
            var flows = manager?.dungeonFlowTypes;
            if (flows != null)
            {
                for (var i = 0; i < flows.Length; i++)
                {
                    var flow = flows[i]?.dungeonFlow;
                    if (flow != null && flow.name == MineshaftFlowName)
                    {
                        _cachedId = i;
                        Plugin.Log.LogDebug($"Resolved Mineshaft as {MineshaftFlowName} (id {i}).");
                        return i;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogDebug($"Mineshaft id name lookup failed, using fallback: {ex.Message}");
        }

        Plugin.Log.LogDebug($"Using Mineshaft fallback id {VanillaFallbackId}.");
        _cachedId = VanillaFallbackId;
        return VanillaFallbackId;
    }
}
