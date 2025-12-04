using UnityEngine;

public class UIScanBar : MonoBehaviour
{
    public float topY = 300f;
    public float bottomY = -300f;
    public float speed = 40f;

    private RectTransform _rt;

    void Awake()
    {
        _rt = transform as RectTransform;
    }

    void Update()
    {
        if (_rt == null) return;
        Vector2 pos = _rt.anchoredPosition;
        pos.y -= speed * Time.deltaTime;
        if (pos.y < bottomY)
            pos.y = topY;
        _rt.anchoredPosition = pos;
    }
}
