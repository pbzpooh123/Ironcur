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
    public TMP_Text SideeventText;
    public Button SideokButton;
    
    private bool waitingForAll = false;
    private int readyCount = 0;

    private void Awake()
    {
        Instance = this;
        Mainpanel.SetActive(false);
        Sidepanel.SetActive(false);
    }

    public void MaineventShow(string title, string body, bool pauseAll)
    {
        Mainpanel.SetActive(true);
        eventText.text = title;   // title line
        HistoryText.text = body;  // description / body
        waitingForAll = pauseAll;
        readyCount = 0;

        okButton.onClick.RemoveAllListeners();
        okButton.onClick.AddListener(OnOk);

        var ui = FindObjectOfType<TurnUI>();
        if (ui != null)
        {
            ui.SetRollInteractable(false);
            ui.ForceDisableEndTurn();
        }
    }

    public void SideeventShow(string msg, bool pauseAll)
    {
        Sidepanel.SetActive(true);
        SideeventText.text = msg;
        waitingForAll = pauseAll;
        readyCount = 0;

        SideokButton.onClick.RemoveAllListeners();
        SideokButton.onClick.AddListener(OnSideOk);

        var ui = FindObjectOfType<TurnUI>();
        if (ui != null)
        {
            ui.SetRollInteractable(false);
            ui.ForceDisableEndTurn();
        }
    }

    private void OnOk()
    {
        Mainpanel.SetActive(false);

        if (waitingForAll)
        {
            // Send "ready" to server
            EventManager.Instance.CmdPlayerReady();
        }
    }
    
    private void OnSideOk()
    {
        Sidepanel.SetActive(false);
        if (waitingForAll)
        {
            // Send "ready" to server
            TurnManager.Instance.CmdTileActionReady();
        }
    }
    
    public System.Action onSideeventReady;

    public void SetSideeventReadyCallback(System.Action cb)
    {
        onSideeventReady = cb;
    }
    
    public void OnSideEventReadyClicked()
    {
        var cb = onSideeventReady;
        onSideeventReady = null;
        cb?.Invoke();   // This will call pawn.CmdTileActionComplete()
        OnSideOk();
    }
}