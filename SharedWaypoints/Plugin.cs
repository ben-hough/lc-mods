using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SharedWaypoints;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.SharedWaypoints";
    public const string ModName = "SharedWaypoints";
    public const string ModVersion = "1.0.1";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<KeyCode> DropKey { get; private set; } = null!;
    internal static ConfigEntry<KeyCode> ClearKey { get; private set; } = null!;
    internal static ConfigEntry<float> MaxDistance { get; private set; } = null!;
    internal static ConfigEntry<bool> ShowOwnWaypoint { get; private set; } = null!;
    internal static ConfigEntry<float> HudScale { get; private set; } = null!;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Enabled = Config.Bind("General", "Enabled", true, "Enable shared waypoints.");
        DropKey = Config.Bind(
            "General",
            "DropKey",
            KeyCode.F8,
            "Place or replace your waypoint at your current position.");
        ClearKey = Config.Bind(
            "General",
            "ClearKey",
            KeyCode.F7,
            "Remove your waypoint for everyone.");
        MaxDistance = Config.Bind(
            "General",
            "MaxDistance",
            500f,
            "Hide waypoint markers beyond this distance (meters).");
        ShowOwnWaypoint = Config.Bind(
            "General",
            "ShowOwnWaypoint",
            true,
            "Show your own waypoint on the HUD.");
        HudScale = Config.Bind(
            "General",
            "HudScale",
            1.0f,
            "Scale of the fixed waypoint HUD panel.");

        try
        {
            new Harmony(ModGuid).PatchAll(typeof(Plugin).Assembly);
            WaypointHud.EnsureExists();
            Log.LogInfo($"{ModName} v{ModVersion} loaded. Drop={DropKey.Value} Clear={ClearKey.Value}.");
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to start: {ex}");
        }
    }
}

internal static class PluginInfo
{
    public const string PLUGIN_GUID = Plugin.ModGuid;
    public const string PLUGIN_NAME = Plugin.ModName;
    public const string PLUGIN_VERSION = Plugin.ModVersion;
}
