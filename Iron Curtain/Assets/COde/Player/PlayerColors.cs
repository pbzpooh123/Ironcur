using UnityEngine;

public static class PlayerColors
{
   public static readonly Color[] Palette = new Color[]
    {
        new Color32(255,  0,  0, 255),// 0: Red
        new Color32(  0,  0,255, 255), // 1: Blue
        new Color32(  0,255,  0, 255), // : Green
        new Color32(255,255,  0, 255), // 3 Yellow
        new Color32(  255,  0,255, 255), // 4: Magenta
        new Color32(0,255,  255, 255), // 5 Cyan
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
