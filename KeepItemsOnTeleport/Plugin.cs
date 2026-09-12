using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace KeepItemsOnTeleport;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.KeepItemsOnTeleport";
    public const string ModName = "KeepItemsOnTeleport";
    public const string ModVersion = "1.0.0";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<bool> KeepOnNormalTeleporter { get; private set; } = null!;
    internal static ConfigEntry<bool> KeepOnInverseTeleporter { get; private set; } = null!;
    internal static ConfigEntry<bool> Verbose { get; private set; } = null!;

    private readonly Harmony _harmony = new(ModGuid);

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Enabled = Config.Bind("General", "Enabled", true, "Master toggle — keep inventory when ship-teleported.");
        KeepOnNormalTeleporter = Config.Bind(
            "General",
            "KeepOnNormalTeleporter",
            true,
            "Keep items when beamed back to the ship (normal teleporter).");
        KeepOnInverseTeleporter = Config.Bind(
            "General",
            "KeepOnInverseTeleporter",
            true,
            "Keep items when beamed into the facility (inverse teleporter).");
        Verbose = Config.Bind("General", "VerboseLogging", false, "Log when a teleport drop is skipped.");

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
