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
    public Transform gridParent;      // where Slot prefabs go
    public GameObject slotPrefab;

    // runtime
    private readonly Dictionary<int, SlotWidget> _cidToWidget = new();
    private int _localCid = -1;

    private void Awake()
    {
        Instance = this;
        if (panel) panel.SetActive(false);
    }

    public void Show(string header, List<int> clientIds, Dictionary<int,string> names, int localCid)
    {
        _cidToWidget.Clear();
        _localCid = localCid;

        // Clear old
        for (int i = gridParent.childCount-1; i >= 0; i--)
            Destroy(gridParent.GetChild(i).gameObject);

        if (headerText) headerText.text = header;

        foreach (int cid in clientIds)
        {
            var go = Instantiate(slotPrefab, gridParent);
            var w = go.GetComponent<SlotWidget>();
            if (w == null) w = go.AddComponent<SlotWidget>(); // safety
            w.Bind(cid, names.TryGetValue(cid, out var nm) ? nm : ("P"+cid));

            bool isLocal = (cid == _localCid);
            w.SetButtonEnabled(isLocal, () =>
            {
                // local pressed
                // disable immediately to guard double-click
                w.SetButtonEnabled(false, null);
                // Ask server to roll on our behalf (authoritative)
                EventManager.Instance.CmdRequestTierRoll(); 
            });

            _cidToWidget[cid] = w;
        }

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

    public class SlotWidget : MonoBehaviour
    {
        public TMP_Text nameText;
        public TMP_Text resultText;
        public Image diceImage;
        public TMP_Text diceNumber;    // if you don’t use sprite numbers
        public Button rollButton;

        int _cid;

        public void Bind(int cid, string displayName)
        {
            _cid = cid;
            if (nameText) nameText.text = displayName;
            if (resultText) resultText.text = "Waiting…";
            SetDice(0);
        }

        public void SetButtonEnabled(bool enabled, System.Action onClick)
        {
            if (rollButton)
            {
                rollButton.gameObject.SetActive(enabled);
                rollButton.onClick.RemoveAllListeners();
                if (enabled && onClick != null) rollButton.onClick.AddListener(() => onClick());
            }
        }

        public void SetRolling()
        {
            if (resultText) resultText.text = "Rolling…";
        }

        public void SetRolled(int value, string outcome)
        {
            SetDice(value);
            if (resultText) resultText.text = $"Rolled {value}: {outcome}";
            SetButtonEnabled(false, null);
        }

        void SetDice(int value)
        {
            // Option A: show number
            if (diceNumber)
                diceNumber.text = (value <= 0 ? "-" : value.ToString());

            // Option B (optional): set sprite by value
            // if (diceImage) diceImage.sprite = DiceAtlas.Get(value);
        }
    }
}
