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

    [Space(6)]
    public TMP_Text nextMainEventText;        // event title/name (from EventManager)
    public TMP_Text nextMainEventHistory;     // short history/blurb (from EventManager)
    public TMP_Text nextMainEventEtaText;     // “Main event in X rounds” (from TurnManager)

    [Header("Next Main Event")]
    public GameObject nextEventContainer;     // existing
    public Button nextEventToggleButton;      // existing
    public RectTransform nextEventChevron;    // existing

    [Header("Next Event Slide")]
    public RectTransform nextEventPanelRect;

    [Tooltip("Anchored position when panel is fully visible.")]
    public Vector2 nextEventExpandedPos;

    [Tooltip("Anchored position when panel is hidden (off-screen or collapsed).")]
    public Vector2 nextEventCollapsedPos;

    [Tooltip("Duration of slide animation in seconds.")]
    public float nextEventSlideDuration = 0.25f;

    [Header("Help & Status")]
    public Button activeEffectsButton;


    private Coroutine _nextEventSlideCo;
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
        if (activeEffectsButton)
        activeEffectsButton.onClick.AddListener(OnActiveEffectsClicked);

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

    private void OnActiveEffectsClicked()
    {
        var pawn = GetOrFindLocalPawn();
        ActiveEffectsUI.Instance?.ShowForPawn(pawn);
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
            nextMainEventEtaText.text = "เหตุการณ์หลัก: เกิดในรอบนี้";
        }
        else if (rounds == 1)
        {
            nextMainEventEtaText.text = "เหตุการณ์หลักจะเกิดในอีก 1 รอบ";
        }
        else
        {
            nextMainEventEtaText.text = $"เหตุการณ์หลักจะเกิดในอีก {rounds} รอบ";
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
        if (nextEventContainer && !nextEventContainer.activeSelf)
            nextEventContainer.SetActive(true);

        if (nextEventChevron)
        {
            float z = _nextEventCollapsed ? 0f : 0f;
            nextEventChevron.localEulerAngles = new Vector3(0f, 0f, z);
        }

        if (nextEventPanelRect)
        {
            if (_nextEventSlideCo != null)
                StopCoroutine(_nextEventSlideCo);

            _nextEventSlideCo = StartCoroutine(SlideNextEventPanel(_nextEventCollapsed));
        }
        else
        {
            if (nextEventContainer)
                nextEventContainer.SetActive(!_nextEventCollapsed);
        }
    }

    private IEnumerator SlideNextEventPanel(bool collapse)
    {
        if (nextEventPanelRect == null) yield break;

        Vector2 start = nextEventPanelRect.anchoredPosition;
        Vector2 target = collapse ? nextEventCollapsedPos : nextEventExpandedPos;

        float t = 0f;
        float duration = Mathf.Max(0.01f, nextEventSlideDuration);

        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / duration);
            k = k * k * (3f - 2f * k);
            nextEventPanelRect.anchoredPosition = Vector2.Lerp(start, target, k);
            yield return null;
        }

        nextEventPanelRect.anchoredPosition = target;

        _nextEventSlideCo = null;
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

        // NEW: show “what to do now” text
        SetPhaseHint(phase, isMyTurn);

        _applyCo = null;
    }

    public void SetPhaseTimer(TurnPhase phase, int seconds, int ownerCid)
    {
        if (phaseLabelText) phaseLabelText.text = PhaseToShortText(phase);

        if (phaseTimerText)
        {
            phaseTimerText.text  = Mathf.Max(0, seconds).ToString() + "s";
        }
    }

    private string PhaseToShortText(TurnPhase p) => p switch
    {
        TurnPhase.Review          => "Review",
        TurnPhase.Rolling         => "Rolling",
        TurnPhase.TileEventPending=> "Event",
        TurnPhase.Proposal        => "Proposal",
        TurnPhase.EndReady        => "Ending",
        _                         => "—"
    };

    // NEW: phase → Thai instruction text
    public void SetPhaseHint(TurnPhase phase, bool isMyTurn)
    {
        if (!messageTMP) return;

        if (!isMyTurn)
        {
            messageTMP.text = "รอให้ผู้เล่นคนอื่นจบเทิร์นของเขา...";
            return;
        }

        switch (phase)
        {
            case TurnPhase.Review:
                messageTMP.text = "ตรวจข้อเสนอซื้อหุ้นในบริษัทของคุณ แล้วเลือกว่าจะรับหรือไม่ จากนั้นกด 'ปิด'";
                break;

            case TurnPhase.Rolling:
                messageTMP.text = "กดปุ่ม 'ทอยลูกเต๋า' เพื่อเดินตัวหมากของคุณ";
                break;

            case TurnPhase.TileEventPending:
                messageTMP.text = "อ่านผลเหตุการณ์ให้จบ แล้วกดปุ่มตามที่เกมบอก 'ทอยเต๋า/พร้อม' จากนั้นกด 'ปิด'";
                break;

            case TurnPhase.Proposal:
                messageTMP.text = "เปิดหน้าต่างตลาด ส่งข้อเสนอ/บังคับซื้อ จากนั้นกด 'ปิด'";
                break;

            default:
                messageTMP.text = "";
                break;
        }
    }
}
