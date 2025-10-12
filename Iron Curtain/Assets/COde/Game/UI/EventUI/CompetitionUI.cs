using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class CompetitionUI : MonoBehaviour
{
    public static CompetitionUI Instance;
    public GameObject panel;
    public TMP_Text titleText;
    public TMP_Text statusText;
    public Button rollButton;

    private void Awake() => Instance = this;

    public void Show(string title)
    {
        if (titleText) titleText.text = title;
        if (statusText) statusText.text = "Waiting for your roll...";
        panel.SetActive(true);

        rollButton.onClick.RemoveAllListeners();
        rollButton.onClick.AddListener(() =>
        {
            int r = Random.Range(1, 7);
            EventManager.Instance.CmdSubmitCompetitionRoll(r);
            UpdateStatus($"You rolled: {r}. Please wait...");
            rollButton.interactable = false;
        });
    }

    public void UpdateStatus(string s)
    {
        if (statusText) statusText.text = s;
    }

    public void Hide()
    {
        panel.SetActive(false);
    }
}