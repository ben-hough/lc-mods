using System;
using UnityEngine;

namespace WeedKillerRefill;

/// <summary>Backup Update loop (also driven from PlayerControllerB.Update Harmony patch).</summary>
internal sealed class WeedKillerRefillBehaviour : MonoBehaviour
{
    private void Update() => RefillController.Tick("Behaviour.Update");
}
