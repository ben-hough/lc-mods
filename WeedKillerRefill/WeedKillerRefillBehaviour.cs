using System;
using UnityEngine;

namespace WeedKillerRefill;

/// <summary>
/// While holding an empty weed killer bottle, press the configured key to refill tank + battery.
/// </summary>
internal sealed class WeedKillerRefillBehaviour : MonoBehaviour
{
    private bool _loggedHoldingEmpty;

    private void Update()
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
                $"Holding empty weed killer (tank={spray.sprayCanTank:0.###}, tryingEmpty={spray.tryingToUseEmptyCan}). Press {Plugin.RefillKey.Value} to refill.");
        }
        else if (!empty)
        {
            _loggedHoldingEmpty = false;
        }

        if (!InputUtil.WasPressedThisFrame(Plugin.RefillKey.Value))
            return;

        Plugin.Log.LogInfo($"Refill key {Plugin.RefillKey.Value} pressed (empty={empty}, tank={spray.sprayCanTank:0.###}).");

        if (Plugin.OnlyWhenEmpty.Value && !empty)
            return;

        try
        {
            WeedKillerUtil.Refill(spray);
            Plugin.Log.LogInfo("Refilled weed killer tank + battery.");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to refill weed killer: {ex.Message}");
        }
    }
}
