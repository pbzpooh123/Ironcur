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
    public Button portfolioButton;             // assign in prefab

    // Identity for this panel’s player (serialized so you can see it in Inspector during play)
    [SerializeField, HideInInspector] private int _ownerCid = -1;
    [SerializeField, HideInInspector] private string _ownerName = "";

    public int OwnerCid => _ownerCid;
    public string OwnerName => _ownerName;

    [Header("Turn Highlight")]
    public Image highlightImage;
    public Color activeColor = new Color(1f, 0.9f, 0.3f, 0.75f);
    public Color idleColor   = new Color(1f, 1f, 1f, 0.15f);

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

        // color swap
        var c = isActive ? activeColor : idleColor;
        c.a = Mathf.Clamp01(c.a);
        highlightImage.color = c;
        highlightImage.enabled = true;

        var cg = highlightImage.GetComponent<CanvasGroup>();
        if (cg == null) cg = highlightImage.gameObject.AddComponent<CanvasGroup>();
        cg.alpha = 1f;
        cg.blocksRaycasts = false;     
        cg.interactable   = true;     
        cg.ignoreParentGroups = true;  

   
        highlightImage.raycastTarget = false;
        highlightImage.transform.SetAsFirstSibling();


        if (nameText != null) nameText.color = isActive ? Color.yellow : Color.white;

       
        if (portfolioButton) portfolioButton.interactable = true;
    }


    public void SetInfo(string name, int money = 0)
    {
        _ownerName = name;
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
        if (profitText) profitText.text = $"Money: {money:0} M";
    }

    public void SetTurnOrder(int orderIndex) // 1-based
    {
        if (turnOrderText) turnOrderText.text = $"Turn #{orderIndex}";
    }

    public void UpdateBailoutMarks(int marks)
    {
        if (bailoutText != null)
            bailoutText.text = $"ใบแจ้งหนี้: {marks}";
    }

}

