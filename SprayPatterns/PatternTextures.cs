using System.Collections.Generic;
using UnityEngine;

namespace SprayPatterns;

/// <summary>Procedural high-contrast spray icons / stamps (no external art).</summary>
internal static class PatternTextures
{
    private const int Size = 128;
    private static readonly Dictionary<PatternId, Texture2D> Map = new();
    private static readonly Dictionary<PatternId, Sprite> Sprites = new();
    private static bool _ready;

    public static void EnsureGenerated()
    {
        if (_ready)
            return;

        Map[PatternId.Vanilla] = DrawVanilla();
        Map[PatternId.Arrow] = DrawArrow(null);
        Map[PatternId.ArrowMain] = DrawArrow("MAIN");
        Map[PatternId.ArrowFire] = DrawArrow("FIRE");
        Map[PatternId.Dry] = DrawDry();
        Map[PatternId.Juicy] = DrawJuicy();
        Map[PatternId.Skull] = DrawSkull();
        Map[PatternId.Sunshine] = DrawSun();

        foreach (var kv in Map)
        {
            kv.Value.name = $"SprayPattern_{kv.Key}";
            kv.Value.wrapMode = TextureWrapMode.Clamp;
            kv.Value.filterMode = FilterMode.Bilinear;
            Sprites[kv.Key] = Sprite.Create(
                kv.Value,
                new Rect(0, 0, Size, Size),
                new Vector2(0.5f, 0.5f),
                100f);
        }

        _ready = true;
    }

    public static Texture2D GetTexture(PatternId id)
    {
        EnsureGenerated();
        return Map.TryGetValue(id, out var t) ? t : Map[PatternId.Vanilla];
    }

    public static Sprite GetSprite(PatternId id)
    {
        EnsureGenerated();
        return Sprites.TryGetValue(id, out var s) ? s : Sprites[PatternId.Vanilla];
    }

    private static Texture2D Blank()
    {
        var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
        var clear = new Color32(0, 0, 0, 0);
        var pixels = new Color32[Size * Size];
        for (var i = 0; i < pixels.Length; i++)
            pixels[i] = clear;
        tex.SetPixels32(pixels);
        return tex;
    }

    private static void Apply(Texture2D tex, Color32[] pixels)
    {
        tex.SetPixels32(pixels);
        tex.Apply(false, false);
    }

    private static Color32[] Get(Texture2D tex) => tex.GetPixels32();

    private static void Set(Color32[] px, int x, int y, Color32 c)
    {
        if ((uint)x >= Size || (uint)y >= Size)
            return;
        px[y * Size + x] = c;
    }

    private static void Blend(Color32[] px, int x, int y, Color32 c)
    {
        if ((uint)x >= Size || (uint)y >= Size)
            return;
        var i = y * Size + x;
        var dst = px[i];
        var a = c.a / 255f;
        if (a <= 0f)
            return;
        if (a >= 1f)
        {
            px[i] = c;
            return;
        }
        px[i] = new Color32(
            (byte)(c.r * a + dst.r * (1f - a)),
            (byte)(c.g * a + dst.g * (1f - a)),
            (byte)(c.b * a + dst.b * (1f - a)),
            (byte)Mathf.Clamp(dst.a + c.a, 0, 255));
    }

    private static void FillCircle(Color32[] px, float cx, float cy, float r, Color32 c)
    {
        var r2 = r * r;
        var minX = Mathf.FloorToInt(cx - r);
        var maxX = Mathf.CeilToInt(cx + r);
        var minY = Mathf.FloorToInt(cy - r);
        var maxY = Mathf.CeilToInt(cy + r);
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var dx = x + 0.5f - cx;
            var dy = y + 0.5f - cy;
            if (dx * dx + dy * dy <= r2)
                Blend(px, x, y, c);
        }
    }

    private static void StrokeCircle(Color32[] px, float cx, float cy, float r, float thickness, Color32 c)
    {
        var outer = r + thickness * 0.5f;
        var inner = Mathf.Max(0f, r - thickness * 0.5f);
        var o2 = outer * outer;
        var i2 = inner * inner;
        var minX = Mathf.FloorToInt(cx - outer);
        var maxX = Mathf.CeilToInt(cx + outer);
        var minY = Mathf.FloorToInt(cy - outer);
        var maxY = Mathf.CeilToInt(cy + outer);
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var dx = x + 0.5f - cx;
            var dy = y + 0.5f - cy;
            var d2 = dx * dx + dy * dy;
            if (d2 <= o2 && d2 >= i2)
                Blend(px, x, y, c);
        }
    }

    private static void FillRect(Color32[] px, int x0, int y0, int x1, int y1, Color32 c)
    {
        if (x0 > x1) (x0, x1) = (x1, x0);
        if (y0 > y1) (y0, y1) = (y1, y0);
        for (var y = y0; y <= y1; y++)
        for (var x = x0; x <= x1; x++)
            Blend(px, x, y, c);
    }

    private static void FillTriangle(Color32[] px, Vector2 a, Vector2 b, Vector2 c, Color32 col)
    {
        var minX = Mathf.FloorToInt(Mathf.Min(a.x, Mathf.Min(b.x, c.x)));
        var maxX = Mathf.CeilToInt(Mathf.Max(a.x, Mathf.Max(b.x, c.x)));
        var minY = Mathf.FloorToInt(Mathf.Min(a.y, Mathf.Min(b.y, c.y)));
        var maxY = Mathf.CeilToInt(Mathf.Max(a.y, Mathf.Max(b.y, c.y)));
        for (var y = minY; y <= maxY; y++)
        for (var x = minX; x <= maxX; x++)
        {
            var p = new Vector2(x + 0.5f, y + 0.5f);
            if (PointInTri(p, a, b, c))
                Blend(px, x, y, col);
        }
    }

    private static bool PointInTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
    {
        var v0 = c - a;
        var v1 = b - a;
        var v2 = p - a;
        var dot00 = Vector2.Dot(v0, v0);
        var dot01 = Vector2.Dot(v0, v1);
        var dot02 = Vector2.Dot(v0, v2);
        var dot11 = Vector2.Dot(v1, v1);
        var dot12 = Vector2.Dot(v1, v2);
        var inv = 1f / (dot00 * dot11 - dot01 * dot01);
        var u = (dot11 * dot02 - dot01 * dot12) * inv;
        var v = (dot00 * dot12 - dot01 * dot02) * inv;
        return u >= 0f && v >= 0f && u + v < 1f;
    }

    private static void DrawLine(Color32[] px, float x0, float y0, float x1, float y1, float thickness, Color32 c)
    {
        var dx = x1 - x0;
        var dy = y1 - y0;
        var len = Mathf.Sqrt(dx * dx + dy * dy);
        if (len < 0.001f)
            return;
        var steps = Mathf.CeilToInt(len);
        for (var i = 0; i <= steps; i++)
        {
            var t = i / (float)steps;
            FillCircle(px, x0 + dx * t, y0 + dy * t, thickness * 0.5f, c);
        }
    }

    /// <summary>5x7 block glyph atlas for MAIN/FIRE labels.</summary>
    private static readonly Dictionary<char, string[]> Glyphs = new()
    {
        ['M'] = new[] { "10001", "11011", "10101", "10001", "10001", "10001", "10001" },
        ['A'] = new[] { "01110", "10001", "10001", "11111", "10001", "10001", "10001" },
        ['I'] = new[] { "11111", "00100", "00100", "00100", "00100", "00100", "11111" },
        ['N'] = new[] { "10001", "11001", "10101", "10011", "10001", "10001", "10001" },
        ['F'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "10000" },
        ['R'] = new[] { "11110", "10001", "10001", "11110", "10100", "10010", "10001" },
        ['E'] = new[] { "11111", "10000", "10000", "11110", "10000", "10000", "11111" },
    };

    private static void DrawText(Color32[] px, string text, int originX, int originY, int scale, Color32 c)
    {
        var cursor = originX;
        foreach (var ch in text.ToUpperInvariant())
        {
            if (!Glyphs.TryGetValue(ch, out var rows))
            {
                cursor += 4 * scale;
                continue;
            }
            for (var row = 0; row < rows.Length; row++)
            {
                var line = rows[row];
                for (var col = 0; col < line.Length; col++)
                {
                    if (line[col] != '1')
                        continue;
                    for (var sy = 0; sy < scale; sy++)
                    for (var sx = 0; sx < scale; sx++)
                        Blend(px, cursor + col * scale + sx, originY + (rows.Length - 1 - row) * scale + sy, c);
                }
            }
            cursor += (5 + 1) * scale;
        }
    }

    private static Texture2D DrawVanilla()
    {
        var tex = Blank();
        var px = Get(tex);
        var ink = new Color32(220, 220, 230, 230);
        // Scribble dots like free paint
        for (var i = 0; i < 48; i++)
        {
            var ang = i * 0.7f;
            var r = 18f + (i % 7) * 4f;
            var x = Size * 0.5f + Mathf.Cos(ang) * r;
            var y = Size * 0.5f + Mathf.Sin(ang * 1.3f) * r * 0.7f;
            FillCircle(px, x, y, 3f + (i % 3), ink);
        }
        DrawText(px, "FREE", 28, 10, 2, ink);
        Apply(tex, px);
        return tex;
    }

    private static Texture2D DrawArrow(string? label)
    {
        var tex = Blank();
        var px = Get(tex);
        var ink = new Color32(255, 240, 80, 255);
        // shaft pointing up
        FillRect(px, 54, 20, 74, 78, ink);
        // head
        FillTriangle(px, new Vector2(64, 118), new Vector2(28, 72), new Vector2(100, 72), ink);
        if (!string.IsNullOrEmpty(label))
        {
            var outline = new Color32(20, 20, 20, 255);
            DrawText(px, label!, 34, 28, 2, outline);
            DrawText(px, label!, 32, 30, 2, ink);
        }
        Apply(tex, px);
        return tex;
    }

    private static Texture2D DrawDry()
    {
        var tex = Blank();
        var px = Get(tex);
        var ink = new Color32(200, 200, 210, 255);
        // empty droplet outline
        StrokeCircle(px, 64, 78, 28, 5, ink);
        FillTriangle(px, new Vector2(64, 118), new Vector2(40, 88), new Vector2(88, 88), ink);
        // clear center of droplet body a bit + big X
        FillCircle(px, 64, 72, 16, new Color32(0, 0, 0, 0));
        DrawLine(px, 36, 36, 92, 92, 8, ink);
        DrawLine(px, 92, 36, 36, 92, 8, ink);
        Apply(tex, px);
        return tex;
    }

    private static Texture2D DrawJuicy()
    {
        var tex = Blank();
        var px = Get(tex);
        var ink = new Color32(80, 255, 120, 255);
        // full droplet
        FillCircle(px, 64, 70, 30, ink);
        FillTriangle(px, new Vector2(64, 118), new Vector2(34, 82), new Vector2(94, 82), ink);
        // sparkles / $
        var spark = new Color32(255, 255, 180, 255);
        FillCircle(px, 30, 100, 4, spark);
        FillCircle(px, 100, 105, 5, spark);
        FillCircle(px, 96, 40, 4, spark);
        DrawText(px, "E", 54, 55, 3, new Color32(20, 60, 30, 255)); // stand-in cash mark
        Apply(tex, px);
        return tex;
    }

    private static Texture2D DrawSkull()
    {
        var tex = Blank();
        var px = Get(tex);
        var ink = new Color32(245, 245, 245, 255);
        FillCircle(px, 64, 78, 36, ink);
        FillRect(px, 44, 28, 84, 55, ink);
        // eyes
        var voidC = new Color32(10, 10, 14, 255);
        FillCircle(px, 48, 82, 10, voidC);
        FillCircle(px, 80, 82, 10, voidC);
        // nose
        FillTriangle(px, new Vector2(64, 68), new Vector2(56, 54), new Vector2(72, 54), voidC);
        // teeth
        for (var i = 0; i < 4; i++)
            FillRect(px, 48 + i * 8, 32, 52 + i * 8, 44, voidC);
        Apply(tex, px);
        return tex;
    }

    private static Texture2D DrawSun()
    {
        var tex = Blank();
        var px = Get(tex);
        var ink = new Color32(255, 230, 60, 255);
        FillCircle(px, 64, 64, 26, ink);
        for (var i = 0; i < 12; i++)
        {
            var ang = i * Mathf.PI * 2f / 12f;
            var x0 = 64f + Mathf.Cos(ang) * 34f;
            var y0 = 64f + Mathf.Sin(ang) * 34f;
            var x1 = 64f + Mathf.Cos(ang) * 56f;
            var y1 = 64f + Mathf.Sin(ang) * 56f;
            DrawLine(px, x0, y0, x1, y1, 6f, ink);
        }
        Apply(tex, px);
        return tex;
    }
}
