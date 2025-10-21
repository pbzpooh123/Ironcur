// TileVisuals.cs
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
            mainRenderer.sprite = unclaimedSprite ? unclaimedSprite : mainRenderer.sprite;
            mainRenderer.color  = unclaimedTint;
        }
    }

    public void ShowOwnedByColor(int colorIndex)
    {
        var c = PlayerColors.Palette[PlayerColors.Clamp(colorIndex)];

        if (mainRenderer != null)
        {
            // use the claimed sprite and tint slightly toward the owner color for tone
            mainRenderer.sprite = claimedSprite ? claimedSprite : mainRenderer.sprite;
            var tone = Color.Lerp(Color.white, c, ownedToneLerp);
            tone.a = 1f;
            mainRenderer.color = tone;
        }
    }
}
