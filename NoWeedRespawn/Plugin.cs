using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace NoWeedRespawn;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.NoWeedRespawn";
    public const string ModName = "NoWeedRespawn";
    public const string ModVersion = "1.0.1";

    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<bool> BlockIndoorRespawn { get; private set; } = null!;
    internal static ConfigEntry<bool> BlockOutdoorRespawn { get; private set; } = null!;
    internal static ConfigEntry<bool> VerboseLogging { get; private set; } = null!;

    private void Awake()
    {
        Log = Logger;

        Enabled = Config.Bind(
            "General",
            "Enabled",
            true,
            "Master toggle. When false, all patches no-op.");
        BlockIndoorRespawn = Config.Bind(
            "General",
            "BlockIndoorRespawn",
            true,
            "Keep indoor cadaver/weed tiles eradicated after they are fully cleared.");
        BlockOutdoorRespawn = Config.Bind(
            "General",
            "BlockOutdoorRespawn",
            true,
            "Prevent outdoor vain shroud / mold from regenerating after clears this moon.");
        VerboseLogging = Config.Bind(
            "General",
            "VerboseLogging",
            false,
            "Log patch hits (eradication keep, spawn blocks, GenerateMold skips).");

        new Harmony(ModGuid).PatchAll(typeof(Plugin).Assembly);
        Log.LogInfo($"{ModName} v{ModVersion} loaded. Host should run this for networked growth RPCs.");
    }

    internal static bool IndoorActive => HostModGate.IndoorActive;
    internal static bool OutdoorActive => HostModGate.OutdoorActive;

    internal static void VLog(string message)
    {
        if (VerboseLogging.Value)
            Log.LogInfo(message);
    }
}

internal static class PluginInfo
{
    public const string PLUGIN_GUID = Plugin.ModGuid;
    public const string PLUGIN_NAME = Plugin.ModName;
    public const string PLUGIN_VERSION = Plugin.ModVersion;
}
