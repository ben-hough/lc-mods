using System;

namespace WeedKillerRefill;

internal static class RefillController
{
    private static bool _loggedHoldingEmpty;
    private static float _nextHeartbeat;
    private static int _lastRefillFrame = -1;

    internal static void Tick(string source)
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value || !Plugin.AllowKeyRefill.Value)
            return;

        if (!WeedKillerUtil.TryGetHeldWeedKiller(out var spray))
        {
            _loggedHoldingEmpty = false;
            return;
        }

        TickHeld(spray, source);
    }

    internal static void TickHeld(SprayPaintItem spray, string source)
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value || spray == null)
            return;

        if (!Plugin.AllowKeyRefill.Value)
            return;

        var empty = WeedKillerUtil.IsEmpty(spray);
        if (empty && !_loggedHoldingEmpty)
        {
            _loggedHoldingEmpty = true;
            Plugin.Log.LogInfo(
                $"[{source}] Holding empty weed killer (tank={spray.sprayCanTank:0.###}). Press Q to shake-refill (or {Plugin.RefillKey.Value}).");
        }
        else if (!empty)
        {
            _loggedHoldingEmpty = false;
        }

        if (UnityEngine.Time.unscaledTime >= _nextHeartbeat)
        {
            _nextHeartbeat = UnityEngine.Time.unscaledTime + 8f;
            Plugin.V($"[{source}] holding weedkiller empty={empty} tank={spray.sprayCanTank:0.###} keyDown={InputUtil.IsDown(Plugin.RefillKey.Value)}");
        }

        if (!InputUtil.WasPressedThisFrame(Plugin.RefillKey.Value))
            return;

        var frame = UnityEngine.Time.frameCount;
        if (frame == _lastRefillFrame)
            return;
        _lastRefillFrame = frame;

        Plugin.Log.LogInfo($"[{source}] Refill key {Plugin.RefillKey.Value} pressed (empty={empty}, tank={spray.sprayCanTank:0.###}).");

        if (Plugin.OnlyWhenEmpty.Value && !empty)
            return;

        try
        {
            WeedKillerUtil.Refill(spray, source);
            WeedKillerUtil.PlayShakeFeedback(spray);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[{source}] Failed to refill: {ex.Message}");
        }
    }
}
