using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace EmotePlaylist;

internal static class PlaylistNet
{
    public const string MessageName = "MrGlim.EmotePlaylist";
    private static bool _registered;

    public static void EnsureRegistered()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || _registered)
            return;
        nm.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
        _registered = true;
    }

    public static void BroadcastStart(PlaylistState state)
    {
        EnsureRegistered();
        var nm = NetworkManager.Singleton;
        if (nm == null)
            return;

        using var writer = WriteState(1, state);
        if (nm.IsServer)
            nm.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer, NetworkDelivery.Reliable);
        else
            nm.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
    }

    public static void BroadcastStop(ulong ownerPlayerId)
    {
        EnsureRegistered();
        var nm = NetworkManager.Singleton;
        if (nm == null)
            return;

        var writer = new FastBufferWriter(16, Allocator.Temp);
        writer.WriteValueSafe((byte)0);
        writer.WriteValueSafe(ownerPlayerId);
        if (nm.IsServer)
            nm.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer, NetworkDelivery.Reliable);
        else
            nm.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
        writer.Dispose();
    }

    private static FastBufferWriter WriteState(byte op, PlaylistState state)
    {
        var names = string.Join("|", state.EmoteNames);
        var bytes = Encoding.UTF8.GetByteCount(names);
        var writer = new FastBufferWriter(64 + bytes, Allocator.Temp);
        writer.WriteValueSafe(op);
        writer.WriteValueSafe(state.OwnerPlayerId);
        writer.WriteValueSafe(state.StartServerTime);
        writer.WriteValueSafe(state.SecondsPerEmote);
        writer.WriteValueSafe(names);
        return writer;
    }

    private static void OnMessage(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte op);
        reader.ReadValueSafe(out ulong owner);
        var nm = NetworkManager.Singleton;

        if (op == 0)
        {
            PlaylistRegistry.Clear(owner);
            if (nm != null && nm.IsServer)
            {
                var stop = new FastBufferWriter(16, Allocator.Temp);
                stop.WriteValueSafe((byte)0);
                stop.WriteValueSafe(owner);
                nm.CustomMessagingManager.SendNamedMessageToAll(MessageName, stop, NetworkDelivery.Reliable);
                stop.Dispose();
            }
            return;
        }

        reader.ReadValueSafe(out double start);
        reader.ReadValueSafe(out int seconds);
        reader.ReadValueSafe(out string names);
        seconds = Mathf.Clamp(seconds, 60, 300);
        var list = string.IsNullOrEmpty(names) ? System.Array.Empty<string>() : names.Split('|');
        var state = new PlaylistState
        {
            OwnerPlayerId = owner,
            StartServerTime = start,
            SecondsPerEmote = seconds,
            EmoteNames = list
        };
        PlaylistRegistry.Set(state);

        if (nm != null && nm.IsServer && sender != NetworkManager.ServerClientId)
        {
            using var echo = WriteState(1, state);
            nm.CustomMessagingManager.SendNamedMessageToAll(MessageName, echo, NetworkDelivery.Reliable);
        }
    }
}
