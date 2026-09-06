using System;
using System.Linq;
using System.Reflection;
using DunGen;
using DunGen.Graph;
using HarmonyLib;
using UnityEngine;

namespace NoMineshaft;

internal static class MineshaftIds
{
    public const int VanillaMineshaftId = 4;
    public const string FlowName = "Level3Flow";

    internal static bool IsMineshaftId(int id) => id == VanillaMineshaftId;

    internal static bool IsMineshaftFlow(DungeonFlow? flow)
    {
        if (flow == null) return false;
        var name = flow.name ?? "";
        return name == FlowName
               || name.IndexOf("Level3", StringComparison.OrdinalIgnoreCase) >= 0
               || (name.IndexOf("Mine", StringComparison.OrdinalIgnoreCase) >= 0
                   && name.IndexOf("Flow", StringComparison.OrdinalIgnoreCase) >= 0);
    }
}

internal static class MineshaftScrubber
{
    internal static bool ScrubLevel(SelectableLevel? level, string context)
    {
        if (level?.dungeonFlowTypes == null || level.dungeonFlowTypes.Length == 0)
        {
            Plugin.Log.LogInfo($"{context}: no dungeonFlowTypes on {level?.name ?? "null"}");
            return false;
        }

        var before = string.Join(", ", level.dungeonFlowTypes.Select(f => $"{f.id}:{f.rarity}"));
        var filtered = level.dungeonFlowTypes.Where(f => !MineshaftIds.IsMineshaftId(f.id)).ToArray();

        if (filtered.Length == 0)
        {
            var changed = false;
            for (var i = 0; i < level.dungeonFlowTypes.Length; i++)
            {
                var f = level.dungeonFlowTypes[i];
                if (!MineshaftIds.IsMineshaftId(f.id) || f.rarity == 0) continue;
                f.rarity = 0;
                level.dungeonFlowTypes[i] = f;
                changed = true;
            }
            if (changed)
                Plugin.Log.LogInfo($"{context}: zeroed Mineshaft rarity on {level.name}. before=[{before}]");
            return changed;
        }

        if (filtered.Length == level.dungeonFlowTypes.Length)
        {
            Plugin.V($"{context}: {level.name} already has no Mineshaft id entries. flows=[{before}]");
            return false;
        }

        level.dungeonFlowTypes = filtered;
        Plugin.Log.LogInfo(
            $"{context}: stripped Mineshaft from {level.name}. before=[{before}] after=[{string.Join(", ", filtered.Select(f => $"{f.id}:{f.rarity}"))}]");
        return true;
    }

    internal static void ScrubAllLevels(string context)
    {
        var start = StartOfRound.Instance;
        if (start?.levels == null)
        {
            Plugin.Log.LogInfo($"{context}: StartOfRound.levels is null");
            return;
        }

        var n = 0;
        foreach (var level in start.levels)
        {
            if (ScrubLevel(level, context)) n++;
        }
        Plugin.Log.LogInfo($"{context}: scrubbed {n}/{start.levels.Length} moons");
    }

    internal static bool RemapDungeonType(RoundManager manager, string context)
    {
        if (manager == null || !MineshaftIds.IsMineshaftId(manager.currentDungeonType))
            return false;

        var level = manager.currentLevel;
        if (level?.dungeonFlowTypes == null || level.dungeonFlowTypes.Length == 0)
        {
            Plugin.Log.LogWarning($"{context}: Mineshaft type set but level has no flows");
            return false;
        }

        var options = level.dungeonFlowTypes.Where(f => !MineshaftIds.IsMineshaftId(f.id) && f.rarity > 0).ToArray();
        if (options.Length == 0)
            options = level.dungeonFlowTypes.Where(f => !MineshaftIds.IsMineshaftId(f.id)).ToArray();

        if (options.Length == 0)
        {
            Plugin.Log.LogWarning($"{context}: no alternate interiors on {level.name}");
            return false;
        }

        var seed = StartOfRound.Instance != null ? StartOfRound.Instance.randomMapSeed : Environment.TickCount;
        var weights = options.Select(f => Math.Max(f.rarity, 1)).ToArray();
        var idx = manager.GetRandomWeightedIndex(weights, new System.Random(seed ^ 0x4D1));
        if (idx < 0 || idx >= options.Length) idx = 0;

        var old = manager.currentDungeonType;
        manager.currentDungeonType = options[idx].id;
        Plugin.Log.LogInfo($"{context}: remapped currentDungeonType {old} -> {manager.currentDungeonType} on {level.name}");
        return true;
    }

    internal static DungeonFlow? PickAlternateFlow(RoundManager manager, out int newId)
    {
        newId = 0;
        var level = manager?.currentLevel;
        var catalog = manager?.dungeonFlowTypes;
        if (level?.dungeonFlowTypes == null || catalog == null)
            return null;

        foreach (var entry in level.dungeonFlowTypes)
        {
            if (MineshaftIds.IsMineshaftId(entry.id) || entry.rarity <= 0)
                continue;
            if (entry.id < 0 || entry.id >= catalog.Length)
                continue;
            var flow = catalog[entry.id]?.dungeonFlow;
            if (flow == null || MineshaftIds.IsMineshaftFlow(flow))
                continue;
            newId = entry.id;
            return flow;
        }

        // Fallback: first non-mineshaft catalog entry
        for (var i = 0; i < catalog.Length; i++)
        {
            var flow = catalog[i]?.dungeonFlow;
            if (flow == null || MineshaftIds.IsMineshaftFlow(flow))
                continue;
            newId = i;
            return flow;
        }

        return null;
    }
}

internal static class ManualPatches
{
    internal static void Apply(Harmony harmony)
    {
        void Patch(Type type, string name, Type patchClass, string prefixName, Type[]? args = null)
        {
            var method = args == null
                ? AccessTools.Method(type, name)
                : AccessTools.Method(type, name, args);
            Plugin.Log.LogInfo($"Resolve {type.Name}.{name} => {(method == null ? "NULL" : (method.IsPublic ? "public" : "nonpublic"))}");
            if (method == null) return;
            harmony.Patch(method, prefix: new HarmonyMethod(patchClass, prefixName));
            Plugin.Log.LogInfo($"Patched {type.Name}.{name}");
        }

        try
        {
            Patch(typeof(RoundManager), "GenerateNewFloor", typeof(GenFloorPatch), nameof(GenFloorPatch.Prefix));
            var genFloor = AccessTools.Method(typeof(RoundManager), "GenerateNewFloor");
            if (genFloor != null)
            {
                harmony.Patch(genFloor, postfix: new HarmonyMethod(typeof(GenFloorPostfixPatch), nameof(GenFloorPostfixPatch.Postfix)));
                Plugin.Log.LogInfo("Patched RoundManager.GenerateNewFloor postfix");
            }
            Patch(typeof(RoundManager), "LoadNewLevel", typeof(LoadLevelPatch), nameof(LoadLevelPatch.Prefix));
            Patch(typeof(RoundManager), "GenerateNewLevelClientRpc", typeof(ClientRpcPatch), nameof(ClientRpcPatch.Prefix));
            var seed = AccessTools.Method(typeof(StartOfRound), "ChooseNewRandomMapSeed");
            Plugin.Log.LogInfo($"Resolve StartOfRound.ChooseNewRandomMapSeed => {(seed == null ? "NULL" : "ok")}");
            if (seed != null)
            {
                harmony.Patch(seed, postfix: new HarmonyMethod(typeof(MapSeedPatch), nameof(MapSeedPatch.Postfix)));
                Plugin.Log.LogInfo("Patched StartOfRound.ChooseNewRandomMapSeed");
            }

            var start = AccessTools.Method(typeof(StartOfRound), "Start");
            Plugin.Log.LogInfo($"Resolve StartOfRound.Start => {(start == null ? "NULL" : "ok")}");
            if (start != null)
            {
                harmony.Patch(start, postfix: new HarmonyMethod(typeof(StartPatch), nameof(StartPatch.Postfix)));
                Plugin.Log.LogInfo("Patched StartOfRound.Start");
            }

            // Critical: runs on host AND client at actual DunGen time
            var generate = AccessTools.Method(typeof(DungeonGenerator), "Generate", Type.EmptyTypes)
                           ?? AccessTools.Method(typeof(DungeonGenerator), "Generate");
            Plugin.Log.LogInfo($"Resolve DungeonGenerator.Generate => {(generate == null ? "NULL" : generate.ToString())}");
            if (generate != null)
            {
                harmony.Patch(generate, prefix: new HarmonyMethod(typeof(DunGenPatch), nameof(DunGenPatch.Prefix)));
                Plugin.Log.LogInfo("Patched DungeonGenerator.Generate");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"ManualPatches.Apply failed: {ex}");
        }
    }
}

internal static class GenFloorPatch
{
    public static void Prefix(RoundManager __instance)
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value) return;
        try
        {
            Plugin.Log.LogInfo($"[GenerateNewFloor] level={__instance.currentLevel?.name} type={__instance.currentDungeonType} isServer={__instance.IsServer}");
            MineshaftScrubber.ScrubLevel(__instance.currentLevel, "GenerateNewFloor");
            MineshaftScrubber.RemapDungeonType(__instance, "GenerateNewFloor");
        }
        catch (Exception ex) { Plugin.Log.LogWarning($"[GenerateNewFloor] {ex.Message}"); }
    }
}

internal static class LoadLevelPatch
{
    public static void Prefix(RoundManager __instance, int randomSeed, SelectableLevel newLevel)
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value) return;
        try
        {
            Plugin.Log.LogInfo($"[LoadNewLevel] seed={randomSeed} level={newLevel?.name}");
            MineshaftScrubber.ScrubLevel(newLevel, "LoadNewLevel");
        }
        catch (Exception ex) { Plugin.Log.LogWarning($"[LoadNewLevel] {ex.Message}"); }
    }
}

internal static class ClientRpcPatch
{
    public static void Prefix(RoundManager __instance, int randomSeed, int levelID)
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value) return;
        try
        {
            Plugin.Log.LogInfo($"[GenerateNewLevelClientRpc] seed={randomSeed} levelID={levelID} type={__instance.currentDungeonType}");
            MineshaftScrubber.ScrubLevel(__instance.currentLevel, "GenerateNewLevelClientRpc");
            MineshaftScrubber.RemapDungeonType(__instance, "GenerateNewLevelClientRpc");
        }
        catch (Exception ex) { Plugin.Log.LogWarning($"[GenerateNewLevelClientRpc] {ex.Message}"); }
    }
}

internal static class MapSeedPatch
{
    private static readonly System.Random Rng = new();

    public static void Postfix(StartOfRound __instance)
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value) return;
        try
        {
            var manager = RoundManager.Instance;
            Plugin.Log.LogInfo($"[ChooseNewRandomMapSeed] seed={__instance.randomMapSeed} level={manager?.currentLevel?.name}");
            if (manager?.currentLevel == null) return;

            MineshaftScrubber.ScrubLevel(manager.currentLevel, "ChooseNewRandomMapSeed");

            var flows = manager.currentLevel.dungeonFlowTypes;
            if (flows == null || flows.Length == 0) return;
            if (flows.All(f => !MineshaftIds.IsMineshaftId(f.id) || f.rarity <= 0))
            {
                Plugin.Log.LogInfo("[ChooseNewRandomMapSeed] Mineshaft already absent/zeroed");
                return;
            }

            // If seed still predicts mineshaft, reroll
            for (var i = 0; i < 1000; i++)
            {
                var candidate = i == 0 ? __instance.randomMapSeed : Rng.Next(1, 100_000_000);
                var predicted = Predict(candidate, manager);
                if (predicted is null || MineshaftIds.IsMineshaftId(predicted.Value))
                    continue;
                if (candidate != __instance.randomMapSeed)
                {
                    __instance.randomMapSeed = candidate;
                    Plugin.Log.LogInfo($"[ChooseNewRandomMapSeed] rerolled to {candidate} (interior {predicted}) after {i + 1} tries");
                }
                else
                {
                    Plugin.Log.LogInfo($"[ChooseNewRandomMapSeed] seed {candidate} ok (interior {predicted})");
                }
                return;
            }
            Plugin.Log.LogWarning("[ChooseNewRandomMapSeed] failed to find non-Mineshaft seed");
        }
        catch (Exception ex) { Plugin.Log.LogWarning($"[ChooseNewRandomMapSeed] {ex.Message}"); }
    }

    private static int? Predict(int seed, RoundManager manager)
    {
        var flows = manager.currentLevel.dungeonFlowTypes;
        var usable = flows.Where(f => f.rarity > 0).ToArray();
        if (usable.Length == 0) usable = flows;
        var rnd = new System.Random(seed);
        var weights = usable.Select(f => f.rarity).ToArray();
        var index = manager.GetRandomWeightedIndex(weights, rnd);
        if (index < 0 || index >= usable.Length) return null;
        return usable[index].id;
    }
}

internal static class StartPatch
{
    public static void Postfix()
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value) return;
        Plugin.Log.LogInfo("[StartOfRound.Start] ensuring watcher + scrubbing moons");
        DungeonTypeWatcher.EnsureExists();
        try { MineshaftScrubber.ScrubAllLevels("StartOfRound.Start"); }
        catch (Exception ex) { Plugin.Log.LogWarning(ex.Message); }
    }
}

internal static class DunGenPatch
{
    public static void Prefix(DungeonGenerator __instance)
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value) return;

        try
        {
            var flow = __instance.DungeonFlow;
            var name = flow != null ? flow.name : "null";
            Plugin.Log.LogInfo($"[DunGen.Generate] flow={name}");

            if (!MineshaftIds.IsMineshaftFlow(flow))
                return;

            var rm = RoundManager.Instance;
            if (rm == null)
            {
                Plugin.Log.LogWarning("[DunGen.Generate] Mineshaft flow but RoundManager null");
                return;
            }

            MineshaftScrubber.ScrubLevel(rm.currentLevel, "DunGen.Generate");
            var alt = MineshaftScrubber.PickAlternateFlow(rm, out var newId);
            if (alt == null)
            {
                Plugin.Log.LogWarning("[DunGen.Generate] no alternate DungeonFlow available");
                return;
            }

            Plugin.Log.LogInfo($"[DunGen.Generate] REPLACING Mineshaft flow {name} with {alt.name} (id {newId})");
            __instance.DungeonFlow = alt;
            rm.currentDungeonType = newId;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[DunGen.Generate] {ex}");
        }
    }
}

/// <summary>After vanilla picks dungeon type — last chance before DunGen runs.</summary>
internal static class GenFloorPostfixPatch
{
    public static void Postfix(RoundManager __instance)
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value) return;
        try
        {
            Plugin.Log.LogInfo($"[GenerateNewFloor.Post] type={__instance.currentDungeonType} level={__instance.currentLevel?.name} isServer={__instance.IsServer}");
            if (!MineshaftIds.IsMineshaftId(__instance.currentDungeonType))
                return;

            MineshaftScrubber.RemapDungeonType(__instance, "GenerateNewFloor.Post");
            var alt = MineshaftScrubber.PickAlternateFlow(__instance, out var newId);
            if (alt == null)
            {
                Plugin.Log.LogWarning("[GenerateNewFloor.Post] still Mineshaft, no alternate flow");
                return;
            }

            __instance.currentDungeonType = newId;
            if (__instance.dungeonGenerator?.Generator != null)
            {
                __instance.dungeonGenerator.Generator.DungeonFlow = alt;
                Plugin.Log.LogInfo($"[GenerateNewFloor.Post] forced DungeonFlow={alt.name} id={newId}");
            }
        }
        catch (Exception ex) { Plugin.Log.LogWarning($"[GenerateNewFloor.Post] {ex.Message}"); }
    }
}
