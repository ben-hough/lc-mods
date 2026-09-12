using System.Collections;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using MoreShipUpgrades.API;
using MoreShipUpgrades.UI.TerminalNodes;
using UnityEngine;

namespace ForceLguRefund;

[BepInPlugin(PluginInfo.PLUGIN_GUID, PluginInfo.PLUGIN_NAME, PluginInfo.PLUGIN_VERSION)]
[BepInDependency(LguGuid, BepInDependency.DependencyFlags.HardDependency)]
public class Plugin : BaseUnityPlugin
{
    public const string ModGuid = "com.benhough.lethal.ForceLguRefund";
    public const string ModName = "ForceLguRefund";
    public const string ModVersion = "1.0.0";
    public const string LguGuid = "com.malco.lethalcompany.moreshipupgrades";

    internal static ManualLogSource Log { get; private set; } = null!;
    internal static ConfigEntry<bool> Enabled = null!;
    /// <summary>-1 = leave node %, else 0–100 applied as 0–1.</summary>
    internal static ConfigEntry<int> RefundPercentageOverride = null!;

    private void Awake()
    {
        Log = Logger;
        Enabled = Config.Bind("General", "Enabled", true,
            "Force CustomTerminalNode.Refundable=true so the LGU refund UI works as client.");
        RefundPercentageOverride = Config.Bind("General", "RefundPercentageOverride", -1,
            "If >= 0, override each node's RefundPercentage (0–100 → 0–1). -1 leaves node values (but 0 becomes 1).");

        new Harmony(ModGuid).PatchAll(typeof(Plugin).Assembly);
        StartCoroutine(ForceRefundableLoop());
        Log.LogInfo($"{ModName} v{ModVersion} loaded (LGU dep: {LguGuid}).");
    }

    private IEnumerator ForceRefundableLoop()
    {
        var wait = new WaitForSeconds(2f);
        while (true)
        {
            try
            {
                ForceAllNodesRefundable();
            }
            catch (System.Exception ex)
            {
                Log.LogDebug($"Force refundable tick skipped: {ex.Message}");
            }
            yield return wait;
        }
    }

    internal static void ForceAllNodesRefundable()
    {
        if (!Enabled.Value)
            return;

        System.Collections.Generic.List<CustomTerminalNode>? nodes;
        try
        {
            nodes = UpgradeApi.GetUpgradeNodes();
        }
        catch
        {
            return;
        }

        if (nodes == null || nodes.Count == 0)
            return;

        float? pctOverride = null;
        int ov = RefundPercentageOverride.Value;
        if (ov >= 0)
            pctOverride = Mathf.Clamp01(ov / 100f);

        foreach (var node in nodes)
        {
            if (node == null)
                continue;

            if (!node.Refundable)
                node.Refundable = true;

            if (pctOverride.HasValue)
                node.RefundPercentage = pctOverride.Value;
            else if (node.RefundPercentage <= 0f)
                node.RefundPercentage = 1f;
        }

        TryForceSyncedRefundConfig();
    }

    /// <summary>
    /// Best-effort: set local REFUND_UPGRADES if writable. CSync may overwrite; node.Refundable is the real UI gate.
    /// </summary>
    private static void TryForceSyncedRefundConfig()
    {
        try
        {
            var busType = AccessTools.TypeByName("MoreShipUpgrades.Managers.UpgradeBus");
            if (busType == null)
                return;

            var instanceProp = AccessTools.Property(busType, "Instance");
            var instance = instanceProp?.GetValue(null);
            if (instance == null)
                return;

            var cfgProp = AccessTools.Property(busType, "PluginConfiguration");
            var cfg = cfgProp?.GetValue(instance);
            if (cfg == null)
                return;

            var refundProp = AccessTools.Property(cfg.GetType(), "REFUND_UPGRADES");
            var entry = refundProp?.GetValue(cfg);
            if (entry == null)
                return;

            var valueProp = AccessTools.Property(entry.GetType(), "Value");
            if (valueProp == null || !valueProp.CanWrite)
                return;

            if (valueProp.GetValue(entry) is bool current && !current)
                valueProp.SetValue(entry, true);
        }
        catch
        {
            // Ignore — node.Refundable polling is enough.
        }
    }
}

internal static class PluginInfo
{
    public const string PLUGIN_GUID = Plugin.ModGuid;
    public const string PLUGIN_NAME = Plugin.ModName;
    public const string PLUGIN_VERSION = Plugin.ModVersion;
}
