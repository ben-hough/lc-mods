using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace WeedKillerRefill;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.WeedKillerRefill";
    public const string ModName = "WeedKillerRefill";
    public const string ModVersion = "1.0.7";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<KeyCode> RefillKey { get; private set; } = null!;
    internal static ConfigEntry<float> EmptyThreshold { get; private set; } = null!;
    internal static ConfigEntry<bool> ShowControlTip { get; private set; } = null!;
    internal static ConfigEntry<bool> OnlyWhenEmpty { get; private set; } = null!;
    internal static ConfigEntry<bool> Verbose { get; private set; } = null!;
    internal static ConfigEntry<bool> AllowKeyRefill { get; private set; } = null!;

    private readonly Harmony _harmony = new(ModGuid);

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Enabled = Config.Bind("General", "Enabled", true, "Allow refilling weed killer with the configured key.");
        RefillKey = Config.Bind(
            "General",
            "RefillKey",
            KeyCode.R,
            "Key to refill a held weed killer.");
        EmptyThreshold = Config.Bind(
            "General",
            "EmptyThreshold",
            0.05f,
            "Tank/battery charge at or below this counts as empty (0–1).");
        OnlyWhenEmpty = Config.Bind(
            "General",
            "OnlyWhenEmpty",
            true,
            "If true, refill only works when empty. If false, always tops off on key press.");
        ShowControlTip = Config.Bind(
            "General",
            "ShowControlTip",
            true,
            "Show a vanilla top-right control tip (Refill : [R]) when holding an empty weed killer.");
        Verbose = Config.Bind(
            "General",
            "VerboseLogging",
            true,
            "Log holding/key/refill traces.");
        AllowKeyRefill = Config.Bind(
            "General",
            "AllowKeyRefill",
            true,
            "Allow refill with RefillKey (default R).");

        try
        {
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo("Harmony patches applied (R key refill + ChargeBatteries sync).");
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Harmony patch failed: {ex.Message}");
        }

        WeedKillerRefillBehaviour.EnsureExists();

        Log.LogInfo($"{ModName} v{ModVersion} loaded. Press {RefillKey.Value} to refill.");
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
