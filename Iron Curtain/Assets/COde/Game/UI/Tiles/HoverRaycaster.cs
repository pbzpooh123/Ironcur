// HoverRaycaster.cs
using UnityEngine;
using UnityEngine.EventSystems;

public class HoverRaycaster : MonoBehaviour
{
    [Header("Raycast")]
    [Tooltip("Which layers can be hovered (tiles).")]
    public LayerMask hoverMask = ~0;
    [Tooltip("Use Physics2D if your tiles have 2D colliders.")]
    public bool usePhysics2D = false;
    [Tooltip("Max ray distance.")]
    public float maxDistance = 2000f;

    [Header("Behaviour")]
    [Tooltip("Delay before showing tooltip (seconds).")]
    public float showDelay = 0.15f;

    private float _hoverTime;
    private IHoverProvider _currentProvider;
    private TileHoverHighlight _currentHL;

    void Update()
    {
        // Don’t hover when cursor is over UI
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            ClearHover();
            return;
        }

        var cam = Camera.main;
        if (!cam)
        {
            ClearHover();
            return;
        }

        // --- Raycast 3D or 2D ---
        IHoverProvider hitProvider = null;
        TileHoverHighlight hitHL = null;

        if (!usePhysics2D)
        {
            Ray ray = cam.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out var hit, maxDistance, hoverMask))
            {
                var tr = hit.collider.transform;
                hitProvider = tr.GetComponentInParent<IHoverProvider>();
                hitHL       = tr.GetComponentInParent<TileHoverHighlight>();
            }
        }
        else
        {
            var wp = cam.ScreenToWorldPoint(Input.mousePosition);
            var hit = Physics2D.Raycast(wp, Vector2.zero, 0f, (int)hoverMask);
            if (hit.collider != null)
            {
                var tr = hit.collider.transform;
                hitProvider = tr.GetComponentInParent<IHoverProvider>();
                hitHL       = tr.GetComponentInParent<TileHoverHighlight>();
            }
        }

        // --- Handle switching target ---
        if (hitProvider != null)
        {
            if (hitProvider != _currentProvider)
            {
                _currentProvider = hitProvider;
                _hoverTime = 0f;

                // switch highlight
                if (_currentHL && _currentHL != hitHL) _currentHL.Set(false);
                _currentHL = hitHL;
                _currentHL?.Set(true);

                // hide tooltip immediately; we’ll re-show after delay
                TooltipUI.Instance?.Hide();
            }
            else
            {
                _hoverTime += Time.unscaledDeltaTime;
                if (_hoverTime >= showDelay)
                {
                    if (TooltipUI.Instance != null)
                    {
                        var info = _currentProvider.BuildHoverInfo();
                        TooltipUI.Instance.Show(info);
                    }
                }
            }
            return; // still hovering the same tile
        }

        // no hoverable hit
        ClearHover();
    }

    private void ClearHover()
    {
        _currentProvider = null;
        _hoverTime = 0f;

        if (_currentHL) { _currentHL.Set(false); _currentHL = null; }
        TooltipUI.Instance?.Hide();
    }
}

// keep your interface
public interface IHoverProvider
{
    HoverInfo BuildHoverInfo();
}
