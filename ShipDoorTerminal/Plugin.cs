using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ShipDoorTerminal;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.ShipDoorTerminal";
    public const string ModName = "ShipDoorTerminal";
    public const string ModVersion = "1.0.10";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<bool> Verbose { get; private set; } = null!;

    private readonly Harmony _harmony = new(ModGuid);

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Enabled = Config.Bind("General", "Enabled", true,
            "Enable terminal commands: door / doors / opendoor / closedoor.");
        Verbose = Config.Bind("General", "VerboseLogging", true,
            "Log terminal/door traces.");

        ManualPatches.Apply(_harmony);
        Log.LogInfo($"{ModName} v{ModVersion} loaded. Verbose={Verbose.Value}");
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
