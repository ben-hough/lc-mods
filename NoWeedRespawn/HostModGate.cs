using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;

namespace NoWeedRespawn;

/// <summary>
/// Host gate + OutdoorCleared sync. Hello: enabled, blockIndoor, blockOutdoor, outdoorCleared.
/// </summary>
internal static class HostModGate
{
    public const string MessageName = "MrGlim.NoWeedRespawn";
    private const byte OpHostHello = 0;
    private const byte OpClientSyncRequest = 1;

    private static bool _registered;
    private static bool _clientConnectedHooked;
    private static bool _requestedSync;
    private static bool _clientHostEnabled;
    private static bool _clientBlockIndoor = true;
    private static bool _clientBlockOutdoor = true;
    private static bool _clientOutdoorCleared;

    public static bool HostHasMod
    {
        get
        {
            EnsureRegistered();
            var nm = NetworkManager.Singleton;
            if (nm == null)
                return true;
            if (nm.IsServer || nm.IsHost)
                return Plugin.Enabled != null && Plugin.Enabled.Value;
            return _clientHostEnabled;
        }
    }

    public static bool FeaturesActive => HostHasMod;

    public static bool IndoorActive
    {
        get
        {
            if (!FeaturesActive)
                return false;
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || nm.IsHost)
                return Plugin.BlockIndoorRespawn != null && Plugin.BlockIndoorRespawn.Value;
            return _clientBlockIndoor;
        }
    }

    public static bool OutdoorActive
    {
        get
        {
            if (!FeaturesActive)
                return false;
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || nm.IsHost)
                return Plugin.BlockOutdoorRespawn != null && Plugin.BlockOutdoorRespawn.Value;
            return _clientBlockOutdoor;
        }
    }

    public static bool OutdoorClearedSynced
    {
        get
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || nm.IsHost)
                return OutdoorSession.OutdoorCleared;
            return _clientOutdoorCleared;
        }
    }

    public static void OnSessionReset()
    {
        _clientOutdoorCleared = false;
    }

    public static void Reset()
    {
        _registered = false;
        _clientConnectedHooked = false;
        _requestedSync = false;
        _clientHostEnabled = false;
        _clientBlockIndoor = true;
        _clientBlockOutdoor = true;
        _clientOutdoorCleared = false;
    }

    public static void EnsureRegistered()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            _registered = false;
            _clientConnectedHooked = false;
            return;
        }

        if (!_registered)
        {
            nm.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
            _registered = true;
            Plugin.Log.LogInfo("MrGlim.NoWeedRespawn net handler registered.");
        }

        if (nm.IsServer && !_clientConnectedHooked)
        {
            nm.OnClientConnectedCallback += OnClientConnected;
            _clientConnectedHooked = true;
        }

        if (!nm.IsServer && nm.IsConnectedClient && !_requestedSync)
        {
            _requestedSync = true;
            RequestSync();
        }
    }

    /// <summary>Host: push OutdoorCleared (and flags) to all clients.</summary>
    public static void BroadcastState()
    {
        EnsureRegistered();
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer)
            return;

        bool enabled = Plugin.Enabled != null && Plugin.Enabled.Value;
        bool indoor = Plugin.BlockIndoorRespawn != null && Plugin.BlockIndoorRespawn.Value;
        bool outdoor = Plugin.BlockOutdoorRespawn != null && Plugin.BlockOutdoorRespawn.Value;
        bool cleared = OutdoorSession.OutdoorCleared;

        var writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe(OpHostHello);
        writer.WriteValueSafe(enabled);
        writer.WriteValueSafe(indoor);
        writer.WriteValueSafe(outdoor);
        writer.WriteValueSafe(cleared);
        nm.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer, NetworkDelivery.Reliable);
        writer.Dispose();
    }

    private static void OnClientConnected(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer)
            return;
        if (clientId == nm.LocalClientId)
            return;
        SendHello(clientId);
    }

    private static void RequestSync()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.IsServer)
            return;

        var writer = new FastBufferWriter(16, Allocator.Temp);
        writer.WriteValueSafe(OpClientSyncRequest);
        nm.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
        writer.Dispose();
    }

    private static void SendHello(ulong clientId)
    {
        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer)
            return;

        bool enabled = Plugin.Enabled != null && Plugin.Enabled.Value;
        bool indoor = Plugin.BlockIndoorRespawn != null && Plugin.BlockIndoorRespawn.Value;
        bool outdoor = Plugin.BlockOutdoorRespawn != null && Plugin.BlockOutdoorRespawn.Value;
        bool cleared = OutdoorSession.OutdoorCleared;

        var writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe(OpHostHello);
        writer.WriteValueSafe(enabled);
        writer.WriteValueSafe(indoor);
        writer.WriteValueSafe(outdoor);
        writer.WriteValueSafe(cleared);
        nm.CustomMessagingManager.SendNamedMessage(MessageName, clientId, writer, NetworkDelivery.Reliable);
        writer.Dispose();
    }

    private static void OnMessage(ulong sender, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte op);
        var nm = NetworkManager.Singleton;

        if (op == OpClientSyncRequest)
        {
            if (nm != null && nm.IsServer)
                SendHello(sender);
            return;
        }

        if (op != OpHostHello)
            return;

        reader.ReadValueSafe(out bool enabled);
        reader.ReadValueSafe(out bool indoor);
        reader.ReadValueSafe(out bool outdoor);
        reader.ReadValueSafe(out bool cleared);
        if (nm != null && !nm.IsServer)
        {
            _clientHostEnabled = enabled;
            _clientBlockIndoor = indoor;
            _clientBlockOutdoor = outdoor;
            _clientOutdoorCleared = cleared;
            if (cleared)
                OutdoorSession.OutdoorCleared = true;
            Plugin.Log.LogInfo($"Host hello: enabled={enabled} indoor={indoor} outdoor={outdoor} cleared={cleared}");
        }
    }
}

[HarmonyPatch(typeof(GameNetworkManager), "Disconnect")]
internal static class HostModGateDisconnectPatch
{
    public static void Prefix()
    {
        HostModGate.Reset();
        Plugin.Log.LogInfo("NoWeedRespawn gate reset on disconnect.");
    }
}
