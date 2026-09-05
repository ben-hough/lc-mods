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
    public const string ModVersion = "1.0.0";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<bool> ShowDistance { get; private set; } = null!;
    internal static ConfigEntry<float> HudScale { get; private set; } = null!;
    internal static ConfigEntry<float> VerticalOffset { get; private set; } = null!;

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
            0.12f,
            "Vertical position on screen (0 = top, 1 = bottom). Default sits near the top.");

        _harmony.PatchAll(typeof(Plugin).Assembly);

        var go = new GameObject("ShipBeaconHUD");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<ShipBeaconHud>();

        Log.LogInfo($"{ModName} v{ModVersion} loaded.");
    }
}

internal static class PluginInfo
{
    public const string PLUGIN_GUID = Plugin.ModGuid;
    public const string PLUGIN_NAME = Plugin.ModName;
    public const string PLUGIN_VERSION = Plugin.ModVersion;
}
