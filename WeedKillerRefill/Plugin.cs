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
    public const string ModVersion = "1.0.2";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<KeyCode> RefillKey { get; private set; } = null!;
    internal static ConfigEntry<float> EmptyThreshold { get; private set; } = null!;
    internal static ConfigEntry<bool> ShowControlTip { get; private set; } = null!;
    internal static ConfigEntry<bool> OnlyWhenEmpty { get; private set; } = null!;

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
            "Key to refill a held weed killer. Uses the Unity Input System (works in Lethal Company).");
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

        try
        {
            _harmony.PatchAll(typeof(Plugin).Assembly);
            Log.LogInfo("Harmony patches applied (control tip on GrabbableObject.SetControlTipsForItem).");
        }
        catch (Exception ex)
        {
            Log.LogWarning($"Harmony patch failed (refill keybind still works): {ex.Message}");
        }

        var go = new GameObject("WeedKillerRefill");
        DontDestroyOnLoad(go);
        go.hideFlags = HideFlags.HideAndDontSave;
        go.AddComponent<WeedKillerRefillBehaviour>();

        Log.LogInfo($"{ModName} v{ModVersion} loaded. Refill key: {RefillKey.Value}");
    }
}

internal static class PluginInfo
{
    public const string PLUGIN_GUID = Plugin.ModGuid;
    public const string PLUGIN_NAME = Plugin.ModName;
    public const string PLUGIN_VERSION = Plugin.ModVersion;
}
