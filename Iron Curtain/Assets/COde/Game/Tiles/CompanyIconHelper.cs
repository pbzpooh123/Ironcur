using UnityEngine;

public static class CompanyIconHelper
{
    /// <summary>
    /// Find the board tile with this companyName and return its UI icon sprite.
    /// </summary>
    public static Sprite GetIconForCompany(string companyName)
    {
        if (string.IsNullOrWhiteSpace(companyName))
            return null;

        if (GameManager.Instance == null || GameManager.Instance.boardTiles == null)
            return null;

        foreach (var go in GameManager.Instance.boardTiles)
        {
            if (!go) continue;

            var td = go.GetComponent<TileData>();
            if (td == null) continue;

            if (!string.Equals(td.companyName, companyName, System.StringComparison.Ordinal))
                continue;

            // 1) Prefer explicit UI icon
            if (td.companyIcon != null)
                return td.companyIcon;

            // 2) Fallback to whatever sprite is on the tile, if you like
            var sr = go.GetComponentInChildren<SpriteRenderer>();
            if (sr != null && sr.sprite != null)
                return sr.sprite;

            break;
        }

        return null;
    }
}
