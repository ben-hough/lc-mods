using System;

namespace WeedKillerRefill;

internal static class RefillController
{
    private static bool _loggedHoldingEmpty;
    private static float _nextHeartbeat;

    internal static void Tick(string source)
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return;

        if (!WeedKillerUtil.TryGetHeldWeedKiller(out var spray))
        {
            _loggedHoldingEmpty = false;
            return;
        }

        var empty = WeedKillerUtil.IsEmpty(spray);
        if (empty && !_loggedHoldingEmpty)
        {
            _loggedHoldingEmpty = true;
            Plugin.Log.LogInfo(
                $"[{source}] Holding empty weed killer (tank={spray.sprayCanTank:0.###}, tryingEmpty={spray.tryingToUseEmptyCan}). Press {Plugin.RefillKey.Value}.");
        }
        else if (!empty)
        {
            _loggedHoldingEmpty = false;
        }

        // Occasional heartbeat while holding weed killer so we know Tick runs.
        if (UnityEngine.Time.unscaledTime >= _nextHeartbeat)
        {
            _nextHeartbeat = UnityEngine.Time.unscaledTime + 8f;
            Plugin.V($"[{source}] holding weedkiller empty={empty} tank={spray.sprayCanTank:0.###} keyDown={InputUtil.IsDown(Plugin.RefillKey.Value)}");
        }

        if (!InputUtil.WasPressedThisFrame(Plugin.RefillKey.Value))
            return;

        Plugin.Log.LogInfo($"[{source}] Refill key {Plugin.RefillKey.Value} pressed (empty={empty}, tank={spray.sprayCanTank:0.###}).");

        if (Plugin.OnlyWhenEmpty.Value && !empty)
            return;

        try
        {
            WeedKillerUtil.Refill(spray);
            Plugin.Log.LogInfo($"[{source}] Refilled weed killer tank + battery.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[{source}] Failed to refill: {ex.Message}");
        }
    }
}
