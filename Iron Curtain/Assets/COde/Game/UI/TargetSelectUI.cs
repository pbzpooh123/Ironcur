using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class TargetSelectUI : MonoBehaviour
{
    public static TargetSelectUI Instance;
    public GameObject panel;
    public TMP_Dropdown dropdown;
    public Button confirmButton;
    public TMP_Text titleText;

    private void Awake() => Instance = this;

    public void Show(string serializedNames)
    {
        panel.SetActive(true);
        if (titleText) titleText.text = "Select a target player";

        dropdown.ClearOptions();
        var arr = serializedNames.Split('|');
        dropdown.AddOptions(new System.Collections.Generic.List<string>(arr));

        confirmButton.onClick.RemoveAllListeners();
        confirmButton.onClick.AddListener(() =>
        {
            var name = dropdown.options[dropdown.value].text;
            EventManager.Instance.CmdSubmitTargetSelect(name);
            panel.SetActive(false);
        });
    }
}