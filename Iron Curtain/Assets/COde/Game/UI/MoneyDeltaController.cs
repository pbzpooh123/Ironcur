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

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.P)) ShowDelta(Random.Range(-500, 1000));
    }


    public void ShowDelta(int delta)
    {
        if (delta == 0) return;
        if (anchor == null) { Debug.LogWarning("MoneyDeltaController: anchor not set"); return; }
        if (popupPrefab == null) { Debug.LogWarning("MoneyDeltaController: popupPrefab not set"); return; }

        var go = Instantiate(popupPrefab, anchor);
        var rt = (RectTransform)go.transform;
        rt.SetParent(anchor, false);
        rt.localScale = Vector3.one;
        rt.SetAsLastSibling();
        rt.anchoredPosition = Vector2.zero; // start centered (not clipped by layout)

        // find text even if it's on a child
        var txt = go.GetComponentInChildren<TMP_Text>(true);
        if (txt != null)
        {
            bool plus = delta > 0;
            txt.text = (plus ? "+" : "-") + "$" + Mathf.Abs(delta) + "M";
            txt.color = plus ? plusColor : minusColor;
            txt.raycastTarget = false; // prevent blocking clicks
        }
        else
        {
            Debug.LogWarning("MoneyDeltaController: TMP_Text missing on popupPrefab (root or child).");
        }

        // ensure the fader exists & animate
        var fader = go.GetComponent<FloatAndFade>();
        if (fader == null) fader = go.AddComponent<FloatAndFade>();
        fader.PlayLocal(Vector2.zero, new Vector2(0f, floatPixels), duration, true);
    }

}
