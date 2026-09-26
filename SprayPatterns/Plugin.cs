using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace SprayPatterns;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.SprayPatterns";
    public const string ModName = "SprayPatterns";
    public const string ModVersion = "0.1.0";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<bool> VerboseLogging { get; private set; } = null!;
    internal static ConfigEntry<KeyCode> RadialKey { get; private set; } = null!;
    internal static ConfigEntry<KeyCode> RotateLeftKey { get; private set; } = null!;
    internal static ConfigEntry<KeyCode> RotateRightKey { get; private set; } = null!;
    internal static ConfigEntry<bool> InvertScroll { get; private set; } = null!;
    internal static ConfigEntry<float> RotateStepDegrees { get; private set; } = null!;
    internal static ConfigEntry<float> PreviewOpacity { get; private set; } = null!;
    internal static ConfigEntry<float> StampSize { get; private set; } = null!;
    internal static ConfigEntry<float> StampTankCost { get; private set; } = null!;
    internal static ConfigEntry<float> StampShakeCost { get; private set; } = null!;
    internal static ConfigEntry<float> StampCooldown { get; private set; } = null!;
    internal static ConfigEntry<int> MaxStamps { get; private set; } = null!;

    internal static ConfigEntry<bool> EnableArrow { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableArrowMain { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableArrowFire { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableDry { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableJuicy { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableSkull { get; private set; } = null!;
    internal static ConfigEntry<bool> EnableSunshine { get; private set; } = null!;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Enabled = Config.Bind("General", "Enabled", true, "Master toggle for Overwatch-style spray patterns.");
        VerboseLogging = Config.Bind("General", "VerboseLogging", false, "Extra diagnostics for stamps / radial / net.");

        RadialKey = Config.Bind(
            "Controls",
            "RadialKey",
            KeyCode.V,
            "Toggle the spray pattern radial selector (while holding a spray paint can).");
        RotateLeftKey = Config.Bind(
            "Controls",
            "RotateLeftKey",
            KeyCode.LeftBracket,
            "Rotate selected pattern stamp counter-clockwise. (Q is reserved for vanilla shake.)");
        RotateRightKey = Config.Bind(
            "Controls",
            "RotateRightKey",
            KeyCode.RightBracket,
            "Rotate selected pattern stamp clockwise.");
        InvertScroll = Config.Bind(
            "Controls",
            "InvertScroll",
            false,
            "Invert mouse-wheel rotation direction.");
        RotateStepDegrees = Config.Bind(
            "Controls",
            "RotateStepDegrees",
            15f,
            new ConfigDescription("Degrees per key/scroll step.", new AcceptableValueRange<float>(1f, 90f)));

        PreviewOpacity = Config.Bind(
            "Visual",
            "PreviewOpacity",
            0.45f,
            new ConfigDescription("Ghost preview opacity (0–1).", new AcceptableValueRange<float>(0.05f, 1f)));
        StampSize = Config.Bind(
            "Visual",
            "StampSize",
            0.85f,
            new ConfigDescription("World size of pattern stamps (meters).", new AcceptableValueRange<float>(0.25f, 2.5f)));
        MaxStamps = Config.Bind(
            "Visual",
            "MaxStamps",
            200,
            new ConfigDescription("Max pooled pattern stamp objects before recycle.", new AcceptableValueRange<int>(32, 1000)));

        StampTankCost = Config.Bind(
            "Spray",
            "StampTankCost",
            0.04f,
            new ConfigDescription("Spray can tank consumed per pattern stamp.", new AcceptableValueRange<float>(0f, 0.5f)));
        StampShakeCost = Config.Bind(
            "Spray",
            "StampShakeCost",
            0.12f,
            new ConfigDescription("Shake meter consumed per pattern stamp.", new AcceptableValueRange<float>(0f, 1f)));
        StampCooldown = Config.Bind(
            "Spray",
            "StampCooldown",
            0.35f,
            new ConfigDescription("Minimum seconds between pattern stamps.", new AcceptableValueRange<float>(0.05f, 2f)));

        EnableArrow = Config.Bind("Patterns", "EnableArrow", true, "Arrow (pointing).");
        EnableArrowMain = Config.Bind("Patterns", "EnableArrowMain", true, "Arrow with MAIN text.");
        EnableArrowFire = Config.Bind("Patterns", "EnableArrowFire", true, "Arrow with FIRE text.");
        EnableDry = Config.Bind("Patterns", "EnableDry", true, "Dry / looted marker.");
        EnableJuicy = Config.Bind("Patterns", "EnableJuicy", true, "Juicy / worth checking marker.");
        EnableSkull = Config.Bind("Patterns", "EnableSkull", true, "Skull hazard marker.");
        EnableSunshine = Config.Bind("Patterns", "EnableSunshine", true, "Sunshine / clear marker.");

        try
        {
            PatternTextures.EnsureGenerated();
            new Harmony(ModGuid).PatchAll(typeof(Plugin).Assembly);
            SprayPatternController.EnsureExists();
            Log.LogInfo($"{ModName} v{ModVersion} loaded. Radial={RadialKey.Value} Rotate=[{RotateLeftKey.Value}/{RotateRightKey.Value}] + scroll.");
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to start: {ex}");
        }
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
