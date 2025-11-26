using UnityEngine;
using TMPro;

public class PawnNameTag : MonoBehaviour
{
    public TMP_Text nameText;

    [Header("World-space offset (X,Y,Z)")]
    public Vector3 worldOffset = new Vector3(0f, 2f, 0f);

    [Header("Screen-space offset (pixels)")]
    public Vector2 screenOffset = new Vector2(0f, 30f);

    private Camera _cam;
    private PlayerPawn _pawn;
    private RectTransform _rt;

    public void Bind(PlayerPawn pawn)
    {
        _pawn = pawn;
        if (_cam == null) _cam = Camera.main;
        if (_rt == null) _rt = (RectTransform)transform;

        if (pawn != null && pawn.playerName != null)
        {
            SetName(pawn.playerName.Value);
            pawn.playerName.OnChange += OnNameChanged; // FishNet SyncVar event
        }
    }

    private void Awake()
    {
        _rt = (RectTransform)transform;
    }

    private void OnDestroy()
    {
        if (_pawn != null && _pawn.playerName != null)
            _pawn.playerName.OnChange -= OnNameChanged;
    }

    private void OnNameChanged(string oldVal, string newVal, bool asServer)
    {
        SetName(newVal);
    }

    private void SetName(string n)
    {
        if (nameText != null)
            nameText.text = n;
    }

    private void LateUpdate()
    {
        if (_pawn == null)
            return;

        if (_cam == null)
            _cam = Camera.main;
        if (_cam == null)
            return;

        // 1) world offset (X,Y,Z) above/around pawn
        Vector3 worldPos  = _pawn.transform.position + worldOffset;
        Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);

        // 2) behind camera → hide
        if (screenPos.z < 0f)
        {
            if (nameText != null) nameText.enabled = false;
            return;
        }

        if (nameText != null) nameText.enabled = true;

        // 3) extra screen-space offset (pixels)
        screenPos.x += screenOffset.x;
        screenPos.y += screenOffset.y;

        // 4) apply to UI rect
        if (_rt != null)
            _rt.position = screenPos;
    }
}
