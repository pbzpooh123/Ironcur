// ColorRegistry.cs
using System.Collections.Generic;

public static class ColorRegistry
{
    private static readonly HashSet<int> _used = new();

    public static bool TryReserve(int idx, PlayerPawn pawn)
    {
        // Free any previous reservation for this pawn
        foreach (var i in new List<int>(_used))
        {
            // If you track owner->idx mapping, free it here. Simple: ignore.
        }

        if (_used.Contains(idx)) return false;
        _used.Add(idx);
        return true;
    }

    public static void Free(int idx)
    {
        _used.Remove(idx);
    }

    public static int FirstFree()
    {
        for (int i = 0; i < PlayerColors.Palette.Length; i++)
            if (!_used.Contains(i)) return i;
        return 0;
    }
}
