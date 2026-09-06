using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using GameNetcodeStuff;
using HarmonyLib;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace ShipDoorTerminal;

internal static class ManualPatches
{
    internal static void Apply(Harmony harmony)
    {
        try
        {
            void Prefix(Type type, string name, Type patchType, string method, Type[]? args = null)
            {
                var m = args == null ? AccessTools.Method(type, name) : AccessTools.Method(type, name, args);
                Plugin.Log.LogInfo($"Resolve {type.Name}.{name} => {(m == null ? "NULL" : (m.IsPublic ? "public" : "nonpublic"))}");
                if (m == null) return;
                harmony.Patch(m, prefix: new HarmonyMethod(patchType, method));
                Plugin.Log.LogInfo($"Patched {type.Name}.{name} prefix {method}");
            }

            void Postfix(Type type, string name, Type patchType, string method)
            {
                var m = AccessTools.Method(type, name);
                Plugin.Log.LogInfo($"Resolve {type.Name}.{name} => {(m == null ? "NULL" : "ok")}");
                if (m == null) return;
                harmony.Patch(m, postfix: new HarmonyMethod(patchType, method));
                Plugin.Log.LogInfo($"Patched {type.Name}.{name} postfix {method}");
            }

            Prefix(typeof(Terminal), "OnSubmit", typeof(OnSubmitPatch), nameof(OnSubmitPatch.Prefix));
            Prefix(typeof(Terminal), "ParseWord", typeof(ParseWordPatch), nameof(ParseWordPatch.Prefix), new[] { typeof(string), typeof(int) });
            Prefix(typeof(Terminal), "ParsePlayerSentence", typeof(ParseSentencePatch), nameof(ParseSentencePatch.Prefix));
            Prefix(typeof(Terminal), "LoadNewNode", typeof(LoadNewNodePatch), nameof(LoadNewNodePatch.Prefix));

            Postfix(typeof(Terminal), "Awake", typeof(TerminalLifecyclePatch), nameof(TerminalLifecyclePatch.AwakePostfix));
            Postfix(typeof(Terminal), "Start", typeof(TerminalLifecyclePatch), nameof(TerminalLifecyclePatch.StartPostfix));
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"ManualPatches.Apply failed: {ex}");
        }
    }
}

internal static class TerminalInput
{
    internal static string? LastSubmitted;

    internal static string Normalize(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return "";
        var s = raw.ToLowerInvariant();
        s = Regex.Replace(s, @"[^a-z0-9\s]", " ");
        s = Regex.Replace(s, @"\s+", " ").Trim();
        return s;
    }

    internal static string Extract(Terminal terminal)
    {
        try
        {
            var text = terminal.screenText != null ? terminal.screenText.text : null;
            if (string.IsNullOrEmpty(text)) return "";
            if (terminal.textAdded > 0 && text.Length >= terminal.textAdded)
                return Normalize(text.Substring(text.Length - terminal.textAdded));
            var idx = text.LastIndexOf('\n');
            var last = idx >= 0 ? text.Substring(idx + 1) : text;
            return Normalize(last.Trim().TrimStart('>', ' '));
        }
        catch { return ""; }
    }

    private static readonly HashSet<string> Commands = new(StringComparer.Ordinal)
    {
        "door", "doors",
        "opendoor", "open door", "door open", "dooropen",
        "closedoor", "close door", "door close", "doorclose",
    };

    internal static bool IsDoorCommand(string input) => Commands.Contains(input);
}

internal static class OnSubmitPatch
{
    public static bool Prefix(Terminal __instance)
    {
        try
        {
            var input = TerminalInput.Extract(__instance);
            TerminalInput.LastSubmitted = input;
            Plugin.Log.LogInfo($"[OnSubmit] captured='{input}' enabled={Plugin.Enabled?.Value}");
            if (Plugin.Enabled == null || !Plugin.Enabled.Value) return true;

            if (!TerminalInput.IsDoorCommand(input)) return true;

            var response = DoorActions.Run(input);
            Plugin.Log.LogInfo($"[OnSubmit] handled '{input}'");
            __instance.LoadNewNode(DoorActions.CreateDisplayNode(response));
            // Vanilla OnSubmit activates the input field after LoadNewNode; returning false skips it.
            DoorActions.ReadyForNextCommand(__instance);
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[OnSubmit] {ex}");
            return true;
        }
    }
}

internal static class ParseWordPatch
{
    public static bool Prefix(string playerWord, int specificityRequired, ref TerminalKeyword __result)
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value) return true;
        try
        {
            var word = TerminalInput.Normalize(playerWord);
            Plugin.V($"[ParseWord] '{playerWord}' -> '{word}' spec={specificityRequired}");
            if (!TerminalInput.IsDoorCommand(word)) return true;

            __result = DoorActions.GetKeyword(word);
            Plugin.Log.LogInfo($"[ParseWord] returning door keyword for '{word}'");
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[ParseWord] {ex}");
            return true;
        }
    }
}

internal static class ParseSentencePatch
{
    public static bool Prefix(Terminal __instance, ref TerminalNode __result)
    {
        if (Plugin.Enabled == null || !Plugin.Enabled.Value) return true;
        try
        {
            var input = TerminalInput.LastSubmitted;
            if (string.IsNullOrEmpty(input))
                input = TerminalInput.Extract(__instance);
            Plugin.V($"[ParseSentence] input='{input}'");
            if (string.IsNullOrEmpty(input) || !TerminalInput.IsDoorCommand(input))
                return true;

            var response = DoorActions.Run(input);
            __result = DoorActions.CreateDisplayNode(response);
            TerminalInput.LastSubmitted = null;
            Plugin.Log.LogInfo($"[ParseSentence] handled '{input}'");
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[ParseSentence] {ex}");
            return true;
        }
    }
}

internal static class LoadNewNodePatch
{
    private static int _reentry;

    public static void Prefix(TerminalNode node)
    {
        if (node == null)
            return;

        if (_reentry > 0)
            return;

        try
        {
            if (Plugin.Enabled == null || !Plugin.Enabled.Value)
                return;

            if (!DoorActions.TryGetCommandForNode(node, out var cmd))
            {
                Plugin.V($"[LoadNewNode] unrelated");
                return;
            }

            Plugin.Log.LogInfo($"[LoadNewNode] door keyword node for '{cmd}' — running action");
            _reentry++;
            try
            {
                var response = DoorActions.Run(cmd);
                var body = response.EndsWith("\n") ? response : response + "\n";
                if (!body.EndsWith("\n\n"))
                    body += "\n";
                node.displayText = body;
                node.clearPreviousText = true;
                node.acceptAnything = false;
                node.overrideOptions = false;
            }
            finally
            {
                _reentry--;
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[LoadNewNode] {ex}");
        }
    }
}

internal static class TerminalLifecyclePatch
{
    public static void AwakePostfix(Terminal __instance)
    {
        Plugin.Log.LogInfo("[Terminal.Awake] postfix hit");
        DoorActions.EnsureKeywordsRegistered(__instance);
        DoorActions.EnsureHelpText(__instance);
    }

    public static void StartPostfix(Terminal __instance)
    {
        Plugin.Log.LogInfo("[Terminal.Start] postfix hit");
        DoorActions.EnsureKeywordsRegistered(__instance);
        DoorActions.EnsureHelpText(__instance);
    }
}

internal static class DoorActions
{
    private static readonly Dictionary<string, TerminalKeyword> Keywords = new();
    private static readonly Dictionary<TerminalNode, string> NodeCommands = new();
    private static bool _registered;
    private static bool _helpInjected;

    // Legacy header from older builds; stripped on inject.
    private const string HelpMarker = "[ShipDoorTerminal]";
    // Fingerprint so we don't double-inject (visible command, no mod banner).
    private const string HelpFingerprint = ">DOOR";
    private const string HelpBlock =
        ">DOOR\n" +
        "Toggle the ship hangar doors.\n" +
        "Also: OPENDOOR / CLOSEDOOR\n\n";

    internal static TerminalNode CreateDisplayNode(string text)
    {
        var node = ScriptableObject.CreateInstance<TerminalNode>();
        var body = text ?? "";
        if (!body.EndsWith("\n"))
            body += "\n";
        if (!body.EndsWith("\n\n"))
            body += "\n";
        node.displayText = body;
        node.clearPreviousText = true;
        node.maxCharactersToType = 80;
        node.acceptAnything = false;
        node.overrideOptions = false;
        return node;
    }

    internal static void ReadyForNextCommand(Terminal terminal)
    {
        try
        {
            if (terminal?.screenText == null)
                return;
            terminal.screenText.ActivateInputField();
            ((Selectable)terminal.screenText).Select();
            var len = terminal.screenText.text != null ? terminal.screenText.text.Length : 0;
            terminal.screenText.caretPosition = len;
            terminal.screenText.selectionAnchorPosition = len;
            terminal.screenText.selectionFocusPosition = len;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[ReadyForNextCommand] {ex.Message}");
        }
    }

    internal static bool TryGetCommandForNode(TerminalNode node, out string cmd)
    {
        if (NodeCommands.TryGetValue(node, out cmd!))
            return true;

        var t = node.displayText ?? "";
        if (t.StartsWith("Ship door command:", StringComparison.OrdinalIgnoreCase))
        {
            cmd = TerminalInput.Normalize(t.Substring("Ship door command:".Length));
            return TerminalInput.IsDoorCommand(cmd);
        }

        cmd = "";
        return false;
    }

    internal static TerminalKeyword GetKeyword(string word)
    {
        var key = word.Contains(" ") ? word.Split(' ')[0] : word;
        if (Keywords.TryGetValue(key, out var existing) && existing != null)
            return existing;

        EnsureKeyword(key);
        return Keywords[key];
    }

    private static void EnsureKeyword(string word)
    {
        if (Keywords.ContainsKey(word)) return;

        var node = CreateDisplayNode($"Ship door command: {word}\n");
        NodeCommands[node] = word;

        var keyword = ScriptableObject.CreateInstance<TerminalKeyword>();
        keyword.word = word;
        keyword.isVerb = false;
        keyword.specialKeywordResult = node;
        Keywords[word] = keyword;
    }

    internal static void EnsureKeywordsRegistered(Terminal terminal)
    {
        try
        {
            if (terminal?.terminalNodes?.allKeywords == null)
            {
                Plugin.Log.LogInfo("Keyword register skipped: allKeywords null");
                return;
            }

            EnsureKeyword("door");
            EnsureKeyword("doors");
            EnsureKeyword("opendoor");
            EnsureKeyword("closedoor");
            EnsureKeyword("dooropen");
            EnsureKeyword("doorclose");

            if (_registered)
            {
                Plugin.V("Keywords already registered");
                return;
            }

            var list = new List<TerminalKeyword>(terminal.terminalNodes.allKeywords);
            foreach (var kv in Keywords)
            {
                if (!list.Exists(k => k != null && k.word == kv.Key))
                    list.Add(kv.Value);
            }

            terminal.terminalNodes.allKeywords = list.ToArray();
            _registered = true;
            Plugin.Log.LogInfo($"Registered door keywords (allKeywords={list.Count}, trackedNodes={NodeCommands.Count})");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"EnsureKeywordsRegistered failed: {ex.Message}");
        }
    }

    internal static void EnsureHelpText(Terminal terminal)
    {
        try
        {
            if (terminal?.terminalNodes?.specialNodes == null)
                return;

            var injected = 0;

            // Walk specialNodes + help/other keyword pages; only inject into real command catalogs.
            for (var i = 0; i < terminal.terminalNodes.specialNodes.Count; i++)
            {
                if (TryApplyHelpToNode(terminal.terminalNodes.specialNodes[i], $"specialNodes[{i}]"))
                    injected++;
            }

            var keywords = terminal.terminalNodes.allKeywords;
            if (keywords != null)
            {
                foreach (var kw in keywords)
                {
                    if (kw == null || string.IsNullOrEmpty(kw.word))
                        continue;
                    var word = kw.word;
                    var isHelpish =
                        string.Equals(word, "help", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(word, "other", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(word, "others", StringComparison.OrdinalIgnoreCase);
                    if (!isHelpish)
                        continue;
                    if (TryApplyHelpToNode(kw.specialKeywordResult, $"keyword:{word}"))
                        injected++;
                }
            }

            if (injected > 0)
                _helpInjected = true;
            else if (!_helpInjected)
                Plugin.Log.LogInfo("[Help] no command-list page found yet");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Help] {ex.Message}");
        }
    }

    /// <summary>
    /// True only for the printed command catalog (STORE/BESTIARY/...), not the first-boot tip.
    /// </summary>
    private static bool IsCommandListPage(string page)
    {
        if (string.IsNullOrEmpty(page))
            return false;

        var hasStore = page.IndexOf(">STORE", StringComparison.OrdinalIgnoreCase) >= 0;
        var hasBestiary = page.IndexOf(">BESTIARY", StringComparison.OrdinalIgnoreCase) >= 0;
        var hasStorage = page.IndexOf(">STORAGE", StringComparison.OrdinalIgnoreCase) >= 0;
        var hasOther = page.IndexOf(">OTHER", StringComparison.OrdinalIgnoreCase) >= 0;

        // First terminal view is a short tip ("Welcome" / "Type help") without the catalog.
        if (!(hasStore && (hasBestiary || hasStorage || hasOther)))
            return false;

        return true;
    }

    private static bool TryApplyHelpToNode(TerminalNode? node, string label)
    {
        if (node == null || string.IsNullOrEmpty(node.displayText))
            return false;

        var page = StripLegacyHelpHeader(node.displayText);

        if (!IsCommandListPage(page))
        {
            // Peel DOOR off welcome / tip pages if an older build put it there.
            var cleaned = StripDoorHelp(page);
            if (cleaned != node.displayText)
            {
                node.displayText = cleaned;
                Plugin.Log.LogInfo($"[Help] stripped door docs from non-list {label}");
            }
            return false;
        }

        if (page.IndexOf(HelpFingerprint, StringComparison.OrdinalIgnoreCase) >= 0)
        {
            if (page != node.displayText)
                node.displayText = page;
            return false;
        }

        node.displayText = InjectHelp(page);
        Plugin.Log.LogInfo($"[Help] injected door docs into {label}");
        return true;
    }

    private static string StripLegacyHelpHeader(string page)
    {
        if (string.IsNullOrEmpty(page) || page.IndexOf(HelpMarker, StringComparison.Ordinal) < 0)
            return page;

        // Remove "[ShipDoorTerminal]" and the blank line that used to follow it.
        var cleaned = page.Replace(HelpMarker + "\n\n", "")
                          .Replace(HelpMarker + "\n", "")
                          .Replace(HelpMarker, "");
        return cleaned;
    }

    private static string StripDoorHelp(string page)
    {
        if (string.IsNullOrEmpty(page) || page.IndexOf(HelpFingerprint, StringComparison.OrdinalIgnoreCase) < 0)
            return page;

        // Remove the exact HelpBlock if present; also a looser >DOOR ... blank-line chunk.
        if (page.Contains(HelpBlock))
            return page.Replace(HelpBlock, "");

        var idx = page.IndexOf(HelpFingerprint, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
            return page;

        var end = page.IndexOf("\n\n", idx);
        if (end < 0)
            end = page.Length;
        else
            end += 2;

        // Include a preceding blank line if we inserted one.
        var start = idx;
        if (start >= 2 && page[start - 2] == '\n' && page[start - 1] == '\n')
            start -= 2;
        else if (start >= 1 && page[start - 1] == '\n')
            start -= 1;

        return page.Substring(0, start) + page.Substring(end);
    }

    private static string InjectHelp(string page)
    {
        page = StripLegacyHelpHeader(page);

        var otherIdx = page.IndexOf(">OTHER", StringComparison.OrdinalIgnoreCase);
        if (otherIdx < 0)
            otherIdx = page.IndexOf("OTHER", StringComparison.OrdinalIgnoreCase);

        // Insert DOOR as another main-list entry after OTHER (vanilla blank-line rhythm).
        if (otherIdx >= 0)
        {
            var after = page.IndexOf("\n\n", otherIdx);
            if (after > otherIdx)
            {
                var insertAt = after + 2;
                return page.Substring(0, insertAt) + HelpBlock + page.Substring(insertAt);
            }
        }

        if (!page.EndsWith("\n"))
            page += "\n";
        if (!page.EndsWith("\n\n"))
            page += "\n";
        return page + HelpBlock;
    }

    internal static string Run(string input)
    {
        return input switch
        {
            "door" or "doors" => Toggle(),
            "opendoor" or "open door" or "door open" or "dooropen" => Set(wantClosed: false),
            "closedoor" or "close door" or "door close" or "doorclose" => Set(wantClosed: true),
            _ => "Unknown door command.\n",
        };
    }

    private static string Toggle()
    {
        var start = StartOfRound.Instance;
        if (start == null) return "Ship doors unavailable.\n";
        return Set(wantClosed: !start.hangarDoorsClosed);
    }

    private static string Set(bool wantClosed)
    {
        var start = StartOfRound.Instance;
        if (start == null) return "Ship doors unavailable.\n";
        if (start.inShipPhase)
            return "Ship doors unavailable while in orbit.\n";

        var door = UnityEngine.Object.FindObjectOfType<HangarShipDoor>();
        Plugin.Log.LogInfo(
            $"[Door] wantClosed={wantClosed}, hangarDoorsClosed={start.hangarDoorsClosed}, " +
            $"shipDoorsEnabled={start.shipDoorsEnabled}, doorNull={door == null}, " +
            $"overheated={door?.overheated}, buttonsEnabled={door?.buttonsEnabled}, doorPower={door?.doorPower}");

        if (door != null && door.overheated)
            return "Ship doors are overheated and cannot be toggled.\n";

        if (start.hangarDoorsClosed == wantClosed)
            return wantClosed ? "Ship doors are already closed.\n" : "Ship doors are already open.\n";

        var buttonName = wantClosed ? "StopButton" : "StartButton";
        if (TryPressDoorButton(buttonName))
        {
            Plugin.Log.LogInfo($"[Door] pressed {buttonName}; hangarDoorsClosed now={start.hangarDoorsClosed}");
            return wantClosed ? "Closing ship doors...\n" : "Opening ship doors...\n";
        }

        Plugin.Log.LogWarning($"[Door] {buttonName} interact failed — trying HangarShipDoor + ServerRpc fallback");

        try
        {
            if (door != null)
            {
                if (wantClosed) door.SetDoorClosed();
                else door.SetDoorOpen();
                door.PlayDoorAnimation(wantClosed);
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Door] HangarShipDoor fallback: {ex.Message}");
        }

        try
        {
            start.SetDoorsClosedServerRpc(wantClosed);
            Plugin.Log.LogInfo("[Door] called SetDoorsClosedServerRpc");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Door] SetDoorsClosedServerRpc: {ex.Message}");
            try { start.SetShipDoorsClosed(wantClosed); }
            catch (Exception ex2) { Plugin.Log.LogWarning($"[Door] SetShipDoorsClosed: {ex2.Message}"); }
        }

        Plugin.Log.LogInfo($"[Door] after fallback: hangarDoorsClosed={start.hangarDoorsClosed}");
        return wantClosed ? "Closing ship doors...\n" : "Opening ship doors...\n";
    }

    private static bool TryPressDoorButton(string buttonName)
    {
        try
        {
            var buttonObject = GameObject.Find(buttonName);
            if (buttonObject == null)
            {
                buttonObject = GameObject.Find($"Environment/HangarShip/AnimatedShipDoor/{buttonName}")
                    ?? GameObject.Find($"HangarShip/{buttonName}");
            }

            if (buttonObject == null)
            {
                Plugin.Log.LogWarning($"[Door] GameObject.Find('{buttonName}') returned null");
                return false;
            }

            var interactTrigger = buttonObject.GetComponentInChildren<InteractTrigger>();
            if (interactTrigger == null)
            {
                Plugin.Log.LogWarning($"[Door] no InteractTrigger on {buttonName}");
                return false;
            }

            var player = GameNetworkManager.Instance?.localPlayerController;
            if (player == null)
            {
                Plugin.Log.LogWarning("[Door] no local player for button interact");
                return false;
            }

            if (interactTrigger.onInteract is UnityEvent<PlayerControllerB> onInteractEvent)
            {
                onInteractEvent.Invoke(player);
                Plugin.Log.LogInfo($"[Door] invoked onInteract on {buttonName}");
                return true;
            }

            interactTrigger.Interact(player.transform);
            Plugin.Log.LogInfo($"[Door] called Interact() on {buttonName}");
            return true;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Door] TryPressDoorButton({buttonName}): {ex}");
            return false;
        }
    }
}
