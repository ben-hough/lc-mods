using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace NoMineshaft.Patches;

/// <summary>
/// Vanilla interior ids: Factory=0, Manor=1, Mineshaft=4.
/// Strip + seed-reroll (same approach as maintained no-mineshaft mods).
/// </summary>
internal static class MineshaftIds
{
    public const int VanillaMineshaftId = 4;
    private const string FlowName = "Level3Flow";

    private static int[]? _ids;

    /// <summary>All catalog ids that resolve to Mineshaft (vanilla 4 + name matches).</summary>
    internal static int[] Resolve(RoundManager manager)
    {
        if (_ids != null)
            return _ids;

        var found = new System.Collections.Generic.HashSet<int> { VanillaMineshaftId };
        try
        {
            var catalog = manager?.dungeonFlowTypes;
            if (catalog != null)
            {
                for (var i = 0; i < catalog.Length; i++)
                {
                    var flow = catalog[i]?.dungeonFlow;
                    if (flow == null)
                        continue;
                    var name = flow.name ?? "";
                    if (name == FlowName || name.IndexOf("Mine", StringComparison.OrdinalIgnoreCase) >= 0)
                        found.Add(i);
                }
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Mineshaft catalog scan failed: {ex.Message}");
        }

        _ids = found.ToArray();
        Plugin.Log.LogInfo($"Mineshaft interior ids: [{string.Join(", ", _ids)}]");
        return _ids;
    }

    internal static bool IsMineshaft(int id, RoundManager manager) =>
        Resolve(manager).Contains(id);
}

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
            {
                Plugin.Log.LogInfo("GenerateNewFloor: no dungeonFlowTypes on current level.");
                return;
            }

            var banned = MineshaftIds.Resolve(__instance);
            var before = level.dungeonFlowTypes.Select(f => $"{f.id}:{f.rarity}").ToArray();
            var filtered = level.dungeonFlowTypes.Where(f => !banned.Contains(f.id)).ToArray();

            Plugin.Log.LogInfo(
                $"GenerateNewFloor on {level.name}: flows before=[{string.Join(", ", before)}] banned=[{string.Join(", ", banned)}]");

            if (filtered.Length == level.dungeonFlowTypes.Length)
            {
                Plugin.Log.LogInfo("GenerateNewFloor: no Mineshaft entries to strip (already absent or id mismatch).");
                return;
            }

            if (filtered.Length == 0)
            {
                Plugin.Log.LogWarning($"Skipping Mineshaft removal on {level.name}: it would leave no interiors.");
                return;
            }

            level.dungeonFlowTypes = filtered;
            Plugin.Log.LogInfo(
                $"Stripped Mineshaft from {level.name}. Remaining=[{string.Join(", ", filtered.Select(f => $"{f.id}:{f.rarity}"))}]");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to remove Mineshaft: {ex.Message}");
        }

        // If type was already chosen as Mineshaft (seed path), remap before generation runs.
        try
        {
            if (!MineshaftIds.IsMineshaft(__instance.currentDungeonType, __instance))
                return;

            var level = __instance.currentLevel;
            if (level?.dungeonFlowTypes == null || level.dungeonFlowTypes.Length == 0)
                return;

            var banned = MineshaftIds.Resolve(__instance);
            var options = level.dungeonFlowTypes.Where(f => !banned.Contains(f.id)).ToArray();
            if (options.Length == 0)
            {
                Plugin.Log.LogWarning("Mineshaft already selected but no alternate interiors available.");
                return;
            }

            var weights = options.Select(f => Math.Max(f.rarity, 1)).ToArray();
            var idx = __instance.GetRandomWeightedIndex(weights, new System.Random(__instance.playersManager != null ? __instance.playersManager.randomMapSeed : Environment.TickCount));
            if (idx < 0 || idx >= options.Length)
                idx = 0;

            var old = __instance.currentDungeonType;
            __instance.currentDungeonType = options[idx].id;
            Plugin.Log.LogInfo($"Remapped currentDungeonType away from Mineshaft: {old} -> {__instance.currentDungeonType}");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Dungeon type remap failed: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(StartOfRound), nameof(StartOfRound.ChooseNewRandomMapSeed))]
internal static class ChooseNewRandomMapSeedPatch
{
    private const int MaxAttempts = 1000;
    private const int MaxSeed = 100_000_000;
    private static readonly System.Random Rng = new();

    private static void Postfix(StartOfRound __instance)
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return;

        try
        {
            var manager = RoundManager.Instance;
            if (manager?.currentLevel?.dungeonFlowTypes == null)
            {
                Plugin.Log.LogInfo("ChooseNewRandomMapSeed: RoundManager/level not ready.");
                return;
            }

            // Always strip first so prediction + generation never see Mineshaft.
            var banned = MineshaftIds.Resolve(manager);
            var flows = manager.currentLevel.dungeonFlowTypes;
            var without = flows.Where(f => !banned.Contains(f.id)).ToArray();
            if (without.Length > 0 && without.Length != flows.Length)
            {
                manager.currentLevel.dungeonFlowTypes = without;
                flows = without;
                Plugin.Log.LogInfo($"ChooseNewRandomMapSeed: stripped Mineshaft from {manager.currentLevel.name}.");
            }

            if (flows.All(f => !banned.Contains(f.id)))
            {
                Plugin.Log.LogInfo("ChooseNewRandomMapSeed: Mineshaft already absent from moon flows.");
                return;
            }

            var predicted = PredictInteriorId(__instance.randomMapSeed, manager);
            Plugin.Log.LogInfo($"ChooseNewRandomMapSeed: seed {__instance.randomMapSeed} predicts interior id {predicted?.ToString() ?? "null"}.");

            if (predicted is null || !banned.Contains(predicted.Value))
                return;

            manager.hasInitializedLevelRandomSeed = false;
            manager.InitializeRandomNumberGenerators();

            for (var i = 0; i < MaxAttempts; i++)
            {
                var candidate = Rng.Next(1, MaxSeed);
                var next = PredictInteriorId(candidate, manager);
                if (next is null || banned.Contains(next.Value))
                    continue;

                __instance.randomMapSeed = candidate;
                Plugin.Log.LogInfo($"Rerolled map seed to {candidate} (interior {next}) after {i + 1} attempts.");
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

        var rnd = new System.Random(seed);
        var weights = flows.Select(flow => flow.rarity).ToArray();
        var index = manager.GetRandomWeightedIndex(weights, rnd);
        if (index < 0 || index >= flows.Length)
            return null;

        return flows[index].id;
    }
}

/// <summary>
/// Host applies strip again when loading a level (covers paths that skip ChooseNewRandomMapSeed).
/// </summary>
[HarmonyPatch(typeof(RoundManager), nameof(RoundManager.LoadNewLevel))]
internal static class LoadNewLevelPatch
{
    private static void Prefix(RoundManager __instance, int randomSeed, SelectableLevel newLevel)
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value || newLevel?.dungeonFlowTypes == null)
            return;

        try
        {
            var banned = MineshaftIds.Resolve(__instance);
            var filtered = newLevel.dungeonFlowTypes.Where(f => !banned.Contains(f.id)).ToArray();
            if (filtered.Length == 0 || filtered.Length == newLevel.dungeonFlowTypes.Length)
                return;

            newLevel.dungeonFlowTypes = filtered;
            Plugin.Log.LogInfo($"LoadNewLevel: stripped Mineshaft from {newLevel.name} (seed {randomSeed}).");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"LoadNewLevel strip failed: {ex.Message}");
        }
    }
}
