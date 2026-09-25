using System;
using System.Collections;
using System.Reflection;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;

namespace MapClickCodes;

/// <summary>
/// Host/local activate path that mirrors Terminal.CallFunctionInAccessibleTerminalObject
/// (all TAOs with that objectCode) + PlayBroadcastCodeEffect, without double-toggling doors.
/// Vanilla CallFunctionFromTerminal only invokes terminalCodeEvent when !inCooldown (silent no-op otherwise).
/// </summary>
internal static class TerminalCodeActivation
{
    private static FieldInfo? _unityCallsField;
    private static FieldInfo? _runtimeCallsField;
    private static bool _listenerReflectTried;

    /// <summary>
    /// Activate every TerminalAccessibleObject with the matched objectCode the way the ship
    /// terminal would (CallFunctionInAccessibleTerminalObject), plus broadcast SFX.
    /// Returns true only when CallFunctionFromTerminal was invoked on at least one non-cooling TAO.
    /// </summary>
    public static bool Activate(TerminalAccessibleObject matched, string source, float score = -1f)
    {
        if (matched == null)
            return false;

        string code = matched.objectCode ?? "";
        if (string.IsNullOrEmpty(code))
        {
            Plugin.Log.LogWarning($"MapClick activate skipped: empty objectCode on '{matched.gameObject.name}' via={source}.");
            return false;
        }

        var objs = UnityEngine.Object.FindObjectsOfType<TerminalAccessibleObject>();
        int matchCount = 0;
        int readyCount = 0;
        for (int i = 0; i < objs.Length; i++)
        {
            var tao = objs[i];
            if (tao == null || !string.Equals(tao.objectCode, code, StringComparison.Ordinal))
                continue;
            matchCount++;
            if (!tao.inCooldown)
                readyCount++;
        }

        string scorePart = score >= 0f ? $" score={score:F4}" : "";
        Plugin.V(
            $"Activate prep '{code}' via={source}{scorePart} matchTAO={matchCount} ready={readyCount} isBigDoor={matched.isBigDoor}");

        if (readyCount == 0)
        {
            Plugin.Log.LogWarning(
                $"Terminal code '{code}' inCooldown on all {matchCount} TAO(s) — CallFunctionFromTerminal would silently no-op; not claiming success.");

            if (matched.isBigDoor && matched.isPoweredOn)
            {
                Plugin.Log.LogInfo($"inCooldown door fallback: SetDoorToggleLocalClient for '{code}'.");
                try
                {
                    matched.SetDoorToggleLocalClient();
                }
                catch (Exception ex)
                {
                    Plugin.Log.LogWarning($"SetDoorToggleLocalClient failed for '{code}': {ex.Message}");
                }
            }

            TryPlayBroadcastEffect();
            TryShowTip("Map code", $"Cooldown: {code}");
            return false;
        }

        bool usedTerminalApi = false;
        int invoked = 0;

        try
        {
            var terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
            if (terminal != null)
            {
                // Vanilla: all matching objectCodes + PlayBroadcastCodeEffect inside.
                terminal.CallFunctionInAccessibleTerminalObject(code);
                usedTerminalApi = true;
                invoked = readyCount;
            }
            else
            {
                for (int i = 0; i < objs.Length; i++)
                {
                    var tao = objs[i];
                    if (tao == null || !string.Equals(tao.objectCode, code, StringComparison.Ordinal))
                        continue;
                    if (tao.inCooldown)
                        continue;
                    try
                    {
                        tao.CallFunctionFromTerminal();
                        invoked++;
                    }
                    catch (Exception ex)
                    {
                        Plugin.Log.LogWarning($"CallFunctionFromTerminal failed for '{code}' on '{tao.gameObject.name}': {ex.Message}");
                    }
                }

                TryPlayBroadcastEffect();
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Activate CallFunction path failed for '{code}': {ex.Message}");
            return false;
        }

        // Door fallback only when UnityEvent has zero listeners and we did not use the terminal API
        // (terminal API already invoked CallFunction on all matches — avoid double-toggle).
        int listeners = CountEventListeners(matched.terminalCodeEvent);
        Plugin.V($"terminalCodeEvent listeners={listeners} on '{matched.gameObject.name}'");
        if (!usedTerminalApi && listeners == 0 && matched.isBigDoor && matched.isPoweredOn)
        {
            Plugin.Log.LogWarning(
                $"terminalCodeEvent has 0 listeners on '{code}' — SetDoorToggleLocalClient fallback.");
            try
            {
                matched.SetDoorToggleLocalClient();
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"SetDoorToggleLocalClient failed for '{code}': {ex.Message}");
            }
        }

        if (invoked <= 0)
        {
            Plugin.Log.LogWarning($"No CallFunctionFromTerminal invoke for '{code}' via={source}.");
            return false;
        }

        // Info only when CallFunction actually ran (never claim success on miss / cooldown).
        Plugin.Log.LogInfo(
            $"CallFunctionFromTerminal ran for '{code}' on {invoked}/{matchCount} TAO(s) via={source}{scorePart}"
            + (usedTerminalApi ? " (Terminal.CallFunctionInAccessibleTerminalObject)" : ""));
        TryShowTip("Map code", $"Activated {code}");
        return true;
    }

    internal static void TryPlayBroadcastEffect()
    {
        try
        {
            var terminal = UnityEngine.Object.FindObjectOfType<Terminal>();
            if (terminal == null)
                return;

            try
            {
                terminal.PlayBroadcastCodeEffect();
                return;
            }
            catch (Exception)
            {
                /* fall through to manual mirror */
            }

            if (terminal.codeBroadcastAnimator != null)
                terminal.codeBroadcastAnimator.SetTrigger("display");

            if (terminal.terminalAudio != null && terminal.codeBroadcastSFX != null)
                terminal.terminalAudio.PlayOneShot(terminal.codeBroadcastSFX, 1f);
        }
        catch (Exception ex)
        {
            Plugin.V($"Broadcast SFX skipped: {ex.Message}");
        }
    }

    private static void TryShowTip(string header, string body)
    {
        try
        {
            if (HUDManager.Instance == null)
                return;
            HUDManager.Instance.DisplayTip(header, body);
        }
        catch (Exception ex)
        {
            Plugin.V($"Activate tip skipped: {ex.Message}");
        }
    }

    internal static int CountEventListeners(UnityEventBase? evt)
    {
        if (evt == null)
            return 0;

        int n = evt.GetPersistentEventCount();
        try
        {
            EnsureListenerReflect();
            if (_unityCallsField == null)
                return n;

            object? calls = _unityCallsField.GetValue(evt);
            if (calls == null)
                return n;

            if (_runtimeCallsField == null)
                _runtimeCallsField = AccessTools.Field(calls.GetType(), "m_RuntimeCalls");

            if (_runtimeCallsField == null)
                return n;

            object? runtime = _runtimeCallsField.GetValue(calls);
            if (runtime is ICollection col)
                n += col.Count;
            else if (runtime is IList list)
                n += list.Count;
        }
        catch (Exception ex)
        {
            Plugin.V($"Listener count reflect failed: {ex.Message}");
        }

        return n;
    }

    private static void EnsureListenerReflect()
    {
        if (_listenerReflectTried)
            return;
        _listenerReflectTried = true;
        _unityCallsField = AccessTools.Field(typeof(UnityEventBase), "m_Calls");
    }
}
