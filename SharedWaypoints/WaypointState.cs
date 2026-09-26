using UnityEngine;

namespace SharedWaypoints;

internal sealed class WaypointState
{
    public ulong OwnerPlayerClientId;
    public string OwnerName = "";
    public Vector3 Position;
    public bool IsInside;
    public int ColorIndex;

    public Color Color => WaypointPalette.Get(ColorIndex);

    public static int ColorIndexFor(ulong clientId)
        => (int)(clientId % (ulong)WaypointPalette.Count);
}

internal static class WaypointPalette
{
    private static readonly Color[] Colors =
    {
        new Color(0.95f, 0.45f, 0.20f, 1f), // orange
        new Color(0.30f, 0.85f, 0.95f, 1f), // cyan
        new Color(0.55f, 0.95f, 0.35f, 1f), // lime
        new Color(0.95f, 0.35f, 0.75f, 1f), // pink
        new Color(0.95f, 0.90f, 0.25f, 1f), // yellow
        new Color(0.55f, 0.55f, 1.00f, 1f), // periwinkle
        new Color(1.00f, 0.55f, 0.35f, 1f), // coral
        new Color(0.40f, 0.95f, 0.70f, 1f), // mint
    };

    public static int Count => Colors.Length;

    public static Color Get(int index)
    {
        if (Colors.Length == 0)
            return Color.white;
        var i = index % Colors.Length;
        if (i < 0)
            i += Colors.Length;
        return Colors[i];
    }
}
