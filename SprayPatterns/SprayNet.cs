using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

namespace SprayPatterns;

/// <summary>
/// Client → server → all reliable named messages for pattern stamps.
/// Layout: patternId(byte) + pos(3f) + forward(3f) + normal(3f) + rot(f) + size(f) + rgba(4 bytes) + sender(ulong)
/// </summary>
internal static class SprayNet
{
    public const string MessageName = "MrGlim.SprayPatterns";
    private static bool _registered;

    public static void EnsureRegistered()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            _registered = false;
            return;
        }

        if (_registered)
            return;

        nm.CustomMessagingManager.RegisterNamedMessageHandler(MessageName, OnMessage);
        _registered = true;
        Plugin.Log.LogInfo("SprayPatterns net handler registered.");
    }

    public static void BroadcastStamp(
        PatternId pattern,
        Vector3 position,
        Vector3 forward,
        Vector3 normal,
        float rotationDegrees,
        float size,
        Color tint)
    {
        EnsureRegistered();
        var nm = NetworkManager.Singleton;
        if (nm == null)
            return;

        var sender = nm.LocalClientId;
        var writer = Write(pattern, position, forward, normal, rotationDegrees, size, tint, sender);
        if (nm.IsServer)
            nm.CustomMessagingManager.SendNamedMessageToAll(MessageName, writer, NetworkDelivery.Reliable);
        else
            nm.CustomMessagingManager.SendNamedMessage(MessageName, NetworkManager.ServerClientId, writer, NetworkDelivery.Reliable);
        writer.Dispose();
    }

    private static FastBufferWriter Write(
        PatternId pattern,
        Vector3 position,
        Vector3 forward,
        Vector3 normal,
        float rotationDegrees,
        float size,
        Color tint,
        ulong sender)
    {
        // 1 + 3*3*4 + 4 + 4 + 4 + 8 = 1+36+4+4+4+8 = 57
        var writer = new FastBufferWriter(64, Allocator.Temp);
        writer.WriteValueSafe((byte)pattern);
        writer.WriteValueSafe(position.x);
        writer.WriteValueSafe(position.y);
        writer.WriteValueSafe(position.z);
        writer.WriteValueSafe(forward.x);
        writer.WriteValueSafe(forward.y);
        writer.WriteValueSafe(forward.z);
        writer.WriteValueSafe(normal.x);
        writer.WriteValueSafe(normal.y);
        writer.WriteValueSafe(normal.z);
        writer.WriteValueSafe(rotationDegrees);
        writer.WriteValueSafe(size);
        writer.WriteValueSafe((byte)Mathf.Clamp(Mathf.RoundToInt(tint.r * 255f), 0, 255));
        writer.WriteValueSafe((byte)Mathf.Clamp(Mathf.RoundToInt(tint.g * 255f), 0, 255));
        writer.WriteValueSafe((byte)Mathf.Clamp(Mathf.RoundToInt(tint.b * 255f), 0, 255));
        writer.WriteValueSafe((byte)Mathf.Clamp(Mathf.RoundToInt(tint.a * 255f), 0, 255));
        writer.WriteValueSafe(sender);
        return writer;
    }

    private static void OnMessage(ulong senderClientId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out byte patternByte);
        reader.ReadValueSafe(out float px);
        reader.ReadValueSafe(out float py);
        reader.ReadValueSafe(out float pz);
        reader.ReadValueSafe(out float fx);
        reader.ReadValueSafe(out float fy);
        reader.ReadValueSafe(out float fz);
        reader.ReadValueSafe(out float nx);
        reader.ReadValueSafe(out float ny);
        reader.ReadValueSafe(out float nz);
        reader.ReadValueSafe(out float rot);
        reader.ReadValueSafe(out float size);
        reader.ReadValueSafe(out byte r);
        reader.ReadValueSafe(out byte g);
        reader.ReadValueSafe(out byte b);
        reader.ReadValueSafe(out byte a);
        reader.ReadValueSafe(out ulong originSender);

        var pattern = (PatternId)patternByte;
        var pos = new Vector3(px, py, pz);
        var forward = new Vector3(fx, fy, fz);
        var normal = new Vector3(nx, ny, nz);
        var tint = new Color(r / 255f, g / 255f, b / 255f, a / 255f);

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsServer && senderClientId != nm.LocalClientId)
        {
            // Echo to everyone (including origin so late code paths stay simple; origin skips apply).
            var echo = Write(pattern, pos, forward, normal, rot, size, tint, originSender);
            nm.CustomMessagingManager.SendNamedMessageToAll(MessageName, echo, NetworkDelivery.Reliable);
            echo.Dispose();
        }

        if (nm != null && originSender == nm.LocalClientId)
            return; // already applied locally

        if (!PatternCatalog.IsPatternStamp(pattern))
            return;

        StampSpawner.PlaceStamp(
            pattern,
            pos,
            forward,
            normal,
            rot,
            size,
            tint,
            parent: null,
            isPreview: false);

        Plugin.V($"Net stamp applied pattern={pattern} from={originSender}");
    }
}
