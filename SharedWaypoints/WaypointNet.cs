using System.Text;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SharedWaypoints;

/// <summary>
/// Client → server → all reliable named messages for waypoint set/clear/sync.
/// Ops: 0=clear, 1=set, 2=sync-request (client→server), 3=sync-begin (server clears remote view before dump — unused),
/// server simply echoes set/clear and on connect / op2 dumps all known waypoints as op1.
/// </summary>
internal static class WaypointNet
{
    public const string MessageName = "MrGlim.SharedWaypoints";
    private static bool _registered;
    private static bool _clientConnectedHooked;
    private static bool _requestedSync;

    public static void EnsureRegistered()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            _registered = false;
            _clientConnectedHooked = false;
            _requestedSync = false;
            return;
        }

        if (!_registered)
        {
            nm.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
            _registered = true;
            Plugin.Log.LogInfo("SharedWaypoints net handler registered.");
        }

        if (nm.IsServer && !_clientConnectedHooked)
        {
            nm.OnClientConnectedCallback += OnClientConnected;
            _clientConnectedHooked = true;
        }

        // Late joiner: ask server for current waypoints once.
        if (!nm.IsServer && nm.IsConnectedClient && !_requestedSync)
        {
            _requestedSync = true;
            RequestSync();
        }
    }

    public static void BroadcastSet(WaypointState state)
    {
        EnsureRegistered();
        var nm = NetworkManager.Singleton;
        if (nm == null)
            return;

        using var writer = WriteSet(state);
        if (nm.IsServer)
            nm.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer, NetworkDelivery.Reliable);
        else
            nm.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
    }

    public static void BroadcastClear(ulong ownerPlayerClientId, bool isInside)
    {
        EnsureRegistered();
        var nm = NetworkManager.Singleton;
        if (nm == null)
            return;

        // op + owner + isInside
        var writer = new FastBufferWriter(24, Allocator.Temp);
        writer.WriteValueSafe((byte)0);
        writer.WriteValueSafe(ownerPlayerClientId);
        writer.WriteValueSafe(isInside);
        if (nm.IsServer)
            nm.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer, NetworkDelivery.Reliable);
        else
            nm.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
        writer.Dispose();
    }

    private static void RequestSync()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.IsServer)
            return;

        // byte op + ulong pad = 9 bytes; keep headroom like BroadcastClear
        var writer = new FastBufferWriter(16, Allocator.Temp);
        writer.WriteValueSafe((byte)2);
        writer.WriteValueSafe(0UL);
        nm.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
        writer.Dispose();
        Plugin.Log.LogInfo("Requested waypoint sync from server.");
    }

    private static void OnClientConnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer)
            return;

        // Host already has local registry; skip self.
        if (clientId == nm.LocalClientId)
            return;

        SendAllToClient(clientId);
    }

    private static void SendAllToClient(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer)
            return;

        foreach (var wp in WaypointRegistry.Snapshot())
        {
            using var writer = WriteSet(wp);
            nm.CustomMessagingManager.SendNamedMessage(MessageName, clientId, writer, NetworkDelivery.Reliable);
        }

        Plugin.Log.LogInfo($"Resynced {WaypointRegistry.Count} waypoint(s) to client {clientId}.");
    }

    private static FastBufferWriter WriteSet(WaypointState state)
    {
        var nameBytes = Encoding.UTF8.GetByteCount(state.OwnerName ?? "");
        var writer = new FastBufferWriter(48 + nameBytes, Allocator.Temp);
        writer.WriteValueSafe((byte)1);
        writer.WriteValueSafe(state.OwnerPlayerClientId);
        writer.WriteValueSafe(state.OwnerName ?? "");
        writer.WriteValueSafe(state.Position.x);
        writer.WriteValueSafe(state.Position.y);
        writer.WriteValueSafe(state.Position.z);
        writer.WriteValueSafe(state.IsInside);
        writer.WriteValueSafe(state.ColorIndex);
        return writer;
    }

    private static void OnMessage(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte op);
        var nm = NetworkManager.Singleton;

        if (op == 2)
        {
            // Sync request from a client.
            if (nm != null && nm.IsServer)
                SendAllToClient(sender);
            return;
        }

        reader.ReadValueSafe(out ulong owner);

        if (op == 0)
        {
            reader.ReadValueSafe(out bool clearInside);
            WaypointRegistry.Clear(owner, clearInside);
            Plugin.Log.LogInfo($"Waypoint cleared for owner={owner} inside={clearInside}.");

            if (nm != null && nm.IsServer)
            {
                var stop = new FastBufferWriter(24, Allocator.Temp);
                stop.WriteValueSafe((byte)0);
                stop.WriteValueSafe(owner);
                stop.WriteValueSafe(clearInside);
                nm.CustomMessagingManager.SendNamedMessageToAll(MessageName, stop, NetworkDelivery.Reliable);
                stop.Dispose();
            }
            return;
        }

        if (op != 1)
            return;

        reader.ReadValueSafe(out string name);
        reader.ReadValueSafe(out float x);
        reader.ReadValueSafe(out float y);
        reader.ReadValueSafe(out float z);
        reader.ReadValueSafe(out bool isInside);
        reader.ReadValueSafe(out int colorIndex);

        var state = new WaypointState
        {
            OwnerPlayerClientId = owner,
            OwnerName = name ?? "",
            Position = new Vector3(x, y, z),
            IsInside = isInside,
            ColorIndex = colorIndex
        };
        WaypointRegistry.Set(state);
        Plugin.Log.LogInfo(
            $"Waypoint set owner={owner} name='{name}' inside={isInside} pos=({x:0.0},{y:0.0},{z:0.0}).");

        if (nm != null && nm.IsServer && sender != NetworkManager.ServerClientId)
        {
            using var echo = WriteSet(state);
            nm.CustomMessagingManager.SendNamedMessageToAll(MessageName, echo, NetworkDelivery.Reliable);
        }
    }
}
