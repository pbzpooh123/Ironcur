using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ForcedRollTierUI : MonoBehaviour
{
    public static ForcedRollTierUI Instance;

    [Header("Root")]
    public GameObject panel;
    public TMP_Text headerText;

    [Header("Grid")]
    public Transform gridParent;      // where Slot prefabs go
    public GameObject slotPrefab;

    [Header("Footer")]
    public TMP_Text footerText;       // shows “x/y players closed…”
    public Button closeButton;        // local Close button
    public TMP_Text closeButtonLabel; // optional label text on the button

    // runtime
    private readonly Dictionary<int, SlotWidget> _cidToWidget = new();
    private int _localCid = -1;
    private bool _localCloseSent = false;

    private void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);
        if (closeButton) closeButton.gameObject.SetActive(false);
        if (footerText) footerText.text = "";
    }

    public void Show(string header, List<int> clientIds, Dictionary<int,string> names, int localCid)
    {
        _cidToWidget.Clear();
        _localCid = localCid;
        _localCloseSent = false;

        // Clear old
        for (int i = gridParent.childCount-1; i >= 0; i--)
            Destroy(gridParent.GetChild(i).gameObject);

        if (headerText) headerText.text = header;

        foreach (int cid in clientIds)
        {
            var go = Instantiate(slotPrefab, gridParent);
            var w = go.GetComponent<SlotWidget>();
            if (w == null) w = go.AddComponent<SlotWidget>(); // safety
            w.Bind(cid, names != null && names.TryGetValue(cid, out var nm) ? nm : ("P"+cid));

            bool isLocal = (cid == _localCid);
            w.SetButtonEnabled(isLocal, () =>
            {
                // local pressed Roll → disable immediately to guard double-click
                w.SetButtonEnabled(false, null);
                // Ask server to roll on our behalf (authoritative)
                EventManager.Instance.CmdRequestTierRoll(); 
            });

            _cidToWidget[cid] = w;
        }

        if (footerText) footerText.text = "";
        if (closeButton)
        {
            closeButton.onClick.RemoveAllListeners();
            closeButton.gameObject.SetActive(false); // only shown after server enables
            closeButton.interactable = false;
        }
        if (closeButtonLabel) closeButtonLabel.text = "Close";

        if (panel) panel.SetActive(true);
    }

    public void Hide()
    {
        if (panel) panel.SetActive(false);
    }

    /// server → client updates
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

    /// Called by EventManager.TargetTierEnableClose() once all rolls are in.
    public void EnableCloseForLocal()
    {
        if (!closeButton) return;

        // Only the local player should be able to press their Close button.
        closeButton.gameObject.SetActive(true);
        closeButton.interactable = !_localCloseSent;
        if (closeButtonLabel) closeButtonLabel.text = _localCloseSent ? "Waiting…" : "Close";

        closeButton.onClick.RemoveAllListeners();
        closeButton.onClick.AddListener(() =>
        {
            if (_localCloseSent) return;
            _localCloseSent = true;

            // Immediately disable to avoid double click spam
            closeButton.interactable = false;
            if (closeButtonLabel) closeButtonLabel.text = "Waiting…";

            // Tell server we closed
            EventManager.Instance.CmdTierClientClosed();
        });
    }

    /// Optional: EventManager can broadcast progress (have/total)
    public void UpdateCloseStatus(int have, int total)
    {
        if (footerText) footerText.text = $"Close status: {have}/{total} players closed.";
    }
}
