using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace DoorTeleporter;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.DoorTeleporter";
    public const string ModName = "DoorTeleporter";
    public const string ModVersion = "1.0.0";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<bool> IncludeMain { get; private set; } = null!;
    internal static ConfigEntry<bool> IncludeFireExits { get; private set; } = null!;
    internal static ConfigEntry<float> StandBackMeters { get; private set; } = null!;
    internal static ConfigEntry<bool> Verbose { get; private set; } = null!;

    private readonly Harmony _harmony = new(ModGuid);

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Enabled = Config.Bind("General", "Enabled", true,
            "Enable terminal commands: doorport / dtp / exitport / fireexit.");
        IncludeMain = Config.Bind("General", "IncludeMainEntrance", true,
            "Allow teleporting to the main facility entrance (entranceId 0).");
        IncludeFireExits = Config.Bind("General", "IncludeFireExits", true,
            "Allow teleporting to fire-exit entrances (entranceId > 0).");
        StandBackMeters = Config.Bind("General", "StandBackMeters", 0.35f,
            "Nudge slightly back from the entrance point so you stand in front of the door.");
        Verbose = Config.Bind("General", "VerboseLogging", false,
            "Log terminal/door-teleport traces.");

        ManualPatches.Apply(_harmony);
        Log.LogInfo($"{ModName} v{ModVersion} loaded.");
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
