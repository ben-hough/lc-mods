using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx.Configuration;

namespace ShipStarterKit;

/// <summary>
/// Per-item spawn counts. Known defaults bind at Awake; remaining buyable store
/// equipment is bound dynamically once Terminal.buyableItemsList is available.
/// </summary>
internal static class ItemCountConfig
{
    private const string Section = "ItemCounts";

    /// <summary>Config key + match tokens (case-insensitive contains/equals) + default count.</summary>
    private static readonly (string Key, string[] Matchers, int Default)[] KnownDefaults =
    {
        ("BeltBag", new[] { "belt bag", "beltbag" }, 10),
        ("ProFlashlight", new[] { "pro-flashlight", "pro flashlight", "proflashlight" }, 10),
        ("Jetpack", new[] { "jetpack" }, 10),
        ("Lockpicker", new[] { "lockpicker", "lockpick" }, 10),
        ("SprayPaint", new[] { "spray paint", "spraypaint" }, 10),
        ("WeedKiller", new[] { "weed killer", "weedkiller" }, 10),
        ("Shovel", new[] { "shovel" }, 10),
    };

    private static readonly Dictionary<string, ConfigEntry<int>> Entries = new(StringComparer.OrdinalIgnoreCase);
    private static readonly HashSet<string> BoundDynamicKeys = new(StringComparer.OrdinalIgnoreCase);
    private static ConfigFile? _config;
    private static bool _defaultsBound;

    public static void BindDefaults(ConfigFile config)
    {
        _config = config;
        if (_defaultsBound)
            return;

        foreach (var (key, _, def) in KnownDefaults)
        {
            Entries[key] = config.Bind(
                Section,
                key,
                def,
                $"How many '{key}' to spawn on first save load (0 = skip). Matched flexibly against store itemName.");
        }

        _defaultsBound = true;
    }

    /// <summary>
    /// After Terminal exists, bind any remaining buyableItemsList entries at default 0
    /// so players can raise counts without editing code.
    /// </summary>
    public static void EnsureBuyableEntries(Item[]? buyable)
    {
        if (_config == null || buyable == null)
            return;

        foreach (var item in buyable)
        {
            if (item == null || string.IsNullOrWhiteSpace(item.itemName))
                continue;

            // Skip scrap; store equipment only.
            if (item.isScrap)
                continue;

            if (TryGetKnownKey(item.itemName, out _))
                continue;

            var key = SanitizeKey(item.itemName);
            if (string.IsNullOrEmpty(key) || Entries.ContainsKey(key) || BoundDynamicKeys.Contains(key))
                continue;

            Entries[key] = _config.Bind(
                Section,
                key,
                0,
                $"How many '{item.itemName}' to spawn on first save load (0 = skip). Auto-discovered from Terminal.buyableItemsList.");
            BoundDynamicKeys.Add(key);
            Plugin.V($"Bound dynamic config ItemCounts.{key} for store item '{item.itemName}' (default 0).");
        }
    }

    public static int GetCountForItem(Item item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.itemName))
            return 0;

        if (TryGetKnownKey(item.itemName, out var knownKey) && Entries.TryGetValue(knownKey, out var knownEntry))
            return Math.Max(0, knownEntry.Value);

        var key = SanitizeKey(item.itemName);
        if (Entries.TryGetValue(key, out var entry))
            return Math.Max(0, entry.Value);

        return 0;
    }

    public static bool TryGetKnownKey(string itemName, out string key)
    {
        var name = itemName.Trim();
        foreach (var (k, matchers, _) in KnownDefaults)
        {
            foreach (var m in matchers)
            {
                if (name.Equals(m, StringComparison.OrdinalIgnoreCase)
                    || name.IndexOf(m, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    key = k;
                    return true;
                }
            }
        }

        key = "";
        return false;
    }

    private static string SanitizeKey(string itemName)
    {
        var cleaned = Regex.Replace(itemName.Trim(), @"[^A-Za-z0-9]+", "");
        if (cleaned.Length == 0)
            return "";
        if (char.IsDigit(cleaned[0]))
            cleaned = "Item" + cleaned;
        return cleaned;
    }
}
