using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class PlayerEntryRow : MonoBehaviour
{
    public TMP_Text nameText;
    public Toggle readyToggle;

    public void Bind(string playerName, bool isReady, bool isLocal, System.Action<bool> onLocalToggleChanged)
    {
        if (nameText) nameText.text = playerName;

        if (readyToggle)
        {
            readyToggle.onValueChanged.RemoveAllListeners();
            readyToggle.isOn = isReady;
            readyToggle.interactable = isLocal;
            if (isLocal && onLocalToggleChanged != null)
                readyToggle.onValueChanged.AddListener(onLocalToggleChanged.Invoke);
        }
    }

    public void SetNameColor(Color c)
    {
        if (nameText) nameText.color = c;
    }
}
