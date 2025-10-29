using UnityEngine;
using UnityEngine.UI;

public class TileHighlight : MonoBehaviour
{
    [Header("Renderer (one is enough)")]
    public SpriteRenderer spriteOverlay;   // for 2D board tiles
    public Image uiOverlay;                // for UI-based boards

    [Header("Colors/Alpha")]
    public Color passColor = new(0.2f, 0.6f, 1f, 0.35f);
    public Color landColor = new(1f, 0.85f, 0.2f, 0.55f);

    [Range(0f, 1f)] public float baseAlpha = 0.0f;  // idle alpha when “off”
    [Range(0f, 1f)] public float maxAlpha  = 0.6f;  // peak alpha when “on”

    [Header("Pulse")]
    public float passPulseTime = 0.20f; // quick flash when you pass through
    public float landPulseTime = 0.30f; // slightly longer on landing

    Color _idleCol;

    void Awake()
    {
        _idleCol = new Color(1,1,1, baseAlpha);
        ApplyColor(_idleCol);
        Enable(true); // keep object active; we’ll just change alpha
    }

    public void Clear()
    {
        ApplyColor(_idleCol);
    }

    public void FlashPass()
    {
        StopAllCoroutines();
        StartCoroutine(Pulse(passColor, passPulseTime));
    }

    public void FlashLand()
    {
        StopAllCoroutines();
        StartCoroutine(Pulse(landColor, landPulseTime));
    }

    System.Collections.IEnumerator Pulse(Color c, float time)
    {
        // fade in
        float t = 0f;
        var start = new Color(c.r, c.g, c.b, baseAlpha);
        var peak  = new Color(c.r, c.g, c.b, Mathf.Clamp01(maxAlpha));
        while (t < time * 0.5f)
        {
            t += Time.unscaledDeltaTime;
            ApplyColor(Color.Lerp(start, peak, t / (time * 0.5f)));
            yield return null;
        }
        // fade out to idle
        t = 0f;
        while (t < time * 0.5f)
        {
            t += Time.unscaledDeltaTime;
            ApplyColor(Color.Lerp(peak, _idleCol, t / (time * 0.5f)));
            yield return null;
        }
        ApplyColor(_idleCol);
    }

    void ApplyColor(Color c)
    {
        if (spriteOverlay) { var sc = spriteOverlay.color; sc = c; spriteOverlay.color = sc; }
        if (uiOverlay)     { var ic = uiOverlay.color;     ic = c; uiOverlay.color = ic;     }
    }

    void Enable(bool on)
    {
        if (spriteOverlay) spriteOverlay.enabled = on;
        if (uiOverlay)     uiOverlay.enabled     = on;
    }
}
