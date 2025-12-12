using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerInfoPanel : MonoBehaviour
{
    [Header("Refs")]
    public TMP_Text nameText;
    public TMP_Text profitText;

    [Header("Optional")]
    public TMP_Text turnOrderText; 
    private readonly Dictionary<string, TMP_Text> ownershipEntries = new();

    [Header("Actions")]
    public Button portfolioButton;
    public Button statsButton;             

    // Identity for this panel’s player (serialized so you can see it in Inspector during play)
    [SerializeField, HideInInspector] private int _ownerCid = -1;
    [SerializeField, HideInInspector] private string _ownerName = "";

    public int OwnerCid => _ownerCid;
    public string OwnerName => _ownerName;

    [Header("Turn Highlight")]
    public Image highlightImage;
    public Color activeColor = new Color(1f, 0.9f, 0.3f, 0.75f);
    public Color idleColor   = new Color(1f, 1f, 1f, 0.15f);

    public float pulseScale = 1.05f;      // how big the “bounce” is when active
public float pulseSpeed = 2f;         // how fast it pulses

    private Coroutine _pulseCo;
    private string _baseName = "";


    public TMP_Text bailoutText;

    private static Sprite _fallbackSprite;
    private static Sprite GetFallbackSprite()
    {
        if (_fallbackSprite != null) return _fallbackSprite;

        var tex = new Texture2D(1, 1);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        _fallbackSprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f));
        return _fallbackSprite;
    }

     void Awake()
    {
        if (highlightImage != null)
        {
            // Ensure drawable sprite exists (solid overlay fallback)
            if (highlightImage.sprite == null)
            {
                var tex = Texture2D.whiteTexture;
                highlightImage.sprite = Sprite.Create(
                    tex, new Rect(0,0,tex.width,tex.height), new Vector2(0.5f,0.5f));
                highlightImage.type = Image.Type.Simple;
            }

            highlightImage.raycastTarget = false;
            highlightImage.enabled = true;
            highlightImage.color = idleColor;

            // Sit on top to avoid being hidden by other children
            highlightImage.transform.SetAsFirstSibling();
        }
    }

    public void SetTurnActive(bool isActive)
    {
        if (!highlightImage) return;

        // background color
        var c = isActive ? activeColor : idleColor;
        c.a = Mathf.Clamp01(c.a);
        highlightImage.color = c;
        highlightImage.enabled = true;

        // make sure highlight is on top
        highlightImage.transform.SetAsFirstSibling();
        highlightImage.raycastTarget = false;

        // name text style + label
        if (nameText != null)
        {
            if (string.IsNullOrEmpty(_baseName))
                _baseName = nameText.text;

            if (isActive)
            {
                // e.g. "▶ PlayerName (YOUR TURN)"
                nameText.text = $"▶ {_baseName}";
                nameText.color = Color.yellow;
                nameText.fontStyle = FontStyles.Bold;
                turnOrderText.text = $"ตาของคุณ";
            }
            else
            {
                nameText.text = _baseName;
                nameText.color = Color.white;
                nameText.fontStyle = FontStyles.Normal;
            }
        }

        // pulse animation
        if (isActive)
        {
            if (_pulseCo != null) StopCoroutine(_pulseCo);
            _pulseCo = StartCoroutine(CoPulse());
        }
        else
        {
            if (_pulseCo != null) StopCoroutine(_pulseCo);
            _pulseCo = null;
            transform.localScale = new Vector3(0.452855f, 0.3031918f, 1f);
        }

        // keep portfolio button usable
        if (portfolioButton) portfolioButton.interactable = true;
    }

    private IEnumerator CoPulse()
    {
        var t = 0f;
        while (true)
        {
            t += Time.unscaledDeltaTime * pulseSpeed;
            float s = 0.45f + (Mathf.Sin(t) * 0.05f + 0.05f) * (pulseScale - 1f);
            transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
    }



    public void SetInfo(string name, int money = 0)
    {
        _ownerName = name;
        _baseName = name; 
        if (nameText) nameText.text = name;
        UpdateMoney(money);
        UpdateBailoutMarks(0);
    }

    public void SetOwnerCid(int cid)
    {
        _ownerCid = cid;
        GameHUD.Instance?.NotifyPanelCidChanged(this, cid);
    }

    public void UpdateMoney(int money)
    {
        if (profitText) profitText.text = $"เงิน:{money:0} M";
    }

    public void SetTurnOrder(int orderIndex) 
    {
        if (turnOrderText) turnOrderText.text = $"";
    }

    public void UpdateBailoutMarks(int marks)
    {
        if (bailoutText != null)
            bailoutText.text = $"ใบแจ้งหนี้: {marks}";
    }

    

}

