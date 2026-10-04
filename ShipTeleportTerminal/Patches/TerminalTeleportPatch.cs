using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace ShipTeleportTerminal;

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
            var disc = AccessTools.Method(typeof(GameNetworkManager), "Disconnect");
            if (disc != null)
            {
                harmony.Patch(disc, prefix: new HarmonyMethod(typeof(HostModGateDisconnectPatch), "Prefix"));
                Plugin.Log.LogInfo("Patched GameNetworkManager.Disconnect for host gate reset");
            }
            var sor = AccessTools.Method(typeof(StartOfRound), "Start");
            if (sor != null)
            {
                harmony.Patch(sor, postfix: new HarmonyMethod(typeof(HostModGateStartPatch), "Postfix"));
                Plugin.Log.LogInfo("Patched StartOfRound.Start for host gate register");
            }

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

    // Do NOT include "teleporter" — that is the vanilla store buy noun.
    private static readonly HashSet<string> Commands = new(StringComparer.Ordinal)
    {
        "teleport", "tp", "beam", "ship teleport",
        "iteleport", "itp", "inverse teleport",
        "teleport inverse", "tp inverse",
    };

    internal static bool IsTeleportCommand(string input) => Commands.Contains(input);
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
            if (!HostModGate.FeaturesActive) return true;

            if (!TerminalInput.IsTeleportCommand(input)) return true;

            var response = TeleportActions.Run(input);
            Plugin.Log.LogInfo($"[OnSubmit] teleport handled '{input}'");
            __instance.LoadNewNode(TeleportActions.CreateDisplayNode(response));
            // Vanilla OnSubmit activates the input field after LoadNewNode; returning false skips it.
            TeleportActions.ReadyForNextCommand(__instance);
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
        if (!HostModGate.FeaturesActive) return true;
        try
        {
            var word = TerminalInput.Normalize(playerWord);
            Plugin.V($"[ParseWord] '{playerWord}' -> '{word}' spec={specificityRequired}");
            if (!TerminalInput.IsTeleportCommand(word)) return true;

            __result = TeleportActions.GetKeyword(word);
            Plugin.Log.LogInfo($"[ParseWord] returning teleport keyword for '{word}'");
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
        if (!HostModGate.FeaturesActive) return true;
        try
        {
            var input = TerminalInput.LastSubmitted;
            if (string.IsNullOrEmpty(input))
                input = TerminalInput.Extract(__instance);
            Plugin.V($"[ParseSentence] input='{input}'");
            if (string.IsNullOrEmpty(input) || !TerminalInput.IsTeleportCommand(input))
                return true;

            var response = TeleportActions.Run(input);
            __result = TeleportActions.CreateDisplayNode(response);
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
            if (!HostModGate.FeaturesActive)
                return;

            if (!TeleportActions.TryGetCommandForNode(node, out var cmd))
            {
                Plugin.V($"[LoadNewNode] unrelated");
                return;
            }

            Plugin.Log.LogInfo($"[LoadNewNode] teleport keyword node for '{cmd}' — running action");
            _reentry++;
            try
            {
                var response = TeleportActions.Run(cmd);
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
        TeleportActions.EnsureKeywordsRegistered(__instance);
        TeleportActions.EnsureHelpText(__instance);
    }

    public static void StartPostfix(Terminal __instance)
    {
        HostModGate.EnsureRegistered();
        Plugin.Log.LogInfo("[Terminal.Start] postfix hit");
        TeleportActions.EnsureKeywordsRegistered(__instance);
        TeleportActions.EnsureHelpText(__instance);
    }
}

internal static class TeleportActions
{
    private static readonly Dictionary<string, TerminalKeyword> Keywords = new();
    private static readonly Dictionary<TerminalNode, string> NodeCommands = new();
    private static bool _registered;
    private static bool _helpInjected;

    // Legacy header from older builds; stripped on inject.
    private const string HelpMarker = "[ShipTeleportTerminal]";
    // Fingerprint so we don't double-inject (visible command, no mod banner).
    private const string HelpFingerprint = ">TELEPORT";
    private const string HelpBlock =
        ">TELEPORT\n" +
        "Beam the radar-targeted player (same as the teleporter button).\n" +
        "Also: TP / BEAM / ITELEPORT / ITP (inverse)\n\n";

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
        if (t.StartsWith("Ship teleport command:", StringComparison.OrdinalIgnoreCase))
        {
            cmd = TerminalInput.Normalize(t.Substring("Ship teleport command:".Length));
            return TerminalInput.IsTeleportCommand(cmd);
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

        var node = CreateDisplayNode($"Ship teleport command: {word}\n");
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

            
            
            
            
            
            
            

            // Never register "teleporter" — conflicts with store buy.
            EnsureKeyword("teleport");
            EnsureKeyword("tp");
            EnsureKeyword("beam");
            EnsureKeyword("iteleport");
            EnsureKeyword("itp");

            if (_registered)
            {
                Plugin.V("Keywords already registered");
                return;
            }

            var list = new List<TerminalKeyword>(terminal.terminalNodes.allKeywords);
            foreach (var word in new[] { "teleport", "tp", "beam", "iteleport", "itp" })
            {
                if (!Keywords.TryGetValue(word, out var kw) || kw == null)
                    continue;
                if (!list.Exists(k => k != null && k.word == word))
                    list.Add(kw);
            }

            terminal.terminalNodes.allKeywords = list.ToArray();
            _registered = true;
            Plugin.Log.LogInfo($"Registered teleport keywords (allKeywords={list.Count}, published=teleport/tp/beam/iteleport/itp, trackedNodes={NodeCommands.Count})");
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
            // Peel TELEPORT off welcome / tip pages if an older build put it there.
            var cleaned = StripTeleportHelp(page);
            if (cleaned != node.displayText)
            {
                node.displayText = cleaned;
                Plugin.Log.LogInfo($"[Help] stripped teleport docs from non-list {label}");
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
        Plugin.Log.LogInfo($"[Help] injected teleport docs into {label}");
        return true;
    }

    private static string StripLegacyHelpHeader(string page)
    {
        if (string.IsNullOrEmpty(page) || page.IndexOf(HelpMarker, StringComparison.Ordinal) < 0)
            return page;

        var cleaned = page.Replace(HelpMarker + "\n\n", "")
                          .Replace(HelpMarker + "\n", "")
                          .Replace(HelpMarker, "");
        return cleaned;
    }

    private static string StripTeleportHelp(string page)
    {
        if (string.IsNullOrEmpty(page) || page.IndexOf(HelpFingerprint, StringComparison.OrdinalIgnoreCase) < 0)
            return page;

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

        // Insert TELEPORT as another main-list entry after OTHER (vanilla blank-line rhythm).
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
            "teleport" or "tp" or "beam" or "ship teleport"
                => Fire(inverse: false),
            "iteleport" or "itp" or "inverse teleport"
                or "teleport inverse" or "tp inverse"
                => Fire(inverse: true),
            _ => "Unknown teleport command.\n",
        };
    }

    private static ShipTeleporter? ResolveTeleporter(bool inverse)
    {
        try
        {
            var all = UnityEngine.Object.FindObjectsOfType<ShipTeleporter>();
            if (all == null || all.Length == 0)
                return null;

            ShipTeleporter? match = null;
            ShipTeleporter? fallback = null;
            foreach (var t in all)
            {
                if (t == null) continue;
                fallback ??= t;
                if (t.isInverseTeleporter == inverse)
                {
                    match = t;
                    break;
                }
            }
            return match ?? (inverse ? null : fallback);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Teleport] FindObjectsOfType: {ex.Message}");
            return null;
        }
    }

    private static string Fire(bool inverse)
    {
        if (inverse && Plugin.AllowInverse != null && !Plugin.AllowInverse.Value)
            return "Inverse teleporter commands are disabled in config.\n";

        var pad = ResolveTeleporter(inverse);
        if (pad == null)
        {
            return inverse
                ? "Inverse teleporter not found (is it unlocked/bought?).\n"
                : "Ship teleporter not found (is it unlocked/bought?).\n";
        }

        // RemoveCooldown defaults to true when unset so a missing config still means no wait.
        var removeCooldown = Plugin.RemoveCooldown == null || Plugin.RemoveCooldown.Value;
        if (removeCooldown)
        {
            try
            {
                pad.cooldownTime = 0f;
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[Teleport] cooldown clear: {ex.Message}");
            }
        }

        try
        {
            if (pad.cooldownTime > 0f)
            {
                var left = Mathf.CeilToInt(pad.cooldownTime);
                return $"{(inverse ? "Inverse teleporter" : "Teleporter")} cooling down ({left}s).\n";
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Teleport] cooldown read: {ex.Message}");
        }

        if (inverse)
        {
            try
            {
                if (!pad.CanUseInverseTeleporter())
                    return "Inverse teleporter cannot be used right now.\n";
            }
            catch (Exception ex)
            {
                Plugin.Log.LogWarning($"[Teleport] CanUseInverseTeleporter: {ex.Message}");
            }
        }

        try
        {
            pad.PressTeleportButtonOnLocalClient();
            Plugin.Log.LogInfo($"[Teleport] PressTeleportButtonOnLocalClient inverse={inverse} id={pad.teleporterId}");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Teleport] PressTeleportButtonOnLocalClient failed: {ex.Message}; trying ServerRpc");
            try
            {
                pad.PressTeleportButtonServerRpc();
            }
            catch (Exception ex2)
            {
                Plugin.Log.LogWarning($"[Teleport] PressTeleportButtonServerRpc: {ex2.Message}");
                return "Teleporter button press failed.\n";
            }
        }

        return inverse
            ? "Inverse teleporter activated.\n"
            : "Teleporter activated (radar target).\n";
    }
}


internal static class HostModGateStartPatch
{
    public static void Postfix() => HostModGate.EnsureRegistered();
}
