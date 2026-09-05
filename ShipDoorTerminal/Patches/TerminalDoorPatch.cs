using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace ShipDoorTerminal;

internal static class ManualPatches
{
    internal static void Apply(Harmony harmony)
    {
        try
        {
            var parseWord = AccessTools.Method(typeof(Terminal), "ParseWord", new[] { typeof(string), typeof(int) });
            var onSubmit = AccessTools.Method(typeof(Terminal), "OnSubmit");
            var parseSentence = AccessTools.Method(typeof(Terminal), "ParsePlayerSentence");
            var awake = AccessTools.Method(typeof(Terminal), "Awake");
            var start = AccessTools.Method(typeof(Terminal), "Start");

            Plugin.Log.LogInfo(
                $"Manual patch resolve: ParseWord={Fmt(parseWord)}, OnSubmit={Fmt(onSubmit)}, " +
                $"ParsePlayerSentence={Fmt(parseSentence)}, Awake={Fmt(awake)}, Start={Fmt(start)}");

            if (parseWord != null)
            {
                harmony.Patch(parseWord,
                    prefix: new HarmonyMethod(typeof(ParseWordPatch), nameof(ParseWordPatch.Prefix)));
                Plugin.Log.LogInfo("Patched Terminal.ParseWord (prefix)");
            }

            if (onSubmit != null)
            {
                harmony.Patch(onSubmit,
                    prefix: new HarmonyMethod(typeof(OnSubmitPatch), nameof(OnSubmitPatch.Prefix)));
                Plugin.Log.LogInfo("Patched Terminal.OnSubmit (prefix)");
            }

            if (parseSentence != null)
            {
                harmony.Patch(parseSentence,
                    prefix: new HarmonyMethod(typeof(ParseSentencePatch), nameof(ParseSentencePatch.Prefix)));
                Plugin.Log.LogInfo("Patched Terminal.ParsePlayerSentence (prefix)");
            }

            if (awake != null)
            {
                harmony.Patch(awake,
                    postfix: new HarmonyMethod(typeof(TerminalLifecyclePatch), nameof(TerminalLifecyclePatch.AwakePostfix)));
                Plugin.Log.LogInfo("Patched Terminal.Awake (postfix)");
            }

            if (start != null)
            {
                harmony.Patch(start,
                    postfix: new HarmonyMethod(typeof(TerminalLifecyclePatch), nameof(TerminalLifecyclePatch.StartPostfix)));
                Plugin.Log.LogInfo("Patched Terminal.Start (postfix)");
            }
        }
        catch (Exception ex)
        {
            Plugin.Log.LogError($"ManualPatches.Apply failed: {ex}");
        }
    }

    private static string Fmt(MethodInfo? m) =>
        m == null ? "NULL" : $"{m.DeclaringType?.Name}.{m.Name} ({(m.IsPublic ? "public" : "nonpublic")})";
}

internal static class TerminalInput
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

    internal static string Extract(Terminal terminal)
    {
        try
        {
            var text = terminal.screenText != null ? terminal.screenText.text : null;
            Plugin.V($"Extract screenText len={text?.Length ?? -1}, textAdded={terminal.textAdded}");
            if (string.IsNullOrEmpty(text))
                return "";

            if (terminal.textAdded > 0 && text.Length >= terminal.textAdded)
            {
                var slice = text.Substring(text.Length - terminal.textAdded);
                Plugin.V($"Extract via textAdded: '{Normalize(slice)}'");
                return Normalize(slice);
            }

            var idx = text.LastIndexOf('\n');
            var last = idx >= 0 ? text.Substring(idx + 1) : text;
            last = last.Trim().TrimStart('>', ' ');
            Plugin.V($"Extract via last-line: '{Normalize(last)}'");
            return Normalize(last);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Extract failed: {ex.Message}");
            return "";
        }
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
    // Return false = skip vanilla OnSubmit entirely for our commands.
    public static bool Prefix(Terminal __instance)
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return true;

        try
        {
            var input = TerminalInput.Extract(__instance);
            TerminalInput.LastSubmitted = input;
            Plugin.Log.LogInfo($"[OnSubmit] captured='{input}'");

            if (!TerminalInput.IsDoorCommand(input))
                return true;

            var response = DoorActions.Run(input);
            Plugin.Log.LogInfo($"[OnSubmit] handling '{input}' -> {response.Replace("\n", " ")}");

            var node = DoorActions.CreateNode(response);
            __instance.LoadNewNode(node);
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[OnSubmit] failed: {ex}");
            return true;
        }
    }
}

internal static class ParseWordPatch
{
    public static bool Prefix(string playerWord, int specificityRequired, ref TerminalKeyword __result)
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return true;

        try
        {
            var word = TerminalInput.Normalize(playerWord);
            Plugin.V($"[ParseWord] word='{playerWord}' normalized='{word}' specificity={specificityRequired}");

            if (!TerminalInput.IsDoorCommand(word))
                return true;

            // Run action here too — ParseWord is where vanilla logs "Could not parse word".
            var response = DoorActions.Run(word);
            Plugin.Log.LogInfo($"[ParseWord] intercept '{word}' -> {response.Replace("\n", " ")}");

            __result = DoorActions.GetOrCreateKeyword(word, response);
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[ParseWord] failed: {ex}");
            return true;
        }
    }
}

internal static class ParseSentencePatch
{
    public static bool Prefix(Terminal __instance, ref TerminalNode __result)
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return true;

        try
        {
            var input = TerminalInput.LastSubmitted;
            if (string.IsNullOrEmpty(input))
                input = TerminalInput.Extract(__instance);

            Plugin.V($"[ParseSentence] input='{input}'");

            if (string.IsNullOrEmpty(input) || !TerminalInput.IsDoorCommand(input))
                return true;

            var response = DoorActions.Run(input);
            Plugin.Log.LogInfo($"[ParseSentence] handling '{input}'");
            __result = DoorActions.CreateNode(response);
            TerminalInput.LastSubmitted = null;
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[ParseSentence] failed: {ex}");
            return true;
        }
    }
}

internal static class TerminalLifecyclePatch
{
    public static void AwakePostfix(Terminal __instance)
    {
        Plugin.Log.LogInfo("[Terminal.Awake] postfix hit");
        DoorActions.EnsureKeywordsRegistered(__instance);
    }

    public static void StartPostfix(Terminal __instance)
    {
        Plugin.Log.LogInfo("[Terminal.Start] postfix hit");
        DoorActions.EnsureKeywordsRegistered(__instance);
    }
}

internal static class DoorActions
{
    private static readonly Dictionary<string, TerminalKeyword> Keywords = new();
    private static bool _registered;

    internal static TerminalNode CreateNode(string text)
    {
        var node = ScriptableObject.CreateInstance<TerminalNode>();
        node.displayText = text.EndsWith("\n") ? text : text + "\n";
        node.clearPreviousText = true;
        node.maxCharactersToType = 60;
        return node;
    }

    internal static TerminalKeyword GetOrCreateKeyword(string word, string display)
    {
        if (Keywords.TryGetValue(word, out var existing) && existing != null)
        {
            if (existing.specialKeywordResult != null)
                existing.specialKeywordResult.displayText = display.EndsWith("\n") ? display : display + "\n";
            return existing;
        }

        var node = CreateNode(display);
        var keyword = ScriptableObject.CreateInstance<TerminalKeyword>();
        keyword.word = word.Contains(" ") ? word.Split(' ')[0] : word;
        keyword.isVerb = false;
        keyword.specialKeywordResult = node;
        Keywords[word] = keyword;
        return keyword;
    }

    internal static void EnsureKeywordsRegistered(Terminal terminal)
    {
        try
        {
            if (terminal?.terminalNodes?.allKeywords == null)
            {
                Plugin.Log.LogInfo("Keyword register skipped: terminalNodes/allKeywords null");
                return;
            }

            if (_registered)
            {
                Plugin.V("Keywords already registered");
                return;
            }

            var list = new List<TerminalKeyword>(terminal.terminalNodes.allKeywords);
            void Add(string word)
            {
                if (list.Exists(k => k != null && k.word == word))
                    return;
                list.Add(GetOrCreateKeyword(word, $"Ship door command: {word}\n"));
            }

            Add("door");
            Add("doors");
            Add("opendoor");
            Add("closedoor");
            Add("dooropen");
            Add("doorclose");

            terminal.terminalNodes.allKeywords = list.ToArray();
            _registered = true;
            Plugin.Log.LogInfo($"Registered door keywords into allKeywords (count now {list.Count})");
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"EnsureKeywordsRegistered failed: {ex.Message}");
        }
    }

    internal static string Run(string input)
    {
        return input switch
        {
            "door" or "doors" => Toggle(),
            "opendoor" or "open door" or "door open" or "dooropen" => Set(closed: false),
            "closedoor" or "close door" or "door close" or "doorclose" => Set(closed: true),
            _ => "Unknown door command.\n",
        };
    }

    private static string Toggle()
    {
        var start = StartOfRound.Instance;
        if (start == null)
            return "Ship doors unavailable.\n";
        return Set(closed: !start.hangarDoorsClosed);
    }

    private static string Set(bool closed)
    {
        var start = StartOfRound.Instance;
        if (start == null)
            return "Ship doors unavailable.\n";

        var door = UnityEngine.Object.FindObjectOfType<HangarShipDoor>();
        Plugin.Log.LogInfo(
            $"[Door] closed={closed}, hangarDoorsClosed={start.hangarDoorsClosed}, " +
            $"shipDoorsEnabled={start.shipDoorsEnabled}, doorNull={door == null}, overheated={door?.overheated}");

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
                door.PlayDoorAnimation(closed);
            }

            start.SetShipDoorsClosed(closed);
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"[Door] toggle error: {ex.Message}");
            try { start.SetShipDoorsClosed(closed); }
            catch (Exception ex2) { return $"Failed to toggle ship doors: {ex2.Message}\n"; }
        }

        return closed ? "Closing ship doors...\n" : "Opening ship doors...\n";
    }
}
