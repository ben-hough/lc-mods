using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;

namespace KeepItemsOnTeleport;

/// <summary>
/// Host presence + config gate. Hello payload: enabled, KeepOnNormal, KeepOnInverse.
/// </summary>
internal static class HostModGate
{
    public const string MessageName = "MrGlim.KeepItemsOnTeleport";
    private const byte OpHostHello = 0;
    private const byte OpClientSyncRequest = 1;

    private static bool _registered;
    private static bool _clientConnectedHooked;
    private static bool _requestedSync;
    private static bool _clientHostEnabled;
    private static bool _clientKeepNormal = true;
    private static bool _clientKeepInverse = true;

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

    public static bool KeepOnNormal
    {
        get
        {
            EnsureRegistered();
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || nm.IsHost)
                return Plugin.KeepOnNormalTeleporter != null && Plugin.KeepOnNormalTeleporter.Value;
            return _clientKeepNormal;
        }
    }

    public static bool KeepOnInverse
    {
        get
        {
            EnsureRegistered();
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || nm.IsHost)
                return Plugin.KeepOnInverseTeleporter != null && Plugin.KeepOnInverseTeleporter.Value;
            return _clientKeepInverse;
        }
    }

    public static void Reset()
    {
        _registered = false;
        _clientConnectedHooked = false;
        _requestedSync = false;
        _clientHostEnabled = false;
        _clientKeepNormal = true;
        _clientKeepInverse = true;
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
            Plugin.Log.LogInfo("MrGlim.KeepItemsOnTeleport net handler registered.");
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
        bool keepN = Plugin.KeepOnNormalTeleporter != null && Plugin.KeepOnNormalTeleporter.Value;
        bool keepI = Plugin.KeepOnInverseTeleporter != null && Plugin.KeepOnInverseTeleporter.Value;

        var writer = new FastBufferWriter(32, Allocator.Temp);
        writer.WriteValueSafe(OpHostHello);
        writer.WriteValueSafe(enabled);
        writer.WriteValueSafe(keepN);
        writer.WriteValueSafe(keepI);
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
        reader.ReadValueSafe(out bool keepN);
        reader.ReadValueSafe(out bool keepI);
        if (nm != null && !nm.IsServer)
        {
            _clientHostEnabled = enabled;
            _clientKeepNormal = keepN;
            _clientKeepInverse = keepI;
            Plugin.Log.LogInfo($"Host hello: enabled={enabled} keepNormal={keepN} keepInverse={keepI}");
        }
    }
}

[HarmonyPatch(typeof(GameNetworkManager), "Disconnect")]
internal static class HostModGateDisconnectPatch
{
    public static void Prefix()
    {
        HostModGate.Reset();
        Plugin.Log.LogInfo("KeepItemsOnTeleport gate reset on disconnect.");
    }
}
