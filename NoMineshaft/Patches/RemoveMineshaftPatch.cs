using System;
using System.Linq;
using HarmonyLib;

namespace NoMineshaft.Patches;

/// <summary>
/// Vanilla interior flow IDs used by SelectableLevel.dungeonFlowTypes.
/// Mineshaft was added as id 4.
/// </summary>
internal enum InteriorType
{
    Factory = 0,
    Manor = 1,
    Mineshaft = 4,
}

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

            var filtered = level.dungeonFlowTypes
                .Where(flow => flow.id != (int)InteriorType.Mineshaft)
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
            Plugin.Log.LogDebug($"Removed Mineshaft from dungeon flows on {level.name}.");
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

            // Ensure Mineshaft is already filtered when possible.
            var withoutMineshaft = flows
                .Where(flow => flow.id != (int)InteriorType.Mineshaft)
                .ToArray();
            if (withoutMineshaft.Length > 0 && withoutMineshaft.Length != flows.Length)
            {
                manager.currentLevel.dungeonFlowTypes = withoutMineshaft;
                flows = withoutMineshaft;
            }

            if (flows.All(flow => flow.id != (int)InteriorType.Mineshaft))
                return;

            if (PredictInterior(__instance.randomMapSeed, manager) != InteriorType.Mineshaft)
                return;

            manager.hasInitializedLevelRandomSeed = false;
            manager.InitializeRandomNumberGenerators();

            for (var i = 0; i < MaxAttempts; i++)
            {
                var candidate = Rng.Next(1, MaxSeed);
                var predicted = PredictInterior(candidate, manager);
                if (predicted is null or InteriorType.Mineshaft)
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

    private static InteriorType? PredictInterior(int seed, RoundManager manager)
    {
        var flows = manager.currentLevel.dungeonFlowTypes;
        if (flows == null || flows.Length == 0)
            return null;

        var rnd = new Random(seed);
        var weights = flows.Select(flow => flow.rarity).ToArray();
        var index = manager.GetRandomWeightedIndex(weights, rnd);
        if (index < 0 || index >= flows.Length)
            return null;

        var id = flows[index].id;
        return Enum.IsDefined(typeof(InteriorType), id) ? (InteriorType)id : null;
    }
}
