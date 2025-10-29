using UnityEngine;
using UnityEngine.EventSystems;

public class HoverRaycaster : MonoBehaviour
{
    [Header("Ray")]
    public LayerMask hoverMask;
    public bool useSphereCast = true;
    public float sphereRadius = 0.15f;
    public float maxDistance = 999f;

    [Header("Timing")]
    public float showDelay = 0.15f;     // hover dwell before show
    public float hideLinger = 0.10f;    // keep tooltip alive briefly on miss
    public float moveThreshold = 6f;    // px; ignore tiny mouse jitter

    [Header("Debug")]
    public bool debugDraw = true;               // draw rays & hits
    public bool debugLog  = false;              // print state changes
    public Color debugRayColor = Color.cyan;    // no-hit ray color
    public Color debugHitColor = Color.green;   // hit segment color
    public Color debugMissColor = Color.red;    // draw to max when miss
    public float debugHitMarkSize = 0.15f;      // cross size at hit
    public float debugDrawTime = 0f;            // 0 = 1 frame, >0 = seconds

    private IHoverProvider _currentProvider;
    private float _hoverTimer;
    private float _lastSeenTime;
    private Vector3 _lastMouse;

    // debug throttling
    private string _lastDebugState = "";
    private float _lastLogTime = -999f;
    private float _logCadence = 0.25f; // seconds

    void Update()
    {
        // If pointer over *some* UI, but allow our tooltip panel itself
        if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
        {
            DebugNote("PointerOverUI");
            TryHideIfStale();
            return;
        }

        var cam = Camera.main;
        if (!cam)
        {
            DebugNote("NoCamera");
            TryHideIfStale();
            return;
        }

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);

        bool hitAny = false;
        IHoverProvider provider = null;
        RaycastHit hit = default;

        if (useSphereCast)
        {
            if (Physics.SphereCast(ray, sphereRadius, out hit, maxDistance, hoverMask, QueryTriggerInteraction.Collide))
            {
                provider = hit.collider.GetComponentInParent<IHoverProvider>();
                hitAny = true;
            }
        }
        else
        {
            if (Physics.Raycast(ray, out hit, maxDistance, hoverMask, QueryTriggerInteraction.Collide))
            {
                provider = hit.collider.GetComponentInParent<IHoverProvider>();
                hitAny = true;
            }
        }

        // --- Debug draw the ray path ---
        if (debugDraw)
        {
            if (hitAny)
            {
                // Draw the ray to the hit in green, and a small cross at the hit.
                Debug.DrawRay(ray.origin, ray.direction * hit.distance, debugHitColor, debugDrawTime);
                DrawHitCross(hit.point, hit.normal, debugHitMarkSize, debugHitColor, debugDrawTime);

                // Optionally draw the remainder (ray after hit) in a faded color
                float remain = Mathf.Max(0f, maxDistance - hit.distance);
                if (remain > 0.001f)
                {
                    Vector3 afterStart = hit.point + ray.direction * 0.001f;
                    Debug.DrawRay(afterStart, ray.direction * (remain - 0.001f), debugRayColor * 0.6f, debugDrawTime);
                }
            }
            else
            {
                // No hit: draw full-length ray in red
                Debug.DrawRay(ray.origin, ray.direction * maxDistance, debugMissColor, debugDrawTime);
            }
        }

        if (hitAny && provider != null)
        {
            DebugNote($"Hit:{hit.collider.name} Provider:{provider.GetType().Name}");

            // mouse moved a lot? reset dwell; small jiggle? keep counting
            float moved = (Input.mousePosition - _lastMouse).magnitude;
            _lastMouse = Input.mousePosition;

            if (provider != _currentProvider)
            {
                _currentProvider = provider;
                _hoverTimer = 0f;
                DebugNote("ProviderChanged->ResetDwell");
            }
            else
            {
                if (moved > moveThreshold)
                {
                    _hoverTimer = 0f;
                    DebugNote($"MouseMoved>{moveThreshold:0} reset dwell");
                }
                else
                {
                    _hoverTimer += Time.unscaledDeltaTime;
                    if (_hoverTimer >= showDelay)
                    {
                        var info = provider.BuildHoverInfo();
                        TooltipUI.Instance?.Show(info);
                        DebugNote("Tooltip.Show");
                    }
                }
            }

            _lastSeenTime = Time.unscaledTime;
            return;
        }

        // small miss? linger a bit before hiding
        DebugNote(hitAny ? "HitNoProvider" : "NoHit");
        TryHideIfStale();
    }

    private void TryHideIfStale()
    {
        if (TooltipUI.Instance == null) return;

        // Keep visible for 'hideLinger' after last good hit
        if (_currentProvider != null && Time.unscaledTime - _lastSeenTime <= hideLinger)
            return;

        if (_currentProvider != null)
            DebugNote("Tooltip.Hide");

        _currentProvider = null;
        _hoverTimer = 0f;
        TooltipUI.Instance.Hide();
    }

    private void DrawHitCross(Vector3 point, Vector3 normal, float size, Color c, float time)
    {
        // Build two perpendicular directions to draw a small cross
        Vector3 n = (normal.sqrMagnitude > 0.001f) ? normal.normalized : Vector3.up;
        Vector3 t = Vector3.Cross(n, Vector3.right);
        if (t.sqrMagnitude < 0.001f) t = Vector3.Cross(n, Vector3.forward);
        t.Normalize();
        Vector3 b = Vector3.Cross(n, t).normalized;

        Vector3 a1 = point - t * size;
        Vector3 a2 = point + t * size;
        Vector3 b1 = point - b * size;
        Vector3 b2 = point + b * size;

        Debug.DrawLine(a1, a2, c, time);
        Debug.DrawLine(b1, b2, c, time);
    }

    private void DebugNote(string state)
    {
        if (!debugLog) return;

        // Only log when the state changes, and not more than once per cadence.
        if (state == _lastDebugState && (Time.unscaledTime - _lastLogTime) < _logCadence)
            return;

        _lastDebugState = state;
        _lastLogTime = Time.unscaledTime;
        Debug.Log($"[HoverRaycaster] {state}");
    }
}


// keep your interface
public interface IHoverProvider
{
    HoverInfo BuildHoverInfo();
}
