using BepInEx;
using BepInEx.Logging;
using HarmonyLib;

namespace CadaverWilt;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.CadaverWilt";
    public const string ModName = "CadaverWilt";
    public const string ModVersion = "1.0.2";

    internal static ManualLogSource Log { get; private set; } = null!;

    private void Awake()
    {
        Log = Logger;
        new Harmony(ModGuid).PatchAll(typeof(Plugin).Assembly);
        Log.LogInfo($"{ModName} v{ModVersion} loaded.");
    }
}

internal static class PluginInfo
{
    public const string PLUGIN_GUID = Plugin.ModGuid;
    public const string PLUGIN_NAME = Plugin.ModName;
    public const string PLUGIN_VERSION = Plugin.ModVersion;
}
