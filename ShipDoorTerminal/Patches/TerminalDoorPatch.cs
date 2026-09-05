using System;
using System.Text.RegularExpressions;
using HarmonyLib;
using UnityEngine;

namespace ShipDoorTerminal.Patches;

[HarmonyPatch(typeof(Terminal), nameof(Terminal.ParsePlayerSentence))]
internal static class TerminalDoorPatch
{
    private static readonly Regex NonWord = new(@"[^a-z0-9\s]", RegexOptions.Compiled);

    private static bool Prefix(Terminal __instance, ref TerminalNode __result)
    {
        if (Plugin.Instance == null || !Plugin.Enabled.Value)
            return true;

        try
        {
            if (__instance.screenText == null || __instance.textAdded <= 0)
                return true;

            var raw = __instance.screenText.text;
            if (raw.Length < __instance.textAdded)
                return true;

            var input = raw.Substring(raw.Length - __instance.textAdded);
            input = NonWord.Replace(input.ToLowerInvariant(), " ").Trim();
            input = Regex.Replace(input, @"\s+", " ");

            if (string.IsNullOrEmpty(input))
                return true;

            string? response = input switch
            {
                "door" or "doors" => ToggleDoor(),
                "opendoor" or "open door" or "door open" => SetDoor(closed: false),
                "closedoor" or "close door" or "door close" => SetDoor(closed: true),
                _ => null,
            };

            if (response == null)
                return true;

            __result = CreateNode(response);
            return false;
        }
        catch (Exception ex)
        {
            Plugin.Log.LogWarning($"Ship door terminal command failed: {ex.Message}");
            return true;
        }
    }

    private static TerminalNode CreateNode(string text)
    {
        var node = ScriptableObject.CreateInstance<TerminalNode>();
        node.displayText = text.EndsWith("\n") ? text : text + "\n";
        node.clearPreviousText = false;
        node.maxCharactersToType = 50;
        return node;
    }

    private static string ToggleDoor()
    {
        var start = StartOfRound.Instance;
        if (start == null || !start.shipDoorsEnabled)
            return "Ship doors unavailable (not landed?).\n";

        return SetDoor(closed: !start.hangarDoorsClosed);
    }

    private static string SetDoor(bool closed)
    {
        var start = StartOfRound.Instance;
        if (start == null || !start.shipDoorsEnabled)
            return "Ship doors unavailable (not landed?).\n";

        var door = UnityEngine.Object.FindObjectOfType<HangarShipDoor>();
        if (door != null && door.overheated)
            return "Ship doors are overheated and cannot be toggled.\n";

        if (start.hangarDoorsClosed == closed)
            return closed ? "Ship doors are already closed.\n" : "Ship doors are already open.\n";

        if (door?.triggerScript != null)
        {
            var player = GameNetworkManager.Instance?.localPlayerController;
            if (player == null)
                return "No local player to operate the door.\n";

            try
            {
                if (closed)
                    door.triggerScript.onInteract.Invoke(player);
                else
                    door.triggerScript.onStopInteract.Invoke(player);
            }
            catch
            {
                start.SetShipDoorsClosed(closed);
            }
        }
        else
        {
            start.SetShipDoorsClosed(closed);
        }

        return closed ? "Closing ship doors...\n" : "Opening ship doors...\n";
    }
}
