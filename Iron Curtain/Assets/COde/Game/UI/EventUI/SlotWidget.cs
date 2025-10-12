using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SlotWidget : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text resultText;
    public Image diceImage;
    public TMP_Text diceNumber;    // if you don’t use sprite numbers
    public Button rollButton;

    int _cid;

    public void Bind(int cid, string displayName)
    {
        _cid = cid;
        if (nameText) nameText.text = displayName;
        if (resultText) resultText.text = "Waiting…";
        SetDice(0);
    }

    public void SetButtonEnabled(bool enabled, System.Action onClick)
    {
        if (rollButton)
        {
            rollButton.gameObject.SetActive(enabled);
            rollButton.onClick.RemoveAllListeners();
            if (enabled && onClick != null) rollButton.onClick.AddListener(() => onClick());
        }
    }

    public void SetRolling()
    {
        if (resultText) resultText.text = "Rolling…";
    }

    public void SetRolled(int value, string outcome)
    {
        SetDice(value);
        if (resultText) resultText.text = $"Rolled {value}: {outcome}";
        SetButtonEnabled(false, null);
    }

    void SetDice(int value)
    {
        // Option A: show number
        if (diceNumber)
            diceNumber.text = (value <= 0 ? "-" : value.ToString());

        // Option B (optional): set sprite by value
        // if (diceImage) diceImage.sprite = DiceAtlas.Get(value);
    }
}