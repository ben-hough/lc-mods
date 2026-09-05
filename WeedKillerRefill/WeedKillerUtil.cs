using System;
using GameNetcodeStuff;
using UnityEngine;

namespace WeedKillerRefill;

internal static class WeedKillerUtil
{
    public static bool TryGetHeldWeedKiller(out SprayPaintItem spray)
    {
        spray = null!;
        try
        {
            var player = GameNetworkManager.Instance?.localPlayerController;
            if (player == null || player.isPlayerDead)
                return false;

            // Prefer client-local held object; server field can lag / be wrong on clients.
            GrabbableObject? held = player.currentlyHeldObject;
            if (held == null)
                held = player.currentlyHeldObjectServer;

            if (held is not SprayPaintItem item)
                return false;

            if (!item.isWeedKillerSprayBottle)
                return false;

            spray = item;
            return true;
        }
        catch
        {
            return false;
        }
    }

    public static bool IsEmpty(SprayPaintItem spray)
    {
        var threshold = Plugin.EmptyThreshold.Value;
        if (spray.sprayCanTank <= threshold)
            return true;
        if (spray.tryingToUseEmptyCan)
            return true;

        try
        {
            var bat = spray.insertedBattery;
            if (bat.empty || bat.charge <= threshold)
                return true;
        }
        catch
        {
            // ignored
        }

        return false;
    }

    public static void Refill(SprayPaintItem spray)
    {
        spray.sprayCanTank = 1f;
        spray.tryingToUseEmptyCan = false;
        spray.sprayCanShakeMeter = 0f;

        try
        {
            var bat = spray.insertedBattery;
            bat.charge = 1f;
            bat.empty = false;
            spray.insertedBattery = bat;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Battery refill partial: {ex.Message}");
        }

        try
        {
            // Keep HUD / networked battery in sync (works for local owner).
            spray.SyncBatteryServerRpc(100);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"SyncBatteryServerRpc failed: {ex.Message}");
        }

        try
        {
            spray.ChargeBatteries();
        }
        catch
        {
            // optional
        }

        // Refresh tips so Spray tip returns and Refill tip clears.
        try
        {
            spray.SetControlTipsForItem();
        }
        catch
        {
            // ignored
        }
    }
}
