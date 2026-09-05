using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using UnityEngine;

namespace WeedKillerRefill;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.WeedKillerRefill";
    public const string ModName = "WeedKillerRefill";
    public const string ModVersion = "1.0.0";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<KeyCode> RefillKey { get; private set; } = null!;
    internal static ConfigEntry<float> EmptyThreshold { get; private set; } = null!;
    internal static ConfigEntry<bool> ShowHint { get; private set; } = null!;
    internal static ConfigEntry<bool> OnlyWhenEmpty { get; private set; } = null!;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Enabled = Config.Bind("General", "Enabled", true, "Allow refilling weed killer with the configured key.");
        // Q is usually Drop in Lethal Company; R matches the game's ReloadBatteries / reload metaphor.
        RefillKey = Config.Bind(
            "General",
            "RefillKey",
            KeyCode.R,
            "Key to refill a held weed killer. Default R (reload). Q is typically Drop — change here if you want.");
        EmptyThreshold = Config.Bind(
            "General",
            "EmptyThreshold",
            0.02f,
            "Tank charge at or below this counts as empty (0–1).");
        OnlyWhenEmpty = Config.Bind(
            "General",
            "OnlyWhenEmpty",
            true,
            "If true, refill only works when the tank is empty. If false, always tops off on key press.");
        ShowHint = Config.Bind(
            "General",
            "ShowHint",
            true,
            "Show an on-screen hint when holding an empty weed killer.");

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
