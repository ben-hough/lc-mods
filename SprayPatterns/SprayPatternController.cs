using GameNetcodeStuff;
using UnityEngine;

namespace SprayPatterns;

/// <summary>
/// Owns radial toggle, rotation, ghost preview, and pattern stamp apply helpers.
/// </summary>
internal sealed class SprayPatternController : MonoBehaviour
{
    private static SprayPatternController? _instance;
    private RadialMenuUI? _radial;
    private float _stampCooldownLeft;
    private bool _cursorWasLocked;

    internal static void EnsureExists()
    {
        if (_instance != null)
            return;
        var go = new GameObject("SprayPatterns_Controller");
        DontDestroyOnLoad(go);
        _instance = go.AddComponent<SprayPatternController>();
    }

    public static SprayPatternController? Instance => _instance;

    private void Awake()
    {
        _instance = this;
        var radialGo = new GameObject("SprayPatterns_Radial");
        DontDestroyOnLoad(radialGo);
        _radial = radialGo.AddComponent<RadialMenuUI>();
        _radial.EnsureBuilt();
    }

    private void OnDestroy()
    {
        if (_instance == this)
            _instance = null;
    }

    private void Update()
    {
        if (!Plugin.Enabled.Value)
        {
            CloseRadial();
            StampSpawner.HidePreview();
            return;
        }

        SprayNet.EnsureRegistered();

        if (_stampCooldownLeft > 0f)
            _stampCooldownLeft -= Time.unscaledDeltaTime;

        var spray = GetLocalSprayCan();
        var holdingPaintCan = spray != null && !spray.isWeedKillerSprayBottle;

        if (!holdingPaintCan)
        {
            CloseRadial();
            StampSpawner.HidePreview();
            return;
        }

        HandleRadialToggle();
        HandleRotation(holdingPaintCan);

        if (_radial != null && _radial.IsOpen)
        {
            StampSpawner.HidePreview();
            var hover = _radial.TickHover();
            if (InputUtil.LeftClickPressedThisFrame() && hover.HasValue)
            {
                PatternCatalog.Selected = hover.Value;
                Plugin.Log.LogInfo($"Selected spray pattern: {PatternCatalog.Get(hover.Value).Label}");
                CloseRadial();
            }
            return;
        }

        UpdatePreview(spray!);
    }

    private void HandleRadialToggle()
    {
        if (!InputUtil.WasPressedThisFrame(Plugin.RadialKey.Value))
            return;

        if (_radial == null)
            return;

        if (_radial.IsOpen)
            CloseRadial();
        else
            OpenRadial();
    }

    private void OpenRadial()
    {
        if (_radial == null)
            return;
        _radial.Rebuild();
        _radial.SetVisible(true);
        try
        {
            _cursorWasLocked = Cursor.lockState == CursorLockMode.Locked;
            Cursor.visible = true;
            Cursor.lockState = CursorLockMode.None;
        }
        catch
        {
            // ignored
        }
        Plugin.V("Radial opened");
    }

    private void CloseRadial()
    {
        if (_radial == null || !_radial.IsOpen)
            return;
        _radial.SetVisible(false);
        try
        {
            if (_cursorWasLocked)
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
            }
        }
        catch
        {
            // ignored
        }
        Plugin.V("Radial closed");
    }

    private void HandleRotation(bool holding)
    {
        if (!holding)
            return;
        if (!PatternCatalog.IsPatternStamp(PatternCatalog.Selected))
            return;

        var step = Plugin.RotateStepDegrees.Value;
        if (InputUtil.WasPressedThisFrame(Plugin.RotateLeftKey.Value))
            PatternCatalog.RotationDegrees = Mathf.Repeat(PatternCatalog.RotationDegrees - step, 360f);
        if (InputUtil.WasPressedThisFrame(Plugin.RotateRightKey.Value))
            PatternCatalog.RotationDegrees = Mathf.Repeat(PatternCatalog.RotationDegrees + step, 360f);

        var scroll = InputUtil.ScrollYThisFrame();
        if (Mathf.Abs(scroll) > 0.01f)
        {
            var dir = scroll > 0f ? 1f : -1f;
            if (Plugin.InvertScroll.Value)
                dir = -dir;
            PatternCatalog.RotationDegrees = Mathf.Repeat(PatternCatalog.RotationDegrees + dir * step, 360f);
        }
    }

    private void UpdatePreview(SprayPaintItem spray)
    {
        if (!PatternCatalog.IsPatternStamp(PatternCatalog.Selected))
        {
            StampSpawner.HidePreview();
            return;
        }

        if (!TryRaycastSpray(spray, out var hit, out var forward))
        {
            StampSpawner.HidePreview();
            return;
        }

        var player = GameNetworkManager.Instance?.localPlayerController;
        var inElevator = player != null && (player.isInElevator || (StartOfRound.Instance != null && StartOfRound.Instance.inShipPhase));
        var parent = StampSpawner.ResolveParent(hit, inElevator);
        var def = PatternCatalog.Get(PatternCatalog.Selected);

        StampSpawner.PlaceStamp(
            PatternCatalog.Selected,
            hit.point,
            forward,
            hit.normal,
            PatternCatalog.RotationDegrees,
            Plugin.StampSize.Value,
            def.Tint,
            parent,
            isPreview: true);
    }

    /// <summary>Called from ItemActivate patch when a pattern stamp should fire.</summary>
    public bool TryApplySelectedStamp(SprayPaintItem spray)
    {
        if (!Plugin.Enabled.Value)
            return false;
        if (spray.isWeedKillerSprayBottle)
            return false;
        if (!PatternCatalog.IsPatternStamp(PatternCatalog.Selected))
            return false;
        if (_radial != null && _radial.IsOpen)
            return false;
        if (_stampCooldownLeft > 0f)
            return false;

        if (spray.sprayCanTank <= 0f || spray.sprayCanShakeMeter <= 0f)
        {
            spray.PlayCanEmptyEffect(spray.sprayCanTank <= 0f);
            return true; // consumed activate (don't free-paint)
        }

        if (!TryRaycastSpray(spray, out var hit, out var forward))
        {
            Plugin.V("Stamp raycast miss");
            return true; // still block free-paint while pattern selected
        }

        var player = GameNetworkManager.Instance?.localPlayerController;
        var inElevator = player != null && (player.isInElevator || (StartOfRound.Instance != null && StartOfRound.Instance.inShipPhase));
        var parent = StampSpawner.ResolveParent(hit, inElevator);
        var def = PatternCatalog.Get(PatternCatalog.Selected);
        var size = Plugin.StampSize.Value;
        var rot = PatternCatalog.RotationDegrees;

        // Local apply
        StampSpawner.PlaceStamp(
            PatternCatalog.Selected,
            hit.point,
            forward,
            hit.normal,
            rot,
            size,
            def.Tint,
            parent,
            isPreview: false);

        SprayNet.BroadcastStamp(
            PatternCatalog.Selected,
            hit.point,
            forward,
            hit.normal,
            rot,
            size,
            def.Tint);

        spray.sprayCanTank = Mathf.Max(0f, spray.sprayCanTank - Plugin.StampTankCost.Value);
        spray.sprayCanShakeMeter = Mathf.Max(0f, spray.sprayCanShakeMeter - Plugin.StampShakeCost.Value);
        _stampCooldownLeft = Plugin.StampCooldown.Value;

        // Short feedback without entering continuous free-paint.
        try
        {
            if (spray.sprayAudio != null && spray.sprayStart != null)
                spray.sprayAudio.PlayOneShot(spray.sprayStart);
            if (spray.sprayParticle != null)
            {
                spray.sprayParticle.Play(true);
                // stop shortly via invoke
                CancelInvoke(nameof(StopParticleFeedback));
                Invoke(nameof(StopParticleFeedback), 0.15f);
                _particleSpray = spray;
            }
        }
        catch
        {
            // ignored
        }

        Plugin.V($"Stamped {def.Label} at {hit.point} rot={rot:0}");
        return true;
    }

    private SprayPaintItem? _particleSpray;

    private void StopParticleFeedback()
    {
        try
        {
            if (_particleSpray != null && _particleSpray.sprayParticle != null)
                _particleSpray.sprayParticle.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
        catch
        {
            // ignored
        }
        _particleSpray = null;
    }

    public bool IsRadialOpen => _radial != null && _radial.IsOpen;

    public static SprayPaintItem? GetLocalSprayCan()
    {
        var player = GameNetworkManager.Instance?.localPlayerController;
        if (player == null || !player.isPlayerControlled)
            return null;
        return player.currentlyHeldObjectServer as SprayPaintItem;
    }

    public static bool TryRaycastSpray(SprayPaintItem spray, out RaycastHit hit, out Vector3 forward)
    {
        hit = default;
        forward = Vector3.forward;
        var player = GameNetworkManager.Instance?.localPlayerController;
        if (player == null || player.gameplayCamera == null)
            return false;

        var cam = player.gameplayCamera.transform;
        forward = cam.forward;
        // Match vanilla TrySpraying / AddSprayPaintLocal origin bias.
        var sprayPos = cam.position + cam.forward * 1f;
        var mask = spray.sprayPaintMask != 0 ? spray.sprayPaintMask : 605030721;
        return Physics.Raycast(sprayPos, forward, out hit, 7f, mask, QueryTriggerInteraction.Ignore);
    }
}
