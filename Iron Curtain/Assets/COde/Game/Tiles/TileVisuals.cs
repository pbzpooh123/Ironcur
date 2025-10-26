using UnityEngine;
using TMPro;

public class TileVisuals : MonoBehaviour
{
    [Header("Main")]
    public SpriteRenderer mainRenderer;
    public Sprite unclaimedSprite;
    public Sprite claimedSprite;
    public Color unclaimedTint = Color.white;

    [Header("Tone")]
    [Range(0f,1f)] public float ownedToneLerp = 0.85f;

    // NEW — wire these in the prefab
    [Header("Labels")]
    public TMP_Text priceText;     // e.g. "$150M"
    public TMP_Text surgeText;     // e.g. "×1.2"

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
        if (!PlayerColors.IsValid(colorIndex))
        {
            ShowUnclaimed();
            return;
        }

        var c = PlayerColors.Palette[colorIndex];
        if (mainRenderer != null)
        {
            if (claimedSprite) mainRenderer.sprite = claimedSprite;
            var tone = Color.Lerp(Color.white, c, ownedToneLerp);
            tone.a = 1f;
            mainRenderer.color = tone;
        }
    }

    // ===== NEW helpers used by TileData =====
    public void SetPrice(string txt)
    {
        if (priceText != null) priceText.text = txt;
        SetPriceVisible(true);
    }

    public void SetPriceVisible(bool v)
    {
        if (priceText != null) priceText.gameObject.SetActive(v);
    }

    public void SetSurgeText(string txt)
    {
        if (surgeText != null) surgeText.text = txt;
    }

    public void SetSurgeVisible(bool v)
    {
        if (surgeText != null) surgeText.gameObject.SetActive(v);
    }

    // Optional: convenience so TileData can pass the owner directly
    public void SetOwner(PlayerPawn pawn)
    {
        if (pawn == null) ShowUnclaimed();
        else ShowOwnedByColor(pawn.colorIndex.Value);
    }
}
