using UnityEngine;

public class TileVisuals : MonoBehaviour
{
    [Header("Main")]
    public SpriteRenderer mainRenderer;
    public Sprite unclaimedSprite;
    public Sprite claimedSprite;
    public Color unclaimedTint = Color.white;

    [Header("Tone")]
    [Range(0f,1f)] public float ownedToneLerp = 0.85f; // darken/lighten a bit when owned

    public void ShowUnclaimed()
    {
        if (mainRenderer != null)
        {
            if (unclaimedSprite) mainRenderer.sprite = unclaimedSprite;
            mainRenderer.color = unclaimedTint;
        }
    }

    public void ShowOwnedByColor(int colorIndex)
    {
        // If color is unset/invalid, render as unclaimed
        if (!PlayerColors.IsValid(colorIndex))
        {
            ShowUnclaimed();
            return;
        }

        var c = PlayerColors.Palette[colorIndex];

        if (mainRenderer != null)
        {
            Debug.Log($"[TileVisuals] ShowOwnedByColor colorIndex={colorIndex} color={c}");
            if (claimedSprite) mainRenderer.sprite = claimedSprite;

            // Subtle tint toward owner color
            var tone = Color.Lerp(Color.white, c, ownedToneLerp);
            tone.a = 1f;
            mainRenderer.color = tone;
        }
    }
}
