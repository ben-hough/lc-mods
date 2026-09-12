using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace UnbreakableSurfaces;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.UnbreakableSurfaces";
    public const string ModName = "UnbreakableSurfaces";
    public const string ModVersion = "1.0.1";

    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<bool> Bridges { get; private set; } = null!;
    internal static ConfigEntry<bool> BreakableSurfaces { get; private set; } = null!;

    private void Awake()
    {
        Log = Logger;
        Enabled = Config.Bind("General", "Enabled", true, "Master toggle.");
        Bridges = Config.Bind("General", "Bridges", true, "Vow-style bridges never collapse.");
        BreakableSurfaces = Config.Bind("General", "BreakableSurfaces", true, "Unstable platforms / type-2 bridges never fall.");

        new Harmony(ModGuid).PatchAll(typeof(Plugin).Assembly);
        Log.LogInfo($"{ModName} v{ModVersion} loaded.");
    }
}

internal static class PluginInfo
{
    public const string PLUGIN_GUID = Plugin.ModGuid;
    public const string PLUGIN_NAME = Plugin.ModName;
    public const string PLUGIN_VERSION = Plugin.ModVersion;
}
