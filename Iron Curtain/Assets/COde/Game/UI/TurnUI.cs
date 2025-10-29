using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TurnUI : MonoBehaviour
{
    [Header("Buttons")]
    public Button rollDiceButton;
    public Button endTurnButton;
    public Button portfolioButton;

    [Header("Message/Banner (optional)")]
    public TMP_Text messageTMP;

    [Header("Rounds")]
    public TMP_Text roundsLeftText;

    [Header("Next Main Event (collapsible)")]
    [Tooltip("Parent panel that contains the next event UI (title, history, ETA row).")]
    public GameObject nextEventContainer;     // drag the whole section here
    public Button nextEventToggleButton;      // a small arrow button in the header
    public RectTransform nextEventChevron;    // Image RectTransform of the chevron icon

    [Space(6)]
    public TMP_Text nextMainEventText;        // event title/name (from EventManager)
    public TMP_Text nextMainEventHistory;     // short history/blurb (from EventManager)
    public TMP_Text nextMainEventEtaText;     // “Main event in X rounds” (from TurnManager)

    private PlayerPawn myPawn;
    public static TurnUI Instance;

    // Persist (optional). Remove if you don’t want persistence.
    private const string PREF_NEXT_EVENT_COLLAPSED = "ui.nextEventCollapsed";
    private bool _nextEventCollapsed = false;

    private bool _hasPendingState;
    private TurnPhase _pendingPhase;
    private int _pendingOwnerCid = -1;
    private Coroutine _applyCo;

    [Header("Turn Timer")]
    public TMP_Text phaseLabelText;  // "Review", "Rolling", etc.
    public TMP_Text phaseTimerText; 

    [Header("Pause")]
    public UnityEngine.UI.Button pauseButton;  // optional


    void Awake()
    {
        Instance = this;
    }

    private void Start()
    {
        if (rollDiceButton)      rollDiceButton.onClick.AddListener(OnRollDiceClicked);
        if (endTurnButton)       endTurnButton.onClick.AddListener(OnEndTurnClicked);
        if (portfolioButton)     portfolioButton.onClick.AddListener(OnPortfolioClicked);
        if (nextEventToggleButton) nextEventToggleButton.onClick.AddListener(ToggleNextEventPanel);

        SetRollInteractable(false);
        SetEndTurnInteractable(false);
        ClearMessage();

        // Load collapsed state (optional)
        _nextEventCollapsed = PlayerPrefs.GetInt(PREF_NEXT_EVENT_COLLAPSED, 0) == 1;
        ApplyNextEventCollapsed();

        if (pauseButton) pauseButton.onClick.AddListener(() =>
        {
            // Client asks server to toggle pause (host actually decides)
            bool wantPause = !(PauseManager.Instance?.isPaused.Value ?? false);
            PauseManager.Instance?.CmdSetPaused(wantPause);
        });

    }

    private void OnDestroy()
    {
        if (rollDiceButton)      rollDiceButton.onClick.RemoveListener(OnRollDiceClicked);
        if (endTurnButton)       endTurnButton.onClick.RemoveListener(OnEndTurnClicked);
        if (nextEventToggleButton) nextEventToggleButton.onClick.RemoveListener(ToggleNextEventPanel);
    }

    public void BindPawn(PlayerPawn pawn)
    {
        myPawn = pawn;
        SetRollInteractable(false);
        SetEndTurnInteractable(false);
    }

    

        private PlayerPawn GetOrFindLocalPawn()
        {
            if (myPawn != null && myPawn && myPawn.IsOwner) return myPawn;
            foreach (var p in GameObject.FindObjectsOfType<PlayerPawn>())
                if (p != null && p.IsOwner) { myPawn = p; break; }
            return myPawn;
  }

    private void OnRollDiceClicked()
    {
        var p = GetOrFindLocalPawn();
        if (p != null && p.IsOwner)
        {
            p.OnRollDiceButton();
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
        if (endTurnButton) endTurnButton.interactable = enable;
    }

    public void SetRollInteractable(bool enable)
    {
        if (rollDiceButton) rollDiceButton.interactable = enable;
    }

    public void ForceDisableEndTurn() => SetEndTurnInteractable(false);

    public void FreezeAll()
    {
        SetRollInteractable(false);
        SetEndTurnInteractable(false);
    }

    public void ShowMessage(string msg)
    {
        if (messageTMP) messageTMP.text = msg;
    }

    public void ClearMessage()
    {
        if (messageTMP) messageTMP.text = "";
    }

    /* ---------- Next Main Event content (from EventManager) ---------- */
    public void SetNextMainEvent(string name, string history)
    {
        // NOTE: you had a small name mismatch earlier (nextEventText vs nextMainEventText).
        if (nextMainEventText)
            nextMainEventText.text = string.IsNullOrWhiteSpace(name) ? "—" : name;

        if (nextMainEventHistory)
        {
            if (string.IsNullOrWhiteSpace(history))
            {
                nextMainEventHistory.gameObject.SetActive(false);
            }
            else
            {
                nextMainEventHistory.gameObject.SetActive(true);
                nextMainEventHistory.text = history;
            }
        }
    }

    /* ---------- Next Main Event ETA (from TurnManager) ---------- */
    public void SetNextMainEventETA(int rounds)
    {
        if (!nextMainEventEtaText) return;

        if (rounds <= 0)
        {
            nextMainEventEtaText.text = "Main event: now";
        }
        else if (rounds == 1)
        {
            nextMainEventEtaText.text = "Main event in 1 round";
        }
        else
        {
            nextMainEventEtaText.text = $"Main event in {rounds} rounds";
        }
    }

    /* ---------- Collapse/Expand behaviour ---------- */
    public void ToggleNextEventPanel()
    {
        _nextEventCollapsed = !_nextEventCollapsed;
        ApplyNextEventCollapsed();

        // Persist (optional)
        PlayerPrefs.SetInt(PREF_NEXT_EVENT_COLLAPSED, _nextEventCollapsed ? 1 : 0);
        PlayerPrefs.Save();
    }

    private void ApplyNextEventCollapsed()
    {
        if (nextEventContainer)
            nextEventContainer.SetActive(!_nextEventCollapsed);

        // Rotate chevron: collapsed = pointing right, expanded = down.
        if (nextEventChevron)
        {
            // Adjust angles to match your art if needed.
            float z = _nextEventCollapsed ? 0f : 0f;
            nextEventChevron.localEulerAngles = new Vector3(0f, 0f, z);
        }
    }


    public void ApplyTurnState(TurnPhase phase, int ownerCid)
    {
         _pendingPhase = phase;
        _pendingOwnerCid = ownerCid;
        _hasPendingState = true;
        GameHUD.Instance?.SetCurrentTurnByCid(ownerCid);

        TryApplyCachedTurnState();
    }

    void OnEnable() => TryApplyCachedTurnState();

    private void TryApplyCachedTurnState()
    {
        if (!_hasPendingState) return;
        if (_applyCo != null) StopCoroutine(_applyCo);
        _applyCo = StartCoroutine(CoApplyWhenReady(_pendingPhase, _pendingOwnerCid));
    }

    private IEnumerator CoApplyWhenReady(TurnPhase phase, int ownerCid)
    {
        // wait up to ~2s for the local-owned pawn to exist
        float t = 2f;
        while (t > 0f && (myPawn == null || !myPawn || !myPawn.IsOwner))
        {
            foreach (var p in GameObject.FindObjectsOfType<PlayerPawn>())
                if (p != null && p.IsOwner) { myPawn = p; break; }

            if (myPawn != null && myPawn.IsOwner) break;
            t -= Time.deltaTime;
            yield return null;
        }

        GameHUD.Instance?.SetCurrentTurnByCid(ownerCid);

        bool isMyTurn = (myPawn != null && myPawn.Owner != null &&
                        ownerCid >= 0 && myPawn.Owner.ClientId == ownerCid);

        // default: lock buttons
        SetRollInteractable(false);
        SetEndTurnInteractable(false);

        switch (phase)
        {
            case TurnPhase.Rolling:
                SetRollInteractable(isMyTurn);
                break;
                // other phases keep buttons disabled
        }

        _applyCo = null;
    }
    
    public void SetPhaseTimer(TurnPhase phase, int seconds, int ownerCid)
    {
        if (phaseLabelText) phaseLabelText.text = PhaseToShortText(phase);

        if (phaseTimerText)
        {
            phaseTimerText.text  = Mathf.Max(0, seconds).ToString() + "s";
        }

        // You can also dim buttons here if needed; we already handle per-phase interactability elsewhere.
    }

    private string PhaseToShortText(TurnPhase p) => p switch
    {
        TurnPhase.Review => "Review",
        TurnPhase.Rolling => "Rolling",
        TurnPhase.TileEventPending => "Event",
        TurnPhase.Proposal => "Proposal",
        TurnPhase.EndReady => "Ending",
        _ => "—"
    };
    

}
