using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace ShipBeacon;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.ShipBeacon";
    public const string ModName = "ShipBeacon";
    public const string ModVersion = "1.0.12";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<bool> ShowDistance { get; private set; } = null!;
    internal static ConfigEntry<float> HudScale { get; private set; } = null!;
    internal static ConfigEntry<float> VerticalOffset { get; private set; } = null!;
    internal static ConfigEntry<bool> MatchClockStyle { get; private set; } = null!;

    private readonly Harmony _harmony = new(ModGuid);

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Enabled = Config.Bind("General", "Enabled", true, "Show the outdoor ship beacon HUD.");
        ShowDistance = Config.Bind("General", "ShowDistance", true, "Show distance to the ship in meters.");
        HudScale = Config.Bind("General", "HudScale", 1.0f, "Scale of the beacon HUD text.");
        VerticalOffset = Config.Bind(
            "General",
            "VerticalOffset",
            0.10f,
            "Beacon text vertical position (0 = bottom, 1 = top). Default sits near the bottom of the screen.");
        MatchClockStyle = Config.Bind(
            "General",
            "MatchClockStyle",
            true,
            "Use the orange clock HUD color/font when available.");

        _harmony.PatchAll(typeof(Plugin).Assembly);
        ShipBeaconHud.EnsureExists();

        Log.LogInfo($"{ModName} v{ModVersion} loaded (bottom orange HUD style).");
    }
}

internal static class PluginInfo
{
    public const string PLUGIN_GUID = Plugin.ModGuid;
    public const string PLUGIN_NAME = Plugin.ModName;
    public const string PLUGIN_VERSION = Plugin.ModVersion;
}
