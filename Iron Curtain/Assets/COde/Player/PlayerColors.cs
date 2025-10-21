using UnityEngine;

public static class PlayerColors
{
   public static readonly Color[] Palette = new Color[]
    {
        new Color(1f, 0.2f, 0.2f), // 0 Red
        new Color(0.2f, 0.5f, 1f), // 1 Blue
        new Color(0.2f, 0.9f, 0.3f), // 2 Green
        new Color(1f, 0.8f, 0.2f), // 3 Yellow
        new Color(0.9f, 0.3f, 0.9f), // 4 Magenta
        new Color(0.2f, 1f, 1f), // 5 Cyan
    };

    public static int Clamp(int idx)
    {
        if (idx < 0) return 0;
        if (idx >= Palette.Length) return Palette.Length - 1;
        return idx;
    }

    public static Color Get(int idx)
    {
        if (idx < 0 || idx >= Palette.Length) idx = 0;
        return Palette[idx];
    }
}
