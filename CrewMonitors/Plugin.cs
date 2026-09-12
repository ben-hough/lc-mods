using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace CrewMonitors;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(OpenBodyCamsGuid, BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.CrewMonitors";
    public const string ModName = "CrewMonitors";
    public const string ModVersion = "1.0.0";
    public const string OpenBodyCamsGuid = "Zaggy1024.OpenBodyCams";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<int> MaxMonitors { get; private set; } = null!;
    internal static ConfigEntry<bool> IncludeLocalPlayer { get; private set; } = null!;
    internal static ConfigEntry<float> CloneSpacing { get; private set; } = null!;
    internal static ConfigEntry<float> RefreshSeconds { get; private set; } = null!;
    internal static ConfigEntry<bool> PreferReuseVanillaScreens { get; private set; } = null!;
    internal static ConfigEntry<bool> ShowNameplates { get; private set; } = null!;

    private readonly Harmony _harmony = new(ModGuid);

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Enabled = Config.Bind(
            "General",
            "Enabled",
            true,
            "Master toggle for crew body-cam monitors on the ship.");
        MaxMonitors = Config.Bind(
            "General",
            "MaxMonitors",
            8,
            new ConfigDescription(
                "Maximum number of crew monitor slots (1–8).",
                new AcceptableValueRange<int>(1, 8)));
        IncludeLocalPlayer = Config.Bind(
            "General",
            "IncludeLocalPlayer",
            true,
            "Include the local player in crew monitor assignments.");
        CloneSpacing = Config.Bind(
            "General",
            "CloneSpacing",
            0.42f,
            "Local-space spacing between cloned monitor panels along the wall.");
        RefreshSeconds = Config.Bind(
            "General",
            "RefreshSeconds",
            1.0f,
            "How often (seconds) to re-assign living players to monitor slots.");
        PreferReuseVanillaScreens = Config.Bind(
            "General",
            "PreferReuseVanillaScreens",
            true,
            "Reuse unused vanilla ship screens (door SingleScreen, inside/security cam meshes) before cloning.");
        ShowNameplates = Config.Bind(
            "General",
            "ShowNameplates",
            true,
            "Show a simple world-space nameplate under each crew monitor.");

        _harmony.PatchAll(typeof(Plugin).Assembly);
        Log.LogInfo($"{ModName} v{ModVersion} loaded (depends on {OpenBodyCamsGuid}).");
    }
}

internal static class PluginInfo
{
    public const string PLUGIN_GUID = Plugin.ModGuid;
    public const string PLUGIN_NAME = Plugin.ModName;
    public const string PLUGIN_VERSION = Plugin.ModVersion;
}
