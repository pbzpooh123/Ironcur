using UnityEngine;

public static class PlayerColors
{
    // index: 0..N-1 are valid; -1 means "unset"
    public static readonly Color[] Palette = new Color[]
    {
        new Color32(255,   0,   0, 255), // 0 Red
        new Color32(  0,   0, 255, 255), // 1 Blue
        new Color32(  0, 255,   0, 255), // 2 Green
        new Color32(255, 255,   0, 255), // 3 Yellow
        new Color32(255,   0, 255, 255), // 4 Magenta
        new Color32(  0, 255, 255, 255), // 5 Cyan
    };

    public static bool IsValid(int idx) => idx >= 0 && idx < Palette.Length;

    /// Returns 'unsetFallback' if idx is -1 or invalid.
    public static Color GetOr(Color unsetFallback, int idx)
        => IsValid(idx) ? Palette[idx] : unsetFallback;

    /// Clamp only **valid** domain; keep -1 as -1.
    public static int ClampOrUnset(int idx)
    {
        if (idx == -1) return -1;
        if (idx < 0) return 0;
        if (idx >= Palette.Length) return Palette.Length - 1;
        return idx;
    }

    public static Color Get(int colorIndex)
    {
        return PlayerColors.Palette[colorIndex];
    }
}
