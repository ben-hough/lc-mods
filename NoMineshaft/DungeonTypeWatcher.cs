using UnityEngine;

namespace NoMineshaft;

/// <summary>Periodic debug of currentDungeonType so we can see host/client state even if Prefix hooks miss.</summary>
internal sealed class DungeonTypeWatcher : MonoBehaviour
{
    private float _next;
    private int _lastType = int.MinValue;

    private void Update()
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return;

        if (Time.unscaledTime < _next)
            return;

        _next = Time.unscaledTime + 8f;

        var rm = RoundManager.Instance;
        var start = StartOfRound.Instance;
        if (rm == null)
        {
            Plugin.V("[Watcher] RoundManager null");
            return;
        }

        if (rm.currentDungeonType != _lastType)
        {
            Plugin.Log.LogInfo(
                $("[Watcher] currentDungeonType changed {_lastType} -> {rm.currentDungeonType} " +
                $"(mineshaft={rm.currentDungeonType == 4}) level={rm.currentLevel?.name} " +
                $"inShipPhase={start?.inShipPhase} isServer={rm.IsServer}");
            _lastType = rm.currentDungeonType;
        }
        else
        {
            Plugin.V(
                $("[Watcher] currentDungeonType={rm.currentDungeonType} level={rm.currentLevel?.name} " +
                $"inShipPhase={start?.inShipPhase} isServer={rm.IsServer}");
        }
    }
}
