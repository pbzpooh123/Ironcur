using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ToastItem : MonoBehaviour
{
    public RectTransform Rect;
    public CanvasGroup group;
    public Image bar;               // small left bar for color cue (optional)
    public TMP_Text text;           // message

    [Header("Style")]
    public Color infoCol    = new(0.20f, 0.50f, 1f, 1f);
    public Color successCol = new(0.25f, 0.78f, 0.40f, 1f);
    public Color warnCol    = new(1f, 0.75f, 0.20f, 1f);
    public Color errorCol   = new(0.95f, 0.30f, 0.30f, 1f);

    public void Setup(string msg, ToastKind kind)
    {
        text.text = msg;
        if (bar != null) bar.color = kind switch {
            ToastKind.Success => successCol,
            ToastKind.Warning => warnCol,
            ToastKind.Error   => errorCol,
            _                 => infoCol
        };

        // start hidden
        group.alpha = 0f;
        // slight offset for slide-in
        Rect.anchoredPosition += new Vector2(20f, 0f);
    }

    public IEnumerator PlayShow(float dur)
    {
        float t = 0f;
        Vector2 start = Rect.anchoredPosition;
        Vector2 end   = new Vector2(start.x - 20f, start.y);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            group.alpha = k;
            Rect.anchoredPosition = Vector2.Lerp(start, end, k);
            yield return null;
        }
        group.alpha = 1f;
        Rect.anchoredPosition = end;
    }

    public IEnumerator PlayHide(float dur)
    {
        float t = 0f;
        Vector2 start = Rect.anchoredPosition;
        Vector2 end   = new Vector2(start.x + 20f, start.y);
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            group.alpha = 1f - k;
            Rect.anchoredPosition = Vector2.Lerp(start, end, k);
            yield return null;
        }
        group.alpha = 0f;
        Rect.anchoredPosition = end;
    }
}
