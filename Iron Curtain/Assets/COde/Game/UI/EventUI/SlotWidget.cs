using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class SlotWidget : MonoBehaviour
{
    [Header("Refs")]
    public TMP_Text nameText;
    public Image diceImage;
    public TMP_Text resultText;
    public Button rollButton;

    [Header("Sprites")]
    public Sprite[] diceFaces; 

    private System.Action _onRoll;
    private int _cid;

    public void Bind(int cid, string displayName)
    {
        _cid = cid;
        if (nameText) nameText.text = displayName;
        if (resultText) resultText.text = "—";
        if (diceImage) diceImage.sprite = null; 
    }

    public void SetButtonEnabled(bool enabled, System.Action onRollClick)
    {
        Debug.Log($"Setting button enabled: {enabled}");
        _onRoll = onRollClick;
        if (rollButton)
        {
            rollButton.onClick.RemoveAllListeners();
            rollButton.interactable = enabled;
            if (enabled && onRollClick != null)
                rollButton.onClick.AddListener(() => _onRoll?.Invoke());
        }
    }

    public void SetRolling()
    {
        if (resultText) resultText.text = "Rolling…";
      
    }

    public void SetRolled(int value, string outcome, Sprite diceFaceFromParent = null)
    {
        Debug.Log($"Set");
        if (resultText) resultText.text = outcome; 
        if (diceImage)
        {
            if (diceFaceFromParent != null) diceImage.sprite = diceFaceFromParent;
            else if (diceFaces != null && diceFaces.Length >= 6 && value >= 1 && value <= 6)
                diceImage.sprite = diceFaces[value - 1];
        }
    }
}
