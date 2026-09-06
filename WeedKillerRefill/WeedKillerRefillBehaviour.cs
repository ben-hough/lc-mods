using UnityEngine;

namespace WeedKillerRefill;

/// <summary>Frame loop backup. Recreated after scene loads via EnsureExists.</summary>
internal sealed class WeedKillerRefillBehaviour : MonoBehaviour
{
    private static WeedKillerRefillBehaviour? _instance;
    private float _nextAliveLog;

    internal static void EnsureExists()
    {
        // Unity fake-null friendly
        if (_instance != null)
            return;

        var go = new GameObject("WeedKillerRefill");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<WeedKillerRefillBehaviour>();
        Plugin.Log.LogInfo("[Behaviour] created");
    }

    private void Awake() => _instance = this;

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void LateUpdate()
    {
        if (Time.unscaledTime >= _nextAliveLog)
        {
            _nextAliveLog = Time.unscaledTime + 30f;
            Plugin.V("[Behaviour] LateUpdate alive");
        }

        RefillController.Tick("Behaviour.LateUpdate");
    }
}
