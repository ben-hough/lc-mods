using HarmonyLib;
using Unity.Netcode;
using UnityEngine;

namespace CadaverWilt;

[HarmonyPatch(typeof(SprayPaintItem), nameof(SprayPaintItem.KillCadaverPlantRpc))]
internal static class KillPlantPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        CadaverWipe.TryKillBlooms();
    }
}

[HarmonyPatch(typeof(CadaverGrowthAI), nameof(CadaverGrowthAI.DestroyPlantAtPosition))]
internal static class DestroyPlantPatch
{
    [HarmonyPostfix]
    private static void Postfix()
    {
        CadaverWipe.TryKillBlooms();
    }
}

internal static class CadaverWipe
{
    internal static void TryKillBlooms()
    {
        if (!HostModGate.FeaturesActive)
            return;

        var nm = NetworkManager.Singleton;
        if (nm != null && !nm.IsServer)
            return;

        var growths = Object.FindObjectsOfType<CadaverGrowthAI>();
        if (growths == null || growths.Length == 0)
            return;

        var anyPlants = false;
        var anyTiles = false;
        foreach (var growth in growths)
        {
            if (growth == null || growth.GrowthTiles == null)
                continue;

            foreach (var tile in growth.GrowthTiles)
            {
                if (tile == null)
                    continue;
                anyTiles = true;
                if (tile.plantsInTile > 0)
                    anyPlants = true;
                if (tile.plantPositions != null && tile.plantPositions.Count > 0)
                    anyPlants = true;
            }

            if (growth.growingRecentPlant)
                anyPlants = true;
        }

        if (!anyTiles || anyPlants)
            return;

        var killed = 0;
        var blooms = Object.FindObjectsOfType<CadaverBloomAI>();
        if (blooms == null)
            return;

        foreach (var bloom in blooms)
        {
            if (bloom == null || bloom.isEnemyDead)
                continue;
            try
            {
                bloom.KillEnemy(true);
                killed++;
            }
            catch
            {
                // ignore single failure
            }
        }

        if (killed > 0)
            Plugin.Log.LogInfo($"All cadaver plants gone. Killed {killed} cadaver(s).");
    }
}

[HarmonyPatch(typeof(StartOfRound), "Start")]
internal static class HostModGateStartPatch
{
    private static void Postfix() => HostModGate.EnsureRegistered();
}
