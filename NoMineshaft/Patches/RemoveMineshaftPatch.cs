using System;
using System.Linq;
using HarmonyLib;
using UnityEngine;

namespace NoMineshaft.Patches;

/// <summary>
/// Vanilla interior ids: Factory=0, Manor=1, Mineshaft=4.
/// Scrub every moon's flow list, zero rarity, seed-reroll, and remap currentDungeonType.
/// </summary>
internal static class MineshaftIds
{
    public const int VanillaMineshaftId = 4;

    internal static bool IsMineshaft(int id) => id == VanillaMineshaftId;
}

internal static class MineshaftScrubber
{
    /// <summary>Remove Mineshaft entries (or zero rarity) on one level. Returns true if changed.</summary>
    internal static bool ScrubLevel(SelectableLevel level, string context)
    {
        if (level?.dungeonFlowTypes == null || level.dungeonFlowTypes.Length == 0)
            return false;

        var before = level.dungeonFlowTypes.Select(f => $"{f.id}:{f.rarity}").ToArray();
        var filtered = level.dungeonFlowTypes.Where(f => !MineshaftIds.IsMineshaft(f.id)).ToArray();

        if (filtered.Length == 0)
        {
            // Keep list but zero rarity so weighted pick can't land on it.
            var changed = false;
            for (var i = 0; i < level.dungeonFlowTypes.Length; i++)
            {
                var f = level.dungeonFlowTypes[i];
                if (!MineshaftIds.IsMineshaft(f.id) || f.rarity == 0)
                    continue;
                f.rarity = 0;
                level.dungeonFlowTypes[i] = f;
                changed = true;
            }

            if (changed)
                Plugin.Log.LogInfo($"{context}: zeroed Mineshaft rarity on {level.name} (only interior). before=[{string.Join(", ", before)}]");
            return changed;
        }

        if (filtered.Length == level.dungeonFlowTypes.Length)
            return false;

        level.dungeonFlowTypes = filtered;
        Plugin.Log.LogInfo(
            $"{context}: stripped Mineshaft from {level.name}. before=[{string.Join(", ", before)}] after=[{string.Join(", ", filtered.Select(f => $"{f.id}:{f.rarity}"))}]");
        return true;
    }

    internal static void ScrubAllLevels(string context)
    {
        var start = StartOfRound.Instance;
        if (start?.levels == null)
        {
            Plugin.Log.LogInfo($"{context}: StartOfRound.levels is null — cannot scrub yet.");
            return;
        }

        var n = 0;
        foreach (var level in start.levels)
        {
            if (ScrubLevel(level, context))
                n++;
        }

        Plugin.Log.LogInfo($"{context}: scrubbed Mineshaft from {n}/{start.levels.Length} moons.");
    }

    internal static bool RemapDungeonType(RoundManager manager, string context)
    {
        if (manager == null || !MineshaftIds.IsMineshaft(manager.currentDungeonType))
            return false;

        var level = manager.currentLevel;
        if (level?.dungeonFlowTypes == null || level.dungeonFlowTypes.Length == 0)
        {
            Plugin.Log.LogWarning($"{context}: currentDungeonType is Mineshaft but level has no flows.");
            return false;
        }

        var options = level.dungeonFlowTypes.Where(f => !MineshaftIds.IsMineshaft(f.id) && f.rarity > 0).ToArray();
        if (options.Length == 0)
            options = level.dungeonFlowTypes.Where(f => !MineshaftIds.IsMineshaft(f.id)).ToArray();

        if (options.Length == 0)
        {
            Plugin.Log.LogWarning($"{context}: Mineshaft selected but no alternate interiors on {level.name}.");
            return false;
        }

        var seed = StartOfRound.Instance != null ? StartOfRound.Instance.randomMapSeed : Environment.TickCount;
        var weights = options.Select(f => Math.Max(f.rarity, 1)).ToArray();
        var idx = manager.GetRandomWeightedIndex(weights, new System.Random(seed ^ 0x4D1));
        if (idx < 0 || idx >= options.Length)
            idx = 0;

        var old = manager.currentDungeonType;
        manager.currentDungeonType = options[idx].id;
        Plugin.Log.LogInfo($"{context}: remapped currentDungeonType {old} -> {manager.currentDungeonType} on {level.name}.");
        return true;
    }
}

[HarmonyPatch(typeof(StartOfRound), "Start")]
internal static class StartOfRoundStartPatch
{
    private static void Postfix()
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return;

        try
        {
            MineshaftScrubber.ScrubAllLevels("StartOfRound.Start");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Start scrub failed: {ex.Message}");
        }
    }
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
            Plugin.Log.LogInfo(
                $("GenerateNewFloor Prefix: level={__instance.currentLevel?.name}, currentDungeonType={__instance.currentDungeonType}, isHost={__instance.IsServer}");

            MineshaftScrubber.ScrubLevel(__instance.currentLevel, "GenerateNewFloor");
            // Always attempt remap — do not return early after scrub.
            MineshaftScrubber.RemapDungeonType(__instance, "GenerateNewFloor");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"GenerateNewFloor Prefix failed: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(RoundManager), nameof(RoundManager.LoadNewLevel))]
internal static class LoadNewLevelPatch
{
    private static void Prefix(RoundManager __instance, int randomSeed, SelectableLevel newLevel)
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return;

        try
        {
            Plugin.Log.LogInfo($"LoadNewLevel Prefix: seed={randomSeed}, level={newLevel?.name}");
            MineshaftScrubber.ScrubLevel(newLevel, "LoadNewLevel");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"LoadNewLevel strip failed: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(RoundManager), nameof(RoundManager.GenerateNewLevelClientRpc))]
internal static class GenerateNewLevelClientRpcPatch
{
    private static void Prefix(RoundManager __instance, int randomSeed, int levelID)
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return;

        try
        {
            Plugin.Log.LogInfo(
                $("GenerateNewLevelClientRpc: seed={randomSeed}, levelID={levelID}, currentDungeonType={__instance.currentDungeonType}");

            // Scrub before local generation mirrors host decision as closely as possible.
            MineshaftScrubber.ScrubLevel(__instance.currentLevel, "GenerateNewLevelClientRpc");
            MineshaftScrubber.RemapDungeonType(__instance, "GenerateNewLevelClientRpc");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"GenerateNewLevelClientRpc Prefix failed: {ex.Message}");
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

            MineshaftScrubber.ScrubLevel(manager.currentLevel, "ChooseNewRandomMapSeed");

            var flows = manager.currentLevel.dungeonFlowTypes;
            if (flows.All(f => !MineshaftIds.IsMineshaft(f.id) || f.rarity <= 0))
            {
                Plugin.Log.LogInfo("ChooseNewRandomMapSeed: Mineshaft already absent/zeroed.");
                return;
            }

            var predicted = PredictInteriorId(__instance.randomMapSeed, manager);
            Plugin.Log.LogInfo($"ChooseNewRandomMapSeed: seed {__instance.randomMapSeed} predicts id {predicted?.ToString() ?? "null"}.");

            if (predicted is null || !MineshaftIds.IsMineshaft(predicted.Value))
                return;

            manager.hasInitializedLevelRandomSeed = false;
            manager.InitializeRandomNumberGenerators();

            for (var i = 0; i < MaxAttempts; i++)
            {
                var candidate = Rng.Next(1, MaxSeed);
                var next = PredictInteriorId(candidate, manager);
                if (next is null || MineshaftIds.IsMineshaft(next.Value))
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

        // Mirror vanilla: ignore zero-rarity entries if any remain.
        var usable = flows.Where(f => f.rarity > 0).ToArray();
        if (usable.Length == 0)
            usable = flows;

        var rnd = new System.Random(seed);
        var weights = usable.Select(flow => flow.rarity).ToArray();
        var index = manager.GetRandomWeightedIndex(weights, rnd);
        if (index < 0 || index >= usable.Length)
            return null;

        return usable[index].id;
    }
}
