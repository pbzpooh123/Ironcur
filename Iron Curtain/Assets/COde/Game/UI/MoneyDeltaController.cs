// MoneyDeltaController.cs
using UnityEngine;
using TMPro;

public class MoneyDeltaController : MonoBehaviour
{
    [Header("Where to spawn popups")]
    public RectTransform anchor;             // e.g., the container next to your money label
    [Header("Prefab (TMP_Text + CanvasGroup)")]
    public GameObject popupPrefab;

    [Header("Motion")]
    public float floatPixels = 48f;
    public float duration = 0.85f;

    [Header("Colors")]
    public Color plusColor = new Color(0.2f, 0.85f, 0.2f); // green
    public Color minusColor = new Color(0.9f, 0.2f, 0.2f); // red

    public void ShowDelta(int delta)
    {
        if (delta == 0 || anchor == null || popupPrefab == null) return;

        var go = Instantiate(popupPrefab, anchor);
        var txt = go.GetComponent<TMP_Text>();
        if (txt != null)
        {
            bool plus = delta > 0;
            txt.text = (plus ? "+" : "-") + "$" + Mathf.Abs(delta) + "M";
            txt.color = plus ? plusColor : minusColor;
        }

        var fader = go.GetComponent<FloatAndFade>();
        if (fader == null) fader = go.AddComponent<FloatAndFade>();

        // start at (0,0), float up
        fader.PlayLocal(Vector2.zero, new Vector2(0f, floatPixels), duration, true);
    }
}
