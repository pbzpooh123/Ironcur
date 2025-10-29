using UnityEngine;

public class HighlightPulse : MonoBehaviour
{
    public SpriteRenderer sr;   // assign in prefab
    public float life = 0.35f;  // total time
    public Color color = Color.yellow;
    public float startScale = 0.85f;
    public float endScale = 1.15f;

    float _t;

    void OnEnable()
    {
        _t = 0f;
        if (sr != null)
        {
            sr.color = new Color(color.r, color.g, color.b, 0f);
            transform.localScale = Vector3.one * startScale;
        }
    }

    void Update()
    {
        _t += Time.unscaledDeltaTime;
        float half = life * 0.5f;
        float a = (_t <= half) ? (_t / half) : (1f - (_t - half) / half);
        a = Mathf.Clamp01(a);

        if (sr) sr.color = new Color(color.r, color.g, color.b, a);

        float s = Mathf.Lerp(startScale, endScale, _t / life);
        transform.localScale = Vector3.one * s;

        if (_t >= life) gameObject.SetActive(false);
    }
}
