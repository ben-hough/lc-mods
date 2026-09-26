using System.Collections.Generic;
using UnityEngine;

namespace SprayPatterns;

internal readonly struct PatternDef
{
    public readonly PatternId Id;
    public readonly string Label;
    public readonly Color Tint;

    public PatternDef(PatternId id, string label, Color tint)
    {
        Id = id;
        Label = label;
        Tint = tint;
    }
}

internal static class PatternCatalog
{
    public static PatternId Selected { get; set; } = PatternId.Vanilla;
    public static float RotationDegrees { get; set; }

    private static readonly PatternDef[] All =
    {
        new(PatternId.Vanilla, "Vanilla", new Color(0.85f, 0.85f, 0.9f, 1f)),
        new(PatternId.Arrow, "Arrow", new Color(1f, 0.95f, 0.35f, 1f)),
        new(PatternId.ArrowMain, "MAIN", new Color(1f, 0.85f, 0.2f, 1f)),
        new(PatternId.ArrowFire, "FIRE", new Color(1f, 0.45f, 0.2f, 1f)),
        new(PatternId.Dry, "Dry", new Color(0.75f, 0.75f, 0.8f, 1f)),
        new(PatternId.Juicy, "Juicy", new Color(0.35f, 1f, 0.45f, 1f)),
        new(PatternId.Skull, "Skull", new Color(1f, 1f, 1f, 1f)),
        new(PatternId.Sunshine, "Sun", new Color(1f, 0.92f, 0.25f, 1f)),
    };

    public static bool IsEnabled(PatternId id) => id switch
    {
        PatternId.Vanilla => true,
        PatternId.Arrow => Plugin.EnableArrow.Value,
        PatternId.ArrowMain => Plugin.EnableArrowMain.Value,
        PatternId.ArrowFire => Plugin.EnableArrowFire.Value,
        PatternId.Dry => Plugin.EnableDry.Value,
        PatternId.Juicy => Plugin.EnableJuicy.Value,
        PatternId.Skull => Plugin.EnableSkull.Value,
        PatternId.Sunshine => Plugin.EnableSunshine.Value,
        _ => false,
    };

    public static IReadOnlyList<PatternDef> GetEnabled()
    {
        var list = new List<PatternDef>(All.Length);
        foreach (var def in All)
        {
            if (IsEnabled(def.Id))
                list.Add(def);
        }
        if (list.Count == 0)
            list.Add(All[0]);
        return list;
    }

    public static PatternDef Get(PatternId id)
    {
        foreach (var def in All)
        {
            if (def.Id == id)
                return def;
        }
        return All[0];
    }

    public static bool IsPatternStamp(PatternId id) => id != PatternId.Vanilla && IsEnabled(id);
}
