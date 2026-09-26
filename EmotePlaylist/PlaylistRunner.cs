using System;
using System.Collections.Generic;
using GameNetcodeStuff;
using HarmonyLib;
using TooManyEmotes;
using Unity.Netcode;
using UnityEngine;

namespace EmotePlaylist;

internal sealed class PlaylistRunner : MonoBehaviour
{
    private static PlaylistRunner? _instance;

    private ulong _followingOwner;
    private int _lastIndex = -1;
    private bool _running;
    private string _tip = "";
    private float _tipUntil;
    private bool _didShowListOnStart;
    private bool _hadLocalPlayer;

    internal static bool IsRunning => _instance != null && _instance._running;

    internal static bool TryGetStatus(out int index, out int remainSeconds, out int total)
    {
        index = 0;
        remainSeconds = 0;
        total = 0;
        if (_instance == null || !_instance._running)
            return false;
        if (!PlaylistRegistry.TryGet(_instance._followingOwner, out var state) || state.EmoteNames.Length == 0)
            return false;

        var now = ServerNow();
        index = state.IndexAt(now);
        total = state.EmoteNames.Length;
        remainSeconds = state.SecondsPerEmote - (int)((now - state.StartServerTime) % state.SecondsPerEmote);
        if (remainSeconds <= 0)
            remainSeconds = state.SecondsPerEmote;
        return true;
    }

    private void Awake()
    {
        _instance = this;
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value)
            return;

        PlaylistNet.EnsureRegistered();
        PlaylistHud.EnsureExists();

        var player = GameNetworkManager.Instance?.localPlayerController;
        if (player == null || player.isPlayerDead)
        {
            StopLocal(broadcast: _running);
            ClearStatusTip();
            _hadLocalPlayer = false;
            return;
        }

        // First valid local player this session → optional auto-show list + tip.
        if (!_hadLocalPlayer)
        {
            _hadLocalPlayer = true;
            MaybeShowListOnStart();
        }

        if (!player.isTypingChat && !player.inTerminalMenu)
        {
            if (InputUtil.WasPressedThisFrame(Plugin.PlaylistKey.Value))
                ToggleOwnPlaylist(player);

            if (InputUtil.WasPressedThisFrame(Plugin.ListKey.Value))
                PlaylistHud.Toggle();
        }

        if (!_running)
        {
            if (Time.unscaledTime < _tipUntil && !string.IsNullOrEmpty(_tip))
                ShowControlTip(_tip);
            return;
        }

        if (!PlaylistRegistry.TryGet(_followingOwner, out var state) || state.EmoteNames.Length == 0)
        {
            StopLocal(broadcast: false);
            ClearStatusTip();
            return;
        }

        var now = ServerNow();
        var index = state.IndexAt(now);
        if (index != _lastIndex)
        {
            PlayIndex(state, index);
            _lastIndex = index;
        }

        var remain = state.SecondsPerEmote - (int)((now - state.StartServerTime) % state.SecondsPerEmote);
        if (remain <= 0)
            remain = state.SecondsPerEmote;
        _tip = $"Playlist {index + 1}/{state.EmoteNames.Length}: {state.EmoteNames[index]} ({remain}s)  [{InputUtil.TipLabel(Plugin.PlaylistKey.Value)} stop]";
        ShowControlTip(_tip);
    }

    private void MaybeShowListOnStart()
    {
        if (_didShowListOnStart)
            return;
        if (Plugin.ShowListOnStart == null || !Plugin.ShowListOnStart.Value)
            return;

        _didShowListOnStart = true;
        PlaylistHud.Show();

        var listKey = InputUtil.TipLabel(Plugin.ListKey.Value);
        var startKey = InputUtil.TipLabel(Plugin.PlaylistKey.Value);
        try
        {
            HUDManager.Instance?.DisplayTip(
                "Emote Playlist",
                $"{listKey} list · {startKey} start after setting TME quick slots");
        }
        catch
        {
            // ignored
        }

        Plugin.Log.LogInfo("ShowListOnStart: playlist HUD shown + tip.");
    }

    internal void Join(ulong ownerPlayerId)
    {
        if (!PlaylistRegistry.TryGet(ownerPlayerId, out var state) || state.EmoteNames.Length == 0)
            return;

        _followingOwner = ownerPlayerId;
        _running = true;
        _lastIndex = -1;
        var now = ServerNow();
        var index = state.IndexAt(now);
        PlayIndex(state, index);
        _lastIndex = index;
        FlashTip($"Joined playlist ({state.EmoteNames.Length} emotes)", 3f);
        Plugin.Log.LogInfo($"Joined playlist owner={ownerPlayerId} index={index} duration={state.SecondsPerEmote}s");
    }

    private void ToggleOwnPlaylist(PlayerControllerB player)
    {
        Plugin.Log.LogInfo($"Playlist key pressed ({Plugin.PlaylistKey.Value}).");

        if (_running && _followingOwner == player.playerClientId)
        {
            StopLocal(broadcast: true);
            FlashTip("Playlist stopped", 2.5f);
            return;
        }

        var names = BuildPlaylist();
        if (names.Count == 0)
        {
            Plugin.Log.LogWarning("Playlist empty — assign TooManyEmotes quick slots (wheel remotes 1–8) first.");
            FlashTip("Playlist empty — set TME quick slots 1–8 first", 4f);
            try
            {
                var startKey = InputUtil.TipLabel(Plugin.PlaylistKey.Value);
                HUDManager.Instance?.DisplayTip(
                    "Emote Playlist",
                    $"Assign TooManyEmotes quick slots (wheel remotes), then press {startKey}.");
            }
            catch
            {
                // ignored
            }
            return;
        }

        var state = new PlaylistState
        {
            OwnerPlayerId = player.playerClientId,
            StartServerTime = ServerNow(),
            SecondsPerEmote = Plugin.ClampedSeconds(),
            EmoteNames = names.ToArray()
        };
        PlaylistRegistry.Set(state);
        PlaylistNet.BroadcastStart(state);
        _followingOwner = player.playerClientId;
        _running = true;
        _lastIndex = -1;
        FlashTip($"Playlist started ({names.Count} emotes)", 2.5f);
        Plugin.Log.LogInfo($"Started playlist ({names.Count} emotes, {state.SecondsPerEmote}s each).");
    }

    private void StopLocal(bool broadcast)
    {
        if (!_running)
            return;
        var owner = _followingOwner;
        _running = false;
        _lastIndex = -1;
        if (broadcast)
        {
            PlaylistRegistry.Clear(owner);
            PlaylistNet.BroadcastStop(owner);
            Plugin.Log.LogInfo("Stopped playlist.");
        }
    }

    private void FlashTip(string text, float seconds)
    {
        _tip = text;
        _tipUntil = Time.unscaledTime + seconds;
        ShowControlTip(text);
    }

    private void ClearStatusTip()
    {
        _tip = "";
        _tipUntil = 0f;
    }

    private static void ShowControlTip(string text)
    {
        try
        {
            var hud = HUDManager.Instance;
            if (hud == null)
                return;
            hud.ChangeControlTip(3, text);
        }
        catch
        {
            // ignored
        }
    }

    private static List<string> BuildPlaylist()
    {
        var names = new List<string>();
        var over = Plugin.PlaylistOverride.Value;
        if (!string.IsNullOrWhiteSpace(over))
        {
            foreach (var part in over.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var n = part.Trim();
                if (n.Length > 0)
                    names.Add(n);
            }
            return names;
        }

        for (var i = 0; i < 8; i++)
        {
            try
            {
                var emote = QuickEmotes.GetQuickEmote(i);
                if (emote != null && !string.IsNullOrEmpty(emote.emoteName))
                    names.Add(emote.emoteName);
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"Quick slot {i} failed: {ex.Message}");
            }
        }
        return names;
    }

    private static void PlayIndex(PlaylistState state, int index)
    {
        var local = EmoteControllerPlayer.emoteControllerLocal;
        if (local == null || index < 0 || index >= state.EmoteNames.Length)
            return;

        var name = state.EmoteNames[index];
        if (!EmotesManager.allUnlockableEmotesDict.TryGetValue(name, out var emote) || emote == null)
        {
            Plugin.Log.LogWarning($"Playlist emote not found: {name}");
            return;
        }

        try
        {
            local.TryPerformingEmoteLocal(emote);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to play {name}: {ex.Message}");
        }
    }

    private static double ServerNow()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
            return Time.realtimeSinceStartupAsDouble;
        return nm.ServerTime.Time;
    }
}

[HarmonyPatch(typeof(TooManyEmotes.Patches.SyncWithEmoteControllerManager), "SyncWithEmoteController_performed")]
internal static class SyncPlaylistPatch
{
    [HarmonyPrefix]
    [HarmonyPriority(Priority.First)]
    private static bool Prefix(PlayerControllerB __instance)
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value)
            return true;

        var looking = TooManyEmotes.Patches.SyncWithEmoteControllerManager.lookingAtSyncableEmoteController;
        if (looking is not EmoteControllerPlayer target || target.playerController == null)
            return true;

        var owner = target.playerController.playerClientId;
        if (!PlaylistRegistry.TryGet(owner, out _))
            return true;

        var runner = Plugin.Instance != null ? Plugin.Instance.GetComponent<PlaylistRunner>() : null;
        runner?.Join(owner);
        TooManyEmotes.Patches.SyncWithEmoteControllerManager.ResetState();
        return false;
    }
}
