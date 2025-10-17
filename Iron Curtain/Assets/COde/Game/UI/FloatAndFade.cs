using UnityEngine;
using TMPro;

[RequireComponent(typeof(CanvasGroup))]
public class FloatAndFade : MonoBehaviour
{
    public void PlayLocal(Vector2 start, Vector2 end, float duration = 0.8f, bool destroyOnEnd = true)
    {
        var rt = transform as RectTransform;
        var cg = GetComponent<CanvasGroup>();

        StopAllCoroutines();
        StartCoroutine(Co(start, end, duration, destroyOnEnd, rt, cg));
    }

    private System.Collections.IEnumerator Co(Vector2 start, Vector2 end, float duration, bool destroyOnEnd, RectTransform rt, CanvasGroup cg)
    {
        float t = 0f;
        if (rt != null) rt.anchoredPosition = start;
        if (cg != null) cg.alpha = 1f;

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);

            if (rt != null) rt.anchoredPosition = Vector2.LerpUnclamped(start, end, k);
            if (cg != null) cg.alpha = 1f - k;

            yield return null;
        }

        if (destroyOnEnd) Destroy(gameObject);
    }
}
