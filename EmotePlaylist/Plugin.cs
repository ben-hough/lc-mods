using System;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace EmotePlaylist;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency("FlipMods.TooManyEmotes")]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.EmotePlaylist";
    public const string ModName = "EmotePlaylist";
    public const string ModVersion = "1.0.2";

    internal static Plugin Instance { get; private set; } = null!;
    internal static ManualLogSource Log { get; private set; } = null!;

    internal static ConfigEntry<bool> Enabled { get; private set; } = null!;
    internal static ConfigEntry<KeyCode> PlaylistKey { get; private set; } = null!;
    internal static ConfigEntry<KeyCode> ListKey { get; private set; } = null!;
    internal static ConfigEntry<bool> ShowListOnStart { get; private set; } = null!;
    internal static ConfigEntry<int> SecondsPerEmote { get; private set; } = null!;
    internal static ConfigEntry<string> PlaylistOverride { get; private set; } = null!;

    private void Awake()
    {
        Instance = this;
        Log = Logger;

        Enabled = Config.Bind("General", "Enabled", true, "Enable emote playlists.");
        PlaylistKey = Config.Bind(
            "General",
            "PlaylistKey",
            KeyCode.F9,
            "Start/stop a looping playlist of your 8 TooManyEmotes quick slots (the wheel remotes).");
        ListKey = Config.Bind(
            "General",
            "ListKey",
            KeyCode.F10,
            "Toggle the on-screen emote playlist panel.");
        ShowListOnStart = Config.Bind(
            "General",
            "ShowListOnStart",
            true,
            "Auto-show the playlist panel (and a tip) when your local player first becomes available this session.");
        SecondsPerEmote = Config.Bind(
            "General",
            "SecondsPerEmote",
            120,
            "How long each playlist emote plays (clamped 60–300).");
        PlaylistOverride = Config.Bind(
            "General",
            "PlaylistOverride",
            "",
            "Optional comma-separated emote names. Empty = use current quick slots 1–8.");

        try
        {
            new Harmony(ModGuid).PatchAll(typeof(Plugin).Assembly);
            gameObject.AddComponent<PlaylistRunner>();
            PlaylistHud.EnsureExists();
            Log.LogInfo($"{ModName} v{ModVersion} loaded.");
        }
        catch (Exception ex)
        {
            Log.LogError($"Failed to start: {ex}");
        }
    }

    internal static int ClampedSeconds()
    {
        var v = SecondsPerEmote?.Value ?? 120;
        return Mathf.Clamp(v, 60, 300);
    }
}

internal static class PluginInfo
{
    public const string PLUGIN_GUID = Plugin.ModGuid;
    public const string PLUGIN_NAME = Plugin.ModName;
    public const string PLUGIN_VERSION = Plugin.ModVersion;
}
