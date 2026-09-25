using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;

namespace NoMineshaft;

/// <summary>
/// Host presence gate via Unity Netcode named messages.
/// Advantage features stay off on pure clients until host-hello (enabled=true).
/// </summary>
internal static class HostModGate
{
    public const string MessageName = "MrGlim.NoMineshaft";
    private const byte OpHostHello = 0;
    private const byte OpClientSyncRequest = 1;

    private static bool _registered;
    private static bool _clientConnectedHooked;
    private static bool _requestedSync;
    private static bool _clientHostEnabled;

    public static bool HostHasMod
    {
        get
        {
            EnsureRegistered();
            var nm = NetworkManager.Singleton;
            if (nm == null)
                return true;
            if (nm.IsServer || nm.IsHost)
                return LocalEnabled();
            return _clientHostEnabled;
        }
    }

    public static bool FeaturesActive => HostHasMod;

    private static bool LocalEnabled()
    {
        return Plugin.Enabled != null && Plugin.Enabled.Value;
    }

    public static void Reset()
    {
        _registered = false;
        _clientConnectedHooked = false;
        _requestedSync = false;
        _clientHostEnabled = false;
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
            Plugin.Log.LogInfo("MrGlim.NoMineshaft net handler registered.");
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

        var writer = new FastBufferWriter(16, Allocator.Temp);
        writer.WriteValueSafe(OpHostHello);
        writer.WriteValueSafe(LocalEnabled());
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
        if (nm != null && !nm.IsServer)
        {
            _clientHostEnabled = enabled;
            Plugin.Log.LogInfo("Host hello: enabled=" + enabled);
        }
    }
}

[HarmonyPatch(typeof(GameNetworkManager), "Disconnect")]
internal static class HostModGateDisconnectPatch
{
    public static void Prefix()
    {
        HostModGate.Reset();
        Plugin.Log.LogInfo("MrGlim.NoMineshaft gate reset on disconnect.");
    }
}
