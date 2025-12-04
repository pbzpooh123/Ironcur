using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(Image))]
public class UIPulseAlpha : MonoBehaviour
{
    public float minAlpha = 0.3f;
    public float maxAlpha = 1f;
    public float speed = 1.5f;

    private Image _img;
    private Color _base;

    void Awake()
    {
        _img = GetComponent<Image>();
        _base = _img.color;
    }

    void Update()
    {
        float t = (Mathf.Sin(Time.time * speed) + 1f) * 0.5f;   // 0..1
        float a = Mathf.Lerp(minAlpha, maxAlpha, t);
        _img.color = new Color(_base.r, _base.g, _base.b, a);
    }
}
