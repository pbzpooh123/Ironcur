// TooltipUI.cs
using UnityEngine;
using TMPro;

public enum TooltipCorner { TopLeft, TopRight, BottomLeft, BottomRight }

public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance;

    [Header("Refs")]
    public RectTransform panel;      // the root RectTransform of the tooltip panel
    public TMP_Text titleTMP;
    public TMP_Text bodyTMP;

    [Header("Behaviour")]
    public bool pinned = true;                      // << set true to not follow mouse
    public TooltipCorner corner = TooltipCorner.BottomRight;
    public Vector2 margin = new Vector2(24, 24);    // distance from chosen corner (in pixels)

    private Canvas _canvas;
    private bool _visible;

    void Awake()
    {
        Instance = this;
        _canvas = GetComponentInParent<Canvas>();
        if (panel) panel.gameObject.SetActive(false);
        ApplyCornerAnchors();   // make anchors/pivot match the chosen corner
    }

    public void PinToCorner(TooltipCorner c, Vector2? customMargin = null)
    {
        corner = c;
        if (customMargin.HasValue) margin = customMargin.Value;
        pinned = true;
        ApplyCornerAnchors();
        RepositionPinned();
    }

    public void Unpin() { pinned = false; }

    public void Show(HoverInfo info)
    {
        if (!panel) return;

        titleTMP.text = string.IsNullOrEmpty(info.title) ? "—" : info.title;
        bodyTMP.text  = (info.lines != null && info.lines.Count > 0)
            ? string.Join("\n", info.lines)
            : "—";

        panel.gameObject.SetActive(true);
        _visible = true;

        if (pinned) RepositionPinned();
        // if not pinned, you might still have mouse-follow logic elsewhere; we leave it off.
    }

    public void Hide()
    {
        if (!panel) return;
        panel.gameObject.SetActive(false);
        _visible = false;
    }

    void LateUpdate()
    {
        if (!_visible || !panel) return;

        if (pinned)
        {
            RepositionPinned();
        }
        // else: intentionally do nothing → no mouse following
    }

    private void ApplyCornerAnchors()
    {
        if (!panel) return;

        Vector2 aMin, aMax, pivot;
        switch (corner)
        {
            case TooltipCorner.TopLeft:     aMin = aMax = pivot = new Vector2(0f, 1f); break;
            case TooltipCorner.TopRight:    aMin = aMax = pivot = new Vector2(1f, 1f); break;
            case TooltipCorner.BottomLeft:  aMin = aMax = pivot = new Vector2(0f, 0f); break;
            default:                        aMin = aMax = pivot = new Vector2(1f, 0f); break; // BottomRight
        }
        panel.anchorMin = aMin;
        panel.anchorMax = aMax;
        panel.pivot     = pivot;
    }

    private void RepositionPinned()
    {
        if (!panel) return;

        // anchoredPosition is from the chosen corner thanks to anchors/pivot.
        // For right corners X should be negative to move inward; for left it’s positive.
        // For top corners Y should be negative to move inward; for bottom it’s positive.

        Vector2 pos = Vector2.zero;

        switch (corner)
        {
            case TooltipCorner.BottomRight:
                pos = new Vector2(-margin.x,  margin.y);
                break;
            case TooltipCorner.BottomLeft:
                pos = new Vector2( margin.x,  margin.y);
                break;
            case TooltipCorner.TopRight:
                pos = new Vector2(-margin.x, -margin.y);
                break;
            case TooltipCorner.TopLeft:
                pos = new Vector2( margin.x, -margin.y);
                break;
        }

        panel.anchoredPosition = pos;
    }
}
