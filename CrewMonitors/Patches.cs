using HarmonyLib;
using UnityEngine;

namespace CrewMonitors;

[HarmonyPatch(typeof(StartOfRound))]
internal static class StartOfRoundPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(StartOfRound.Start))]
    private static void StartPostfix(StartOfRound __instance)
    {
        if (!Plugin.Enabled.Value)
            return;

        if (__instance.GetComponent<CrewMonitorManager>() != null)
            return;

        __instance.gameObject.AddComponent<CrewMonitorManager>();
        Plugin.Log.LogInfo("CrewMonitorManager attached after StartOfRound.Start.");
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(StartOfRound.OnDestroy))]
    private static void OnDestroyPostfix()
    {
        // Manager lives on StartOfRound and is destroyed with it.
    }
}

[HarmonyPatch(typeof(GameNetcodeStuff.PlayerControllerB))]
internal static class PlayerPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameNetcodeStuff.PlayerControllerB.KillPlayerClientRpc))]
    private static void KillPlayerClientRpcPostfix()
    {
        CrewMonitorManager.Instance?.RequestRefresh();
    }

    [HarmonyPostfix]
    [HarmonyPatch(nameof(GameNetcodeStuff.PlayerControllerB.ConnectClientToPlayerObject))]
    private static void ConnectClientToPlayerObjectPostfix()
    {
        // OpenBodyCams finishes LateInitialization here; nudge our setup soon after.
        CrewMonitorManager.Instance?.RequestSetupSoon();
    }
}
