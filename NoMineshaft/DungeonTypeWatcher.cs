using UnityEngine;

namespace NoMineshaft;

internal sealed class DungeonTypeWatcher : MonoBehaviour
{
    private static DungeonTypeWatcher? _instance;
    private float _next;
    private int _lastType = int.MinValue;

    internal static void EnsureExists()
    {
        // Unity fake-null friendly check
        if (_instance != null)
            return;

        var go = new GameObject("NoMineshaftWatcher");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<DungeonTypeWatcher>();
        Plugin.Log.LogInfo("[Watcher] created");
    }

    private void Awake() => _instance = this;

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return;

        if (Time.unscaledTime < _next)
            return;
        _next = Time.unscaledTime + 5f;

        var rm = RoundManager.Instance;
        var start = StartOfRound.Instance;
        if (rm == null)
        {
            Plugin.Log.LogInfo("[Watcher] RoundManager null");
            return;
        }

        if (rm.currentDungeonType != _lastType)
        {
            Plugin.Log.LogInfo(
                $"[Watcher] currentDungeonType {_lastType} -> {rm.currentDungeonType} " +
                $"(mineshaft={rm.currentDungeonType == 4}) level={rm.currentLevel?.name} " +
                $"inShipPhase={start?.inShipPhase} isServer={rm.IsServer} generating={rm.dungeonIsGenerating}");
            _lastType = rm.currentDungeonType;

            if (rm.currentDungeonType == 4)
            {
                MineshaftScrubber.ScrubLevel(rm.currentLevel, "Watcher");
                MineshaftScrubber.RemapDungeonType(rm, "Watcher");
            }
        }
        else
        {
            Plugin.Log.LogInfo(
                $"[Watcher] tick type={rm.currentDungeonType} level={rm.currentLevel?.name} " +
                $"inShipPhase={start?.inShipPhase} isServer={rm.IsServer} generating={rm.dungeonIsGenerating}");
        }
    }
}
