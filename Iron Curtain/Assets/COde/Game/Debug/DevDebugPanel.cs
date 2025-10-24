using UnityEngine;
using FishNet;
using FishNet.Object;

public class DevDebugPanel : MonoBehaviour
{
    [Header("Visibility")]
    public bool startVisible = false;
    private bool _visible;

    [Header("Defaults")]
    public string sector = "Defense";
    [Tooltip("Affects buy cost & immediate price bump. 1.0 = none, 1.5 = +50%")]
    public float priceMult = 1.5f;
    [Tooltip("Company-level payout multiplier. 1.0 = none, 1.25 = +25%")]
    public float payoutMult = 1.25f;
    [Tooltip("Rounds until expiry")]
    public int durationRounds = 3;

    [Header("Money Helpers")]
    public int giveCashEach = 1000;
    public float boostPriceDeltaPct = 20f;
    public float boostPayoutMult = 1.5f;
    public int boostDuration = 2;
    public float nerfPriceDeltaPct = -20f;
    public float nerfPayoutMult = 0.5f;
    public int nerfDuration = 2;

    private GUIStyle _title, _btn, _lbl, _field;

    void Awake()
    {
        _visible = startVisible;
        BuildStyles();
    }

    void Update()
    {
        // Toggle panel
        if (Input.GetKeyDown(KeyCode.Keypad4))
            _visible = !_visible;

        // Quick hotkeys (host-only)
        if (!IsServerLike()) return;

        if (Input.GetKeyDown(KeyCode.F2)) TriggerMainEvent();
        if (Input.GetKeyDown(KeyCode.F3)) PruneSurges();
        if (Input.GetKeyDown(KeyCode.F4)) AdvanceRoundOne();
        if (Input.GetKeyDown(KeyCode.F5)) ActivateSurge(sector, priceMult, payoutMult, durationRounds);
    }

    bool IsServerLike()
    {
        // Host or dedicated server:
        return InstanceFinder.IsServer;
    }

    void OnGUI()
    {
        if (!_visible) return;

        const int w = 360;
        const int pad = 12;
        Rect r = new Rect(12, 12, w, 0);

        GUILayout.BeginArea(new Rect(r.x, r.y, w, Screen.height));
        GUILayout.Label("DEV DEBUG PANEL", _title);

        if (!IsServerLike())
        {
            GUILayout.Label("Host/Server only. Run as Host to use.", _lbl);
            GUILayout.EndArea();
            return;
        }

        GUILayout.Space(6);
        GUILayout.Label("Hotkeys: ` to toggle • F2 MainEvent • F3 Prune • F4 Advance Round • F5 Sample Surge", _lbl);

        GUILayout.Space(10);
        GUILayout.Label("Sector Surge", _lbl);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Sector", _lbl, GUILayout.Width(60));
        sector = GUILayout.TextField(sector, _field);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Price x", _lbl, GUILayout.Width(60));
        priceMult = SafeFloatField(priceMult);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Payout x", _lbl, GUILayout.Width(60));
        payoutMult = SafeFloatField(payoutMult);
        GUILayout.EndHorizontal();

        GUILayout.BeginHorizontal();
        GUILayout.Label("Dur (r)", _lbl, GUILayout.Width(60));
        durationRounds = SafeIntField(durationRounds);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Activate Sector Surge", _btn))
            ActivateSurge(sector, priceMult, payoutMult, durationRounds);

        if (GUILayout.Button("Prune Surges Now", _btn))
            PruneSurges();

        GUILayout.Space(10);
        GUILayout.Label("Events & Rounds", _lbl);
        if (GUILayout.Button("Trigger Main Event", _btn))
            TriggerMainEvent();
        if (GUILayout.Button("Advance Round + Market Step + Prune", _btn))
            AdvanceRoundOne();

        GUILayout.Space(10);
        GUILayout.Label("Cash / Ownership Boosters", _lbl);
        GUILayout.BeginHorizontal();
        GUILayout.Label("Give Each", _lbl, GUILayout.Width(80));
        giveCashEach = SafeIntField(giveCashEach);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Give Cash to All Players", _btn))
            GiveCashToAll(giveCashEach);

        GUILayout.Space(4);
        GUILayout.Label("Boost Owned Companies (Local Pawn)", _lbl);
        GUILayout.BeginHorizontal();
        GUILayout.Label("ΔPrice %", _lbl, GUILayout.Width(80));
        boostPriceDeltaPct = SafeFloatField(boostPriceDeltaPct);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Payout x", _lbl, GUILayout.Width(80));
        boostPayoutMult = SafeFloatField(boostPayoutMult);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Dur (r)", _lbl, GUILayout.Width(80));
        boostDuration = SafeIntField(boostDuration);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Boost Mine", _btn))
            BoostMine(boostPriceDeltaPct, boostPayoutMult, boostDuration);

        GUILayout.Space(4);
        GUILayout.Label("Nerf Owned Companies (Local Pawn)", _lbl);
        GUILayout.BeginHorizontal();
        GUILayout.Label("ΔPrice %", _lbl, GUILayout.Width(80));
        nerfPriceDeltaPct = SafeFloatField(nerfPriceDeltaPct);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Payout x", _lbl, GUILayout.Width(80));
        nerfPayoutMult = SafeFloatField(nerfPayoutMult);
        GUILayout.EndHorizontal();
        GUILayout.BeginHorizontal();
        GUILayout.Label("Dur (r)", _lbl, GUILayout.Width(80));
        nerfDuration = SafeIntField(nerfDuration);
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Nerf Mine", _btn))
            NerfMine(nerfPriceDeltaPct, nerfPayoutMult, nerfDuration);

        GUILayout.EndArea();
    }

    /* ====== Buttons ====== */

    void ActivateSurge(string sec, float pMult, float payMult, int dur)
    {
        if (EventManager.Instance == null) return;
        EventManager.Instance.ActivateSectorSurge(sec, pMult, payMult, dur);
        Debug.Log($"[DEV] Surge: sector={sec}, price x{pMult}, payout x{payMult}, dur={dur}");
    }

    void PruneSurges()
    {
        if (EventManager.Instance == null) return;
        EventManager.Instance.ServerPruneSectorSurges();
        Debug.Log("[DEV] Pruned sector surges.");
    }

    void TriggerMainEvent()
    {
        if (EventManager.Instance == null || TurnManager.Instance == null) return;
        var round = Mathf.Max(1, TurnManager.Instance.roundCount.Value);
        EventManager.Instance.TriggerMainEvent(round);
        Debug.Log("[DEV] Triggered main event.");
    }

    void AdvanceRoundOne()
    {
        var tm = TurnManager.Instance;
        if (tm == null) return;

        tm.roundCount.Value++;
        int r = tm.roundCount.Value;

        // keep your current game flow in sync
        MarketManager.Instance?.OnRoundAdvanced(r);
        EventManager.Instance?.ServerPruneSectorSurges();
        Debug.Log($"[DEV] Advanced to round {r} (market advanced, surges pruned).");
    }

    void GiveCashToAll(int amount)
    {
        if (GameManager.Instance == null) return;
        foreach (var p in GameManager.Instance.Players)
            p?.AddMoney(Mathf.Max(0, amount));
        Debug.Log($"[DEV] Gave ${amount} to all players.");
    }

    void BoostMine(float deltaPct, float payout, int dur)
    {
        var me = FindLocalOwnedPawn();
        if (me == null) { Debug.LogWarning("[DEV] No local pawn."); return; }
        MarketManager.Instance?.ServerBoostAllCompaniesOwnedBy(me, deltaPct, payout, dur);
        Debug.Log($"[DEV] Boosted mine: Δ%={deltaPct}, x{payout}, dur={dur}");
    }

    void NerfMine(float deltaPct, float payout, int dur)
    {
        var me = FindLocalOwnedPawn();
        if (me == null) { Debug.LogWarning("[DEV] No local pawn."); return; }
        MarketManager.Instance?.ServerNerfAllCompaniesOwnedBy(me, deltaPct, payout, dur);
        Debug.Log($"[DEV] Nerfed mine: Δ%={deltaPct}, x{payout}, dur={dur}");
    }

    PlayerPawn FindLocalOwnedPawn()
    {
        var pawns = GameObject.FindObjectsOfType<PlayerPawn>();
        foreach (var p in pawns) if (p != null && p.IsOwner) return p;
        return null;
    }

    /* ====== UI helpers ====== */

    float SafeFloatField(float v)
    {
        string s = GUILayout.TextField(v.ToString("0.###"), _field, GUILayout.Width(100));
        return float.TryParse(s, out var f) ? f : v;
    }

    int SafeIntField(int v)
    {
        string s = GUILayout.TextField(v.ToString(), _field, GUILayout.Width(100));
        return int.TryParse(s, out var i) ? i : v;
    }

    void BuildStyles()
    {
        _title = new GUIStyle(GUI.skin.label) { fontSize = 16, fontStyle = FontStyle.Bold };
        _lbl   = new GUIStyle(GUI.skin.label) { fontSize = 12, wordWrap = true };
        _btn   = new GUIStyle(GUI.skin.button) { fontSize = 12, padding = new RectOffset(8,8,6,6) };
        _field = new GUIStyle(GUI.skin.textField) { fontSize = 12 };
    }
}
