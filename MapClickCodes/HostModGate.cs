using HarmonyLib;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace MapClickCodes;

/// <summary>
/// Host presence gate + client→host activate requests via Unity Netcode named messages.
/// Click-to-code stays off on pure clients until host-hello (enabled=true).
/// </summary>
internal static class HostModGate
{
    public const string MessageName = "MrGlim.MapClickCodes";
    private const byte OpHostHello = 0;
    private const byte OpClientSyncRequest = 1;
    private const byte OpActivateRequest = 2;

    private const float SyncRetryInterval = 2f;

    private static bool _registered;
    private static bool _clientConnectedHooked;
    private static bool _requestedSync;
    private static bool _clientHostEnabled;
    private static float _nextSyncRetry;
    private static NetworkManager? _hookedNm;

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

    /// <summary>True when a connected non-host client is still waiting for host hello.</summary>
    public static bool WaitingForHostHello
    {
        get
        {
            var nm = NetworkManager.Singleton;
            if (nm == null || nm.IsServer || nm.IsHost)
                return false;
            return nm.IsConnectedClient && !_clientHostEnabled;
        }
    }

    private static bool LocalEnabled()
    {
        return Plugin.Enabled != null && Plugin.Enabled.Value;
    }

    public static void Reset()
    {
        UnhookClientConnected();
        _registered = false;
        _clientConnectedHooked = false;
        _requestedSync = false;
        _clientHostEnabled = false;
        _nextSyncRetry = 0f;
        _hookedNm = null;
    }

    /// <summary>Called every frame from Plugin.Update — keeps handler registered and retries hello.</summary>
    public static void Tick()
    {
        EnsureRegistered();

        var nm = NetworkManager.Singleton;
        if (nm == null || nm.IsServer || nm.IsHost)
            return;
        if (!nm.IsConnectedClient)
            return;
        if (_clientHostEnabled)
            return;

        float now = Time.unscaledTime;
        if (now < _nextSyncRetry)
            return;

        _nextSyncRetry = now + SyncRetryInterval;
        RequestSync();
        Plugin.Log.LogInfo("MapClickCodes: retrying host hello sync (gate still closed).");
    }

    public static void EnsureRegistered()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            if (_registered || _clientConnectedHooked)
                Reset();
            return;
        }

        // NetworkManager instance may be replaced between sessions.
        if (_hookedNm != null && !ReferenceEquals(_hookedNm, nm))
        {
            UnhookClientConnected();
            _registered = false;
            _clientConnectedHooked = false;
            _requestedSync = false;
            _clientHostEnabled = false;
            _nextSyncRetry = 0f;
        }

        _hookedNm = nm;

        if (!_registered)
        {
            nm.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
            _registered = true;
            Plugin.Log.LogInfo("MrGlim.MapClickCodes net handler registered.");
        }

        if (nm.IsServer && !_clientConnectedHooked)
        {
            nm.OnClientConnectedCallback += OnClientConnected;
            _clientConnectedHooked = true;
        }

        if (!nm.IsServer && nm.IsConnectedClient && !_requestedSync)
        {
            _requestedSync = true;
            _nextSyncRetry = Time.unscaledTime + SyncRetryInterval;
            RequestSync();
        }
    }

    private static void UnhookClientConnected()
    {
        if (_hookedNm != null && _clientConnectedHooked)
        {
            try
            {
                _hookedNm.OnClientConnectedCallback -= OnClientConnected;
            }
            catch
            {
                /* instance may already be destroyed */
            }
        }

        _clientConnectedHooked = false;
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
        if (nm.CustomMessagingManager == null)
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

    /// <summary>Client → host: activate TerminalAccessibleObject by NetworkObjectId (+ code for log/fallback).</summary>
    public static void SendActivateRequest(ulong networkObjectId, string objectCode)
    {
        EnsureRegistered();
        var nm = NetworkManager.Singleton;
        if (nm == null || nm.IsServer)
            return;
        if (!nm.IsConnectedClient || nm.CustomMessagingManager == null)
            return;

        string code = objectCode ?? "";
        int codeBytes = System.Text.Encoding.UTF8.GetByteCount(code);
        var writer = new FastBufferWriter(24 + codeBytes, Allocator.Temp);
        writer.WriteValueSafe(OpActivateRequest);
        writer.WriteValueSafe(networkObjectId);
        writer.WriteValueSafe(code);
        nm.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
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

        if (op == OpActivateRequest)
        {
            if (nm == null || !nm.IsServer)
                return;

            reader.ReadValueSafe(out ulong networkObjectId);
            reader.ReadValueSafe(out string objectCode);
            HandleActivateOnHost(sender, networkObjectId, objectCode);
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

    private static void HandleActivateOnHost(ulong sender, ulong networkObjectId, string objectCode)
    {
        if (!LocalEnabled())
        {
            Plugin.Log.LogWarning($"Ignoring activate from client {sender}: mod disabled on host.");
            return;
        }

        TerminalAccessibleObject? matched = null;

        if (networkObjectId != 0UL)
        {
            var nm = NetworkManager.Singleton;
            if (nm != null
                && nm.SpawnManager != null
                && nm.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject no)
                && no != null)
            {
                matched = no.GetComponent<TerminalAccessibleObject>();
            }
        }

        if (matched == null && !string.IsNullOrEmpty(objectCode))
        {
            var objs = Object.FindObjectsOfType<TerminalAccessibleObject>();
            for (int i = 0; i < objs.Length; i++)
            {
                var tao = objs[i];
                if (tao != null && tao.objectCode == objectCode)
                {
                    matched = tao;
                    break;
                }
            }
        }

        if (matched == null)
        {
            Plugin.Log.LogWarning(
                $"Activate from client {sender}: object not found (netId={networkObjectId}, code='{objectCode}').");
            return;
        }

        string code = matched.objectCode ?? objectCode ?? "?";
        TerminalCodeActivation.Activate(
            matched,
            source: $"host-for-client-{sender}|netId={networkObjectId}|code={code}");
    }
}

[HarmonyPatch(typeof(GameNetworkManager), "Disconnect")]
internal static class HostModGateDisconnectPatch
{
    public static void Prefix()
    {
        HostModGate.Reset();
        Plugin.Log.LogInfo("MrGlim.MapClickCodes gate reset on disconnect.");
    }
}

[HarmonyPatch(typeof(StartOfRound), "Start")]
internal static class HostModGateStartPatch
{
    public static void Postfix() => HostModGate.EnsureRegistered();
}

[HarmonyPatch(typeof(NetworkManager), "Initialize")]
internal static class HostModGateNetworkInitPatch
{
    public static void Postfix() => HostModGate.EnsureRegistered();
}
