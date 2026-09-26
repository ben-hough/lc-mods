using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ShipStarterKit;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.ShipStarterKit";
    public const string ModName = "ShipStarterKit";
    public const string ModVersion = "1.0.0";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<bool> VerboseLogging { get; private set; } = null!;
    internal static ConfigEntry<bool> SpawnOncePerSave { get; private set; } = null!;

    private readonly Harmony _harmony = new(ModGuid);

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Enabled = Config.Bind(
            "General",
            "Enabled",
            true,
            "Master toggle. Host with this mod enabled supplies starter store equipment on first load of each save.");
        VerboseLogging = Config.Bind(
            "General",
            "VerboseLogging",
            false,
            "Log item matching, pile positions, and save-gate decisions.");
        SpawnOncePerSave = Config.Bind(
            "General",
            "SpawnOncePerSave",
            true,
            "If true (recommended), spawn only once per save file via an ES3 flag on that save. Reloading the same save or returning from moons will not re-spawn. Set false only for testing.");

        ItemCountConfig.BindDefaults(Config);

        try
        {
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo($"{ModName} v{ModVersion} loaded.");
        }
        catch (Exception ex)
        {
            Log.LogError($"Harmony patch failed: {ex}");
        }
    }

    internal static void V(string msg)
    {
        if (VerboseLogging != null && VerboseLogging.Value)
            Log.LogInfo(msg);
    }
}

internal static class PluginInfo
{
    public const string PLUGIN_GUID = Plugin.ModGuid;
    public const string PLUGIN_NAME = Plugin.ModName;
    public const string PLUGIN_VERSION = Plugin.ModVersion;
}
