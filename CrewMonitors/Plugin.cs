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
    public const string ModVersion = "1.0.32";
    public const string OpenBodyCamsGuid = "Zaggy1024.OpenBodyCams";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<int> MaxMonitors { get; private set; } = null!;
    internal static ConfigEntry<bool> IncludeLocalPlayer { get; private set; } = null!;
    internal static ConfigEntry<float> CloneSpacing { get; private set; } = null!;
    internal static ConfigEntry<float> CloneScaleFactor { get; private set; } = null!;
    internal static ConfigEntry<float> WallInset { get; private set; } = null!;
    internal static ConfigEntry<float> GridVerticalOffset { get; private set; } = null!;
    internal static ConfigEntry<float> GridHorizontalOffset { get; private set; } = null!;
    internal static ConfigEntry<float> GridYawDegrees { get; private set; } = null!;
    internal static ConfigEntry<float> RefreshSeconds { get; private set; } = null!;
    internal static ConfigEntry<bool> PreferReuseVanillaScreens { get; private set; } = null!;
    internal static ConfigEntry<bool> ShowNameplates { get; private set; } = null!;
    internal static ConfigEntry<bool> AlwaysShowMonitors { get; private set; } = null!;
    internal static ConfigEntry<int> GridColumns { get; private set; } = null!;
    internal static ConfigEntry<float> ScreenBrightness { get; private set; } = null!;

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
            4,
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
            "Local-space spacing between cloned monitor panels on the Cube.001 wall plane.");
        CloneScaleFactor = Config.Bind(
            "General",
            "CloneScaleFactor",
            0.60f,
            new ConfigDescription(
                "User-facing panel size knob for SingleScreen clones. Mapped so 0.6 ≈ ~2× SingleScreen.localScale (prior good wall-tile size). Formula: localScale = SingleScreen.localScale * (CloneScaleFactor * 2.0 / 0.6).",
                new AcceptableValueRange<float>(0.1f, 1.0f)));
        WallInset = Config.Bind(
            "General",
            "WallInset",
            0.55f,
            new ConfigDescription(
                "Flush inset into Cube.001 along the thin (depth) axis from the room-facing face toward the wall center.",
                new AcceptableValueRange<float>(-1f, 1.5f)));
        GridVerticalOffset = Config.Bind(
            "General",
            "GridVerticalOffset",
            -0.45f,
            new ConfigDescription(
                "Offset of the first crew-monitor row along the wall-plane vertical axis in Cube.001 local space.",
                new AcceptableValueRange<float>(-2.5f, 2.5f)));
        GridHorizontalOffset = Config.Bind(
            "General",
            "GridHorizontalOffset",
            3.68f,
            new ConfigDescription(
                "Offset of the whole grid along the wall-plane horizontal axis in Cube.001 local space (positive = left in Glims Mods).",
                new AcceptableValueRange<float>(-8f, 8f)));
        GridYawDegrees = Config.Bind(
            "General",
            "GridYawDegrees",
            20f,
            new ConfigDescription(
                "Rotation (degrees) of each panel around local Z (Quaternion.Euler(0,0,z)). Positive = CCW when looking along +Z.",
                new AcceptableValueRange<float>(-90f, 90f)));
        RefreshSeconds = Config.Bind(
            "General",
            "RefreshSeconds",
            1.0f,
            "How often (seconds) to re-assign living players to monitor slots.");
        PreferReuseVanillaScreens = Config.Bind(
            "General",
            "PreferReuseVanillaScreens",
            false,
            "Reuse whitelisted unused vanilla screens (door SingleScreen, inside cam mesh) before cloning. Default false leaves vanilla door/internal cams alone and builds a clone grid on the main monitor wall.");
        ShowNameplates = Config.Bind(
            "General",
            "ShowNameplates",
            true,
            "Show a simple world-space nameplate under each crew monitor.");
        AlwaysShowMonitors = Config.Bind(
            "General",
            "AlwaysShowMonitors",
            true,
            "Always keep all 4 monitor panels visible. Empty slots stay Off (black) until a player is assigned from the bottom up. No round-robin duplication.");
        GridColumns = Config.Bind(
            "General",
            "GridColumns",
            1,
            new ConfigDescription(
                "Columns in the clone grid. Use 1 for a vertical stack.",
                new AcceptableValueRange<int>(1, 8)));
        ScreenBrightness = Config.Bind(
            "General",
            "ScreenBrightness",
            0.2f,
            new ConfigDescription(
                "Night-vision fill on crew body cams (1.0 = stock OpenBodyCams). Low values keep interior feeds from going milky. Screen emissive uses OpenBodyCams MonitorEmissiveColor (same as the main body-cam panel).",
                new AcceptableValueRange<float>(0.0f, 1.0f)));

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
