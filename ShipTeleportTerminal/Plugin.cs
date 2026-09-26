using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace ShipTeleportTerminal;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.ShipTeleportTerminal";
    public const string ModName = "ShipTeleportTerminal";
    public const string ModVersion = "1.0.2";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<bool> AllowInverse { get; private set; } = null!;
    internal static ConfigEntry<bool> Verbose { get; private set; } = null!;

    private readonly Harmony _harmony = new(ModGuid);

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Enabled = Config.Bind("General", "Enabled", true,
            "Enable terminal commands: teleport / tp (and inverse variants).");
        AllowInverse = Config.Bind("General", "AllowInverse", true,
            "Allow iteleport / itp / inverse for the inverse teleporter.");
        Verbose = Config.Bind("General", "VerboseLogging", false,
            "Log terminal/teleporter traces.");

        ManualPatches.Apply(_harmony);
        _harmony.PatchAll(typeof(HostModGateDisconnectPatch));
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
