using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;

namespace MapClickCodes;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.MapClickCodes";
    public const string ModName = "MapClickCodes";
    public const string ModVersion = "1.0.11";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<bool> VerboseLogging { get; private set; } = null!;
    internal static ConfigEntry<float> HitRadius { get; private set; } = null!;
    internal static ConfigEntry<bool> FlipUvV { get; private set; } = null!;
    internal static ConfigEntry<float> InteractRange { get; private set; } = null!;
    internal static ConfigEntry<bool> ShowHoverTip { get; private set; } = null!;

    /// <summary>Set briefly when a code marker click was handled (dedupe InteractTrigger). Radar switch is never suppressed (1.0.7).</summary>
    internal static bool ClickConsumedThisFrame { get; set; }

    private readonly Harmony _harmony = new(ModGuid);
    private float _consumeClearTimer;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Enabled = Config.Bind(
            "General",
            "Enabled",
            true,
            "Master toggle: click code markers on the main ship radar to activate terminal codes.");
        VerboseLogging = Config.Bind(
            "General",
            "VerboseLogging",
            false,
            "Log UV hits, matched codes, and misses.");
        HitRadius = Config.Bind(
            "General",
            "HitRadius",
            0.012f,
            new ConfigDescription(
                "Extra UV / viewport padding around each code marker RectTransform (small pad only; no minHalf inflate).",
                new AcceptableValueRange<float>(0f, 0.08f)));
        FlipUvV = Config.Bind(
            "General",
            "FlipUvV",
            true,
            "Flip V when converting mesh UV to mapCamera viewport. Try toggling if markers feel vertically inverted.");
        InteractRange = Config.Bind(
            "General",
            "InteractRange",
            4.5f,
            new ConfigDescription(
                "Max raycast distance from the player camera to the main map screen mesh.",
                new AcceptableValueRange<float>(1.5f, 8f)));
        ShowHoverTip = Config.Bind(
            "General",
            "ShowHoverTip",
            true,
            "Show cursor icon + tip (Activate/Open CODE : [E]) when aimed at a map code marker.");

        _harmony.PatchAll(typeof(Plugin).Assembly);
        Log.LogInfo($"{ModName} v{ModVersion} loaded.");
    }

    private void Update()
    {
        HostModGate.Tick();

        if (ClickConsumedThisFrame)
        {
            _consumeClearTimer += UnityEngine.Time.unscaledDeltaTime;
            // Brief window so InteractTrigger does not double-activate; radar is never suppressed.
            if (_consumeClearTimer >= 0.12f)
            {
                ClickConsumedThisFrame = false;
                _consumeClearTimer = 0f;
            }
        }
        else
        {
            _consumeClearTimer = 0f;
        }

        MapCodeClickController.Tick();
    }

    internal static void V(string msg)
    {
        if (VerboseLogging != null && VerboseLogging.Value)
            Log.LogInfo(msg);
    }
}

internal static class PluginInfo
{
    public const string PLUGIN_GUID = Plugin.ModGuid;
    public const string PLUGIN_NAME = Plugin.ModName;
    public const string PLUGIN_VERSION = Plugin.ModVersion;
}
