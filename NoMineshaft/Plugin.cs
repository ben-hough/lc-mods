using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace NoMineshaft;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.NoMineshaft";
    public const string ModName = "NoMineshaft";
    public const string ModVersion = "1.0.4";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<bool> Verbose { get; private set; } = null!;

    private readonly Harmony _harmony = new(ModGuid);

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Enabled = Config.Bind(
            "General",
            "Enabled",
            true,
            "When true, Mineshaft interiors are removed from dungeon rotation.");

        Verbose = Config.Bind(
            "General",
            "VerboseLogging",
            true,
            "Extra Info logs for dungeon scrub/remap/RPC (debugging).");

        if (!Enabled.Value)
        {
            Log.LogInfo($"{ModName} is disabled via config.");
            return;
        }

        _harmony.PatchAll(typeof(Plugin).Assembly);

        var genFloor = AccessTools.Method(typeof(RoundManager), "GenerateNewFloor") != null;
        var loadLevel = AccessTools.Method(typeof(RoundManager), "LoadNewLevel") != null;
        var clientRpc = AccessTools.Method(typeof(RoundManager), "GenerateNewLevelClientRpc") != null;
        var mapSeed = AccessTools.Method(typeof(StartOfRound), "ChooseNewRandomMapSeed") != null;
        Log.LogInfo($"Patch targets: GenerateNewFloor={genFloor}, LoadNewLevel={loadLevel}, GenerateNewLevelClientRpc={clientRpc}, ChooseNewRandomMapSeed={mapSeed}");

        var go = new GameObject("NoMineshaftWatcher");
        Object.DontDestroyOnLoad(go);
        go.AddComponent<DungeonTypeWatcher>();

        Log.LogInfo($"{ModName} v{ModVersion} loaded — Mineshaft interiors disabled. Verbose={Verbose.Value}");
    }

    internal static void V(string msg)
    {
        if (Verbose != null && Verbose.Value)
            Log.LogInfo(msg);
    }
}

internal static class PluginInfo
{
    public const string PLUGIN_GUID = Plugin.ModGuid;
    public const string PLUGIN_NAME = Plugin.ModName;
    public const string PLUGIN_VERSION = Plugin.ModVersion;
}
