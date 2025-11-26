using UnityEngine;
using TMPro;
using FishNet.Object.Synchronizing; // เผื่อยังไม่ได้ใส่

public class PawnNameTag : MonoBehaviour
{
    public TMP_Text nameText;
    public Vector3 worldOffset = new Vector3(0f, 2f, 0f); // ปรับความสูงเหนือ pawn

    private Camera _cam;
    private PlayerPawn _pawn;

    public void Bind(PlayerPawn pawn)
    {
        _pawn = pawn;
        if (_cam == null) _cam = Camera.main;

        if (pawn != null && pawn.playerName != null)
        {
            // อ่านครั้งแรกจากค่า current
            SetName(pawn.playerName.Value);

            // สมัคร event เปลี่ยนชื่อ
            pawn.playerName.OnChange += OnNameChanged;
        }
    }

    private void OnDestroy()
    {
        if (_pawn != null && _pawn.playerName != null)
            _pawn.playerName.OnChange -= OnNameChanged;
    }

    // ต้องมี 3 พารามิเตอร์ตาม FishNet: old, next, asServer
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

        Vector3 worldPos  = _pawn.transform.position + worldOffset;
        Vector3 screenPos = _cam.WorldToScreenPoint(worldPos);

        // ถ้าอยู่หลังกล้องให้ซ่อน text
        if (screenPos.z < 0f)
        {
            if (nameText != null) nameText.enabled = false;
            return;
        }

        if (nameText != null) nameText.enabled = true;
        ((RectTransform)transform).position = screenPos;
    }
}
