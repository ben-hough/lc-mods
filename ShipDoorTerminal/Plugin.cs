using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System.Reflection;

namespace ShipDoorTerminal;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.ShipDoorTerminal";
    public const string ModName = "ShipDoorTerminal";
    public const string ModVersion = "1.0.1";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;

    private readonly Harmony _harmony = new(ModGuid);

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Enabled = Config.Bind(
            "General",
            "Enabled",
            true,
            "Enable terminal commands: door / doors (toggle), opendoor, closedoor.");

        _harmony.PatchAll(typeof(Plugin).Assembly);

        var parse = AccessTools.Method(typeof(Terminal), "ParsePlayerSentence");
        var submit = AccessTools.Method(typeof(Terminal), "OnSubmit");
        Log.LogInfo($"Patched Terminal.ParsePlayerSentence={parse != null}, OnSubmit={submit != null}");

        Log.LogInfo($"{ModName} v{ModVersion} loaded.");
    }
}

internal static class PluginInfo
{
    public const string PLUGIN_GUID = Plugin.ModGuid;
    public const string PLUGIN_NAME = Plugin.ModName;
    public const string PLUGIN_VERSION = Plugin.ModVersion;
}
