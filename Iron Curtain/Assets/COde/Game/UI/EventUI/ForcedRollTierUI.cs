using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ForcedRollTierUI : MonoBehaviour
{
    public enum RollMode { Tier, Media }     // << renamed: no Competition
    public static RollMode CurrentRollMode = RollMode.Tier;

    public static ForcedRollTierUI Instance;

    [Header("Root")]
    public GameObject panel;
    public TMP_Text headerText;

    [Header("Grid")]
    public Transform gridParent;      // parent for SlotWidget prefab
    public GameObject slotPrefab;     // prefab containing SlotWidget

    [Header("Footer")]
    public TMP_Text footerText;       // summary / progress
    public Button closeButton;        // single Close button (always visible)

    // runtime
    private readonly Dictionary<int, SlotWidget> _cidToWidget = new();
    private int _localCid = -1;
    private bool _localCloseSent = false;

    private void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);
        if (closeButton)
        {
            closeButton.gameObject.SetActive(true);  // always visible in layout
            closeButton.interactable = false;        // becomes interactable when server enables
        }
        if (footerText) footerText.text = "";
    }

    /// <summary>Builds the grid and wires up local Roll buttons.</summary>
    public void Show(string header, List<int> clientIds, Dictionary<int,string> names, int localCid)
    {
        _cidToWidget.Clear();
        _localCid = localCid;
        _localCloseSent = false;

        // Clear grid
        for (int i = gridParent.childCount - 1; i >= 0; i--)
            Destroy(gridParent.GetChild(i).gameObject);

        if (headerText) headerText.text = header;

        foreach (int cid in clientIds)
        {
            var go = Instantiate(slotPrefab, gridParent);
            var w = go.GetComponent<SlotWidget>() ?? go.AddComponent<SlotWidget>();

            string display = (names != null && names.TryGetValue(cid, out var nm)) ? nm : ("P" + cid);
            w.Bind(cid, display);

            bool isLocal = (cid == _localCid);
            w.SetButtonEnabled(isLocal, () =>
            {
                // guard double-click immediately
                w.SetButtonEnabled(false, null);

                // Ask server to roll (authoritative)
                if (CurrentRollMode == RollMode.Media)
                    EventManager.Instance.CmdRequestMediaRoll();
                else
                    EventManager.Instance.CmdRequestTierRoll();
            });

            _cidToWidget[cid] = w;
        }

        SetFooter("");
        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.interactable = false;              // will be enabled by server
        }

        if (panel) panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel) panel.SetActive(false);
    }

    // ---- server → client updates ----

    public void SetRolling(int cid)
    {
        if (_cidToWidget.TryGetValue(cid, out var w))
            w.SetRolling();
    }

    public void SetRolled(int cid, int value, string outcome)
    {
        if (_cidToWidget.TryGetValue(cid, out var w))
            w.SetRolled(value, outcome);
    }

    public void EnableCloseForLocal()
    {
        if (!closeButton) return;

        closeButton.interactable = !_localCloseSent;

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() =>
        {
            if (_localCloseSent) return;
            _localCloseSent = true;

            closeButton.interactable = false;

            if (CurrentRollMode == RollMode.Media)
                EventManager.Instance.CmdMediaClientClosed();
            else
                EventManager.Instance.CmdTierClientClosed();
        });
    }

    public void SetFooter(string text)
    {
        if (footerText) footerText.text = text ?? "";
    }

}
