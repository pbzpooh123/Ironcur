using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class EventUI : MonoBehaviour
{
    public static EventUI Instance;

    public GameObject panel;
    public TMP_Text eventText;
    public Button okButton;

    private bool waitingForAll = false;
    private int readyCount = 0;

    private void Awake()
    {
        Instance = this;
        panel.SetActive(false);
    }

    public void Show(string msg, bool pauseAll)
    {
        panel.SetActive(true);
        eventText.text = msg;
        waitingForAll = pauseAll;
        readyCount = 0;

        okButton.onClick.RemoveAllListeners();
        okButton.onClick.AddListener(OnOk);
        // Disable End Turn while popup open
        TurnUI ui = FindObjectOfType<TurnUI>();
        if (ui != null) ui.ForceDisableEndTurn();
    }

    private void OnOk()
    {
        panel.SetActive(false);

        if (waitingForAll)
        {
            // Send "ready" to server
            EventManager.Instance.CmdPlayerReady();
        }
    }
}