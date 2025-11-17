using UnityEngine;
using TMPro;

public enum TooltipCorner { TopLeft, TopRight, BottomLeft, BottomRight }

public class TooltipUI : MonoBehaviour
{
    public static TooltipUI Instance;

    [Header("Refs")]
    public RectTransform panel;      // root RectTransform of the tooltip panel
    public TMP_Text titleTMP;
    public TMP_Text bodyTMP;

    [Header("Behaviour")]
    [Tooltip("If true, tooltip is stuck to a screen corner. If false, it follows the mouse.")]
    public bool pinned = false;                     // DEFAULT: follow mouse
    public TooltipCorner corner = TooltipCorner.BottomRight;
    public Vector2 margin = new Vector2(24, 24);    // when pinned

    [Header("Mouse follow")]
    [Tooltip("Offset from mouse position in screen space.")]
    public Vector2 mouseOffset = new Vector2(16f, -16f);

    private Canvas _canvas;
    private bool _visible;

    void Awake()
    {
        Instance = this;

        // Make sure we actually have a canvas, even if TooltipUI is not parented directly under one
        _canvas = GetComponentInParent<Canvas>();
        if (_canvas == null)
            _canvas = FindObjectOfType<Canvas>();

        if (panel) panel.gameObject.SetActive(false);

        if (pinned)
            ApplyCornerAnchors();
    }

    public void PinToCorner(TooltipCorner c, Vector2? customMargin = null)
    {
        corner = c;
        if (customMargin.HasValue) margin = customMargin.Value;
        pinned = true;
        ApplyCornerAnchors();
        RepositionPinned();
    }

    public void Unpin()
    {
        pinned = false;
    }

    public void Show(HoverInfo info)
    {
        if (!panel) return;

        titleTMP.text = string.IsNullOrEmpty(info.title) ? "—" : info.title;
        bodyTMP.text  = (info.lines != null && info.lines.Count > 0)
            ? string.Join("\n", info.lines)
            : "—";

        panel.gameObject.SetActive(true);
        _visible = true;

        if (pinned)
            RepositionPinned();
        else
            FollowMouse();
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
            RepositionPinned();
        else
            FollowMouse();
    }

    /* ================= Corner mode (optional) ================= */

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

    /* ================= Mouse-follow mode ================= */

    private void FollowMouse()
    {
        if (panel == null) return;

        // If Canvas is Screen Space - Overlay (most common) → just use screen position directly.
        if (_canvas == null || _canvas.renderMode == RenderMode.ScreenSpaceOverlay)
        {
            Vector2 screenPos = Input.mousePosition;
            screenPos += mouseOffset;
            panel.position = screenPos;
            return;
        }

        // For Screen Space - Camera or World Space
        RectTransform parentRect = panel.parent as RectTransform;
        if (parentRect == null) return;

        Vector2 screenP = Input.mousePosition;
        Vector2 localPos;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parentRect,
            screenP,
            _canvas.worldCamera,
            out localPos
        );

        localPos += mouseOffset;

        // Optional clamp to parent rect
        Rect parentBounds = parentRect.rect;
        Vector2 size = panel.rect.size;
        Vector2 pivot = panel.pivot;

        float minX = parentBounds.xMin + size.x * pivot.x;
        float maxX = parentBounds.xMax - size.x * (1f - pivot.x);
        float minY = parentBounds.yMin + size.y * pivot.y;
        float maxY = parentBounds.yMax - size.y * (1f - pivot.y);

        localPos.x = Mathf.Clamp(localPos.x, minX, maxX);
        localPos.y = Mathf.Clamp(localPos.y, minY, maxY);

        panel.anchoredPosition = localPos;
    }
}
