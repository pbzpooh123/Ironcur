using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class EventUI : MonoBehaviour
{
    public static EventUI Instance;

    [Header("Main Event")]
    public GameObject Mainpanel;
    public TMP_Text eventText;
    public TMP_Text HistoryText;
    public Button okButton;

    [Header("Side Event")]
    public GameObject Sidepanel;
    public TMP_Text SideeventTitleText;
    public TMP_Text SideeventText;
    public Button SideokButton;

    private bool waitingForAllMain = false;

    // callback for blocking side events
    public System.Action onSideeventReady;

    private void Awake()
    {
        Instance = this;
        Mainpanel.SetActive(false);
        Sidepanel.SetActive(false);
    }

    /* ========== MAIN EVENT ========== */

    public void MaineventShow(string title, string body, bool pauseAll)
    {
        Mainpanel.SetActive(true);
        eventText.text = title;
        HistoryText.text = body;
        waitingForAllMain = pauseAll;

        okButton.onClick.RemoveAllListeners();
        okButton.onClick.AddListener(OnOkMain);

        var ui = FindObjectOfType<TurnUI>();
        if (ui != null)
        {
            ui.SetRollInteractable(false);
            ui.ForceDisableEndTurn();
        }
    }

    public void OnOkMain()
    {
        Mainpanel.SetActive(false);

        if (waitingForAllMain && EventManager.Instance != null)
            EventManager.Instance.CmdPlayerReady();
    }

    public void SideeventShow(string title,string msg, bool pauseAll)
    {
        Sidepanel.SetActive(true);
        SideeventTitleText.text = title;
        SideeventText.text = msg;

        SideokButton.onClick.RemoveAllListeners();

        if (pauseAll)
        {
            // Blocking side event – use callback
            SideokButton.onClick.AddListener(OnSideEventReadyClicked);
        }
        else
        {
            // Non-blocking – only close
            SideokButton.onClick.AddListener(() =>
            {
                Sidepanel.SetActive(false);
            });
        }

        var ui = FindObjectOfType<TurnUI>();
        if (ui != null)
        {
            ui.SetRollInteractable(false);
            ui.ForceDisableEndTurn();
        }
    }

    public void SetSideeventReadyCallback(System.Action cb)
    {
        onSideeventReady = cb;
    }

    public void OnSideEventReadyClicked()
    {
        Sidepanel.SetActive(false);

        var cb = onSideeventReady;
        onSideeventReady = null;
        cb?.Invoke();   // e.g. TurnManager.Instance.CmdTileActionReady()
    }
}
