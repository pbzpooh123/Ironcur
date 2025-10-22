using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TurnUI : MonoBehaviour
{
    [Header("Buttons")]
    public Button rollDiceButton;
    public Button endTurnButton;

    [Header("Optional Message/Banner (assign either)")]
    public TMP_Text messageTMP;      // TextMeshPro (optional)
    public Button portfolioButton;

    private PlayerPawn myPawn;

    public TMP_Text nextEventText;

    public static TurnUI Instance;

    void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (rollDiceButton != null) rollDiceButton.onClick.AddListener(OnRollDiceClicked);
        if (endTurnButton != null)  endTurnButton.onClick.AddListener(OnEndTurnClicked);
        if (portfolioButton != null)
            portfolioButton.onClick.AddListener(OnPortfolioClicked);
        
        SetRollInteractable(false);
        SetEndTurnInteractable(false);
        ClearMessage();
    }

    private void OnDestroy()
    {
        if (rollDiceButton != null) rollDiceButton.onClick.RemoveListener(OnRollDiceClicked);
        if (endTurnButton != null)  endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
    }

    public void BindPawn(PlayerPawn pawn)
    {
        myPawn = pawn;
        SetRollInteractable(false);
        SetEndTurnInteractable(false);
    }

    private void OnRollDiceClicked()
    {
        if (myPawn != null && myPawn.IsOwner)
        {
            myPawn.OnRollDiceButton();     
            SetRollInteractable(false); 
        }
    }

    private void OnEndTurnClicked()
    {
        if (myPawn != null && myPawn.IsOwner)
        {
            myPawn.OnEndTurnButton();     
            SetEndTurnInteractable(false); 
        }
    }
    
    private void OnPortfolioClicked()
    {
        // Find local pawn if not bound for any reason
        if (myPawn == null)
        {
            foreach (var p in GameObject.FindObjectsOfType<PlayerPawn>())
                if (p.IsOwner) { myPawn = p; break; }
        }

        if (PortfolioUI.Instance != null && myPawn != null)
            PortfolioUI.Instance.Show(myPawn);
    }

    public void SetEndTurnInteractable(bool enable)
    {
        if (endTurnButton != null) endTurnButton.interactable = enable;
    }

    public void SetRollInteractable(bool enable)
    {
        if (rollDiceButton != null) rollDiceButton.interactable = enable;
    }

    public void ForceDisableEndTurn() => SetEndTurnInteractable(false);
    
    public void FreezeAll()
    {
        SetRollInteractable(false);
        SetEndTurnInteractable(false);
    }
    
    public void ShowMessage(string msg)
    {
       if (messageTMP != null) messageTMP.text = msg;
    }

    public void ClearMessage()
    {
        if (messageTMP != null) messageTMP.text = "";
    }
    
    public void ShowToast(string msg, float seconds = 3f)
    {
        if (gameObject.activeInHierarchy)
            StartCoroutine(CoToast(msg, seconds));
    }

    private IEnumerator CoToast(string msg, float seconds)
    {
        if (messageTMP != null) messageTMP.text = msg;
        yield return new WaitForSeconds(seconds);
        if (messageTMP != null) messageTMP.text = "";
    }

    public void SetNextMainEventName(string name)
{
    if (nextEventText != null)
        nextEventText.text = $"Next Main Event: {name}";
}
}
