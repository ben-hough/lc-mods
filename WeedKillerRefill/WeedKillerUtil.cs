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

    public static bool IsHeldByLocalPlayer(SprayPaintItem spray)
    {
        var player = GameNetworkManager.Instance?.localPlayerController;
        if (player == null || spray == null)
            return false;

        var held = player.currentlyHeldObject ?? player.currentlyHeldObjectServer;
        return held == spray || spray.playerHeldBy == player;
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
            if (bat != null && (bat.empty || bat.charge <= threshold))
                return true;
        }
        catch
        {
            // ignored
        }

        return false;
    }

    /// <summary>
    /// Full tank + shake meter restore. Mirrors what paint-can shaking does to
    /// sprayCanShakeMeter, plus fills sprayCanTank (weed killer has no vanilla shake refill).
    /// </summary>
    public static void Refill(SprayPaintItem spray, string source)
    {
        spray.sprayCanTank = 1f;
        spray.sprayCanShakeMeter = 1f;
        spray.tryingToUseEmptyCan = false;

        try
        {
            if (spray.sprayCanNeedsShakingParticle != null && spray.sprayCanNeedsShakingParticle.isPlaying)
                spray.sprayCanNeedsShakingParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        catch
        {
            // optional
        }

        try
        {
            var bat = spray.insertedBattery;
            if (bat != null)
            {
                bat.charge = 1f;
                bat.empty = false;
                spray.insertedBattery = bat;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Battery refill partial: {ex.Message}");
        }

        try
        {
            if (spray.itemProperties != null && spray.itemProperties.requiresBattery)
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
            // optional — ChargeBatteries is how WeedKillerAdjuster links charger → tank
        }

        try
        {
            spray.SetControlTipsForItem();
        }
        catch
        {
            // ignored
        }

        Plugin.Log.LogInfo(
            $"[{source}] Refilled: tank={spray.sprayCanTank:0.###} shake={spray.sprayCanShakeMeter:0.###} tryingEmpty={spray.tryingToUseEmptyCan}");
    }

    public static void PlayShakeFeedback(SprayPaintItem spray)
    {
        try
        {
            if (spray.playerHeldBy != null)
                spray.playerHeldBy.playerBodyAnimator.SetTrigger("shakeItem");
        }
        catch
        {
            // ignored
        }

        try
        {
            if (spray.sprayCanShakeSFX != null && spray.sprayCanShakeSFX.Length > 0 && spray.sprayAudio != null)
            {
                RoundManager.PlayRandomClip(spray.sprayAudio, spray.sprayCanShakeSFX);
                if (spray.sprayCanShakeEmptySFX != null)
                    WalkieTalkie.TransmitOneShotAudio(spray.sprayAudio, spray.sprayCanShakeEmptySFX);
            }
        }
        catch
        {
            // ignored
        }
    }
}
