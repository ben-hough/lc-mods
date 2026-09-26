using System.Reflection;
using HarmonyLib;
using MoreShipUpgrades.UI.TerminalNodes;

namespace ForceLguRefund;

/// <summary>
/// Belt-and-suspenders: CanRefundUpgradeLevel is the cursor enable predicate
/// (Refundable &amp;&amp; Unlocked). Also re-force Refundable when the store builds menus.
/// </summary>
[HarmonyPatch]
internal static class CanRefundUpgradeLevelPatch
{
    private static MethodBase? TargetMethod()
    {
        var type = AccessTools.TypeByName("MoreShipUpgrades.UI.Application.UpgradeStoreApplication");
        return type == null ? null : AccessTools.Method(type, "CanRefundUpgradeLevel");
    }

    [HarmonyPrefix]
    private static bool Prefix(CustomTerminalNode node, ref bool __result)
    {
        if (!HostModGate.FeaturesActive || node == null)
            return true;

        // Ensure ConfirmRefundUpgradeLevel's Refundable check also passes.
        if (node.Unlocked && !node.Refundable)
            node.Refundable = true;

        __result = node.Unlocked;
        return false; // skip original
    }
}

[HarmonyPatch]
internal static class ConfirmRefundUpgradeLevelPatch
{
    private static MethodBase? TargetMethod()
    {
        var type = AccessTools.TypeByName("MoreShipUpgrades.UI.Application.UpgradeStoreApplication");
        return type == null ? null : AccessTools.Method(type, "ConfirmRefundUpgradeLevel");
    }

    [HarmonyPrefix]
    private static void Prefix(CustomTerminalNode node)
    {
        if (!HostModGate.FeaturesActive || node == null)
            return;

        if (!node.Refundable)
            node.Refundable = true;

        Plugin.ForceAllNodesRefundable();
    }
}
