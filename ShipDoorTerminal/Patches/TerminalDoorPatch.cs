using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace ShipDoorTerminal.Patches;

/// <summary>
/// Capture terminal input on submit, then intercept ParsePlayerSentence.
/// v81 often clears/changes textAdded before parse, so we cannot rely on it alone.
/// </summary>
internal static class TerminalInputCapture
{
    internal static string? LastSubmitted;

    internal static string Normalize(string? raw)
    {
        if (string.IsNullOrEmpty(raw))
            return "";

        var s = raw.ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9\s]", " ");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    internal static string ExtractFromScreen(Terminal terminal)
    {
        try
        {
            var text = terminal.screenText != null ? terminal.screenText.text : null;
            if (string.IsNullOrEmpty(text))
                return "";

            if (terminal.textAdded > 0 && text.Length >= terminal.textAdded)
                return Normalize(text.Substring(text.Length - terminal.textAdded));

            // Fallback: last line / after last prompt-like break.
            var idx = text.LastIndexOf('\n');
            var last = idx >= 0 ? text.Substring(idx + 1) : text;
            last = last.Trim().TrimStart('>', ' ');
            return Normalize(last);
        }
        catch
        {
            return "";
        }
    }
}

[HarmonyPatch(typeof(Terminal), nameof(Terminal.OnSubmit))]
internal static class TerminalOnSubmitPatch
{
    private static void Prefix(Terminal __instance)
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return;

        TerminalInputCapture.LastSubmitted = TerminalInputCapture.ExtractFromScreen(__instance);
        if (!string.IsNullOrEmpty(TerminalInputCapture.LastSubmitted))
            Plugin.Log.LogDebug($"Terminal submit captured: '{TerminalInputCapture.LastSubmitted}'");
    }
}

[HarmonyPatch(typeof(Terminal), "ParsePlayerSentence")]
internal static class TerminalDoorPatch
{
    private static readonly HashSet<string> DoorCommands = new(StringComparer.Ordinal)
    {
        "door", "doors",
        "opendoor", "open door", "door open",
        "closedoor", "close door", "door close",
    };

    private static bool Prefix(Terminal __instance, ref TerminalNode __result)
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return true;

        try
        {
            var input = TerminalInputCapture.LastSubmitted;
            if (string.IsNullOrEmpty(input))
                input = TerminalInputCapture.ExtractFromScreen(__instance);

            TerminalInputCapture.LastSubmitted = null;

            if (string.IsNullOrEmpty(input) || !DoorCommands.Contains(input))
                return true;

            Plugin.Log.LogInfo($"Handling ship-door command: '{input}'");

            string response = input switch
            {
                "door" or "doors" => DoorCommandsUtil.ToggleDoor(),
                "opendoor" or "open door" or "door open" => DoorCommandsUtil.SetDoor(closed: false),
                "closedoor" or "close door" or "door close" => DoorCommandsUtil.SetDoor(closed: true),
                _ => "Unknown door command.\n",
            };

            __result = DoorCommandsUtil.CreateNode(response);
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Ship door terminal command failed: {ex.Message}");
            return true;
        }
    }
}

/// <summary>
/// Also inject keywords so vanilla ParseWord can resolve them if sentence parse misses.
/// Actions still run via ParsePlayerSentence intercept above when possible.
/// </summary>
[HarmonyPatch(typeof(Terminal), "Awake")]
internal static class TerminalAwakePatch
{
    private static void Postfix(Terminal __instance)
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return;

        try
        {
            DoorKeywordRegistry.EnsureRegistered(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to register door keywords: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(Terminal), "Start")]
internal static class TerminalStartPatch
{
    private static void Postfix(Terminal __instance)
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return;

        try
        {
            DoorKeywordRegistry.EnsureRegistered(__instance);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Failed to register door keywords on Start: {ex.Message}");
        }
    }
}

internal static class DoorKeywordRegistry
{
    private static bool _done;

    internal static void EnsureRegistered(Terminal terminal)
    {
        if (_done || terminal?.terminalNodes == null)
            return;

        var list = terminal.terminalNodes;
        if (list.allKeywords == null)
            return;

        var existing = new HashSet<string>(
            list.allKeywords.Where(k => k != null && k.word != null).Select(k => k.word.ToLowerInvariant()));

        var added = new List<TerminalKeyword>();
        void Add(string word, string display)
        {
            if (existing.Contains(word))
                return;

            var node = ScriptableObject.CreateInstance<TerminalNode>();
            node.displayText = display;
            node.clearPreviousText = true;
            node.maxCharactersToType = 40;

            var keyword = ScriptableObject.CreateInstance<TerminalKeyword>();
            keyword.word = word;
            keyword.isVerb = false;
            keyword.specialKeywordResult = node;
            added.Add(keyword);
            existing.Add(word);
        }

        // Keyword-only path shows text; ParsePlayerSentence still performs the door action when it matches.
        Add("door", "Toggling ship doors...\n");
        Add("doors", "Toggling ship doors...\n");
        Add("opendoor", "Opening ship doors...\n");
        Add("closedoor", "Closing ship doors...\n");

        if (added.Count == 0)
        {
            _done = true;
            return;
        }

        list.allKeywords = list.allKeywords.Concat(added).ToArray();
        _done = true;
        Plugin.Log.LogInfo($"Registered terminal keywords: {string.Join(", ", added.Select(k => k.word))}");
    }
}

internal static class DoorCommandsUtil
{
    internal static TerminalNode CreateNode(string text)
    {
        var node = ScriptableObject.CreateInstance<TerminalNode>();
        node.displayText = text.EndsWith("\n") ? text : text + "\n";
        node.clearPreviousText = true;
        node.maxCharactersToType = 50;
        return node;
    }

    internal static string ToggleDoor()
    {
        var start = StartOfRound.Instance;
        if (start == null)
            return "Ship doors unavailable.\n";

        // Allow in orbit too if doors object exists; otherwise require landed.
        var door = UnityEngine.Object.FindObjectOfType<HangarShipDoor>();
        if (door == null && !start.shipDoorsEnabled)
            return "Ship doors unavailable (not landed?).\n";

        return SetDoor(closed: !start.hangarDoorsClosed);
    }

    internal static string SetDoor(bool closed)
    {
        var start = StartOfRound.Instance;
        if (start == null)
            return "Ship doors unavailable.\n";

        var door = UnityEngine.Object.FindObjectOfType<HangarShipDoor>();
        if (door != null && door.overheated)
            return "Ship doors are overheated and cannot be toggled.\n";

        if (start.hangarDoorsClosed == closed)
            return closed ? "Ship doors are already closed.\n" : "Ship doors are already open.\n";

        try
        {
            if (door != null)
            {
                if (closed)
                    door.SetDoorClosed();
                else
                    door.SetDoorOpen();

                // Keep animator/network state in sync when available.
                door.PlayDoorAnimation(closed);
            }

            start.SetShipDoorsClosed(closed);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Door toggle failed, trying SetShipDoorsClosed only: {ex.Message}");
            try
            {
                start.SetShipDoorsClosed(closed);
            }
            catch (Exception ex2)
            {
                return $"Failed to toggle ship doors: {ex2.Message}\n";
            }
        }

        return closed ? "Closing ship doors...\n" : "Opening ship doors...\n";
    }
}
