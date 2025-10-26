// LobbyColorBinder.cs
using UnityEngine;
using UnityEngine.UI;
using FishNet;
using System.Collections.Generic;

public class LobbyColorBinder : MonoBehaviour
{
    [Header("Per button")]
    public int slotIndex;      // 0..Palette.Length-1
    public Image swatch;       // your button’s Image to show the color
    public Button pickButton;  // the clickable button

    private static LobbyColorBinder[] _all;

    // client-side lookup: connectionId -> color slot
    private static readonly Dictionary<int,int> _cidToSlot = new();
    public static bool TryGetSlotForCid(int cid, out int slot) => _cidToSlot.TryGetValue(cid, out slot);

    private void Awake()
    {
        if (swatch != null && slotIndex >= 0 && slotIndex < PlayerColors.Palette.Length)
            swatch.color = PlayerColors.Palette[slotIndex];

        if (pickButton != null)
        {
            pickButton.onClick.RemoveAllListeners();
            pickButton.onClick.AddListener(OnPick);
        }
    }

    private void OnEnable()
    {
        _all = FindObjectsOfType<LobbyColorBinder>(true);
        // ask server for a snapshot if you have a server authority script
        if (ColorLockManager.Instance != null && InstanceFinder.ClientManager.Started)
            ColorLockManager.Instance.CmdRequestSnapshot();
    }

    private void OnPick()
    {
        if (ColorLockManager.Instance == null) return;
        string name = PlayerPrefs.GetString("PlayerName", $"P{InstanceFinder.ClientManager.Connection.ClientId}");
        ColorLockManager.Instance.CmdPick(slotIndex, name);
    }

    /// <summary>
    /// Server should call this via an RPC when color ownership changes.
    /// ownerCids[slot] = connectionId or -1 if free.
    /// </summary>
    public static void ApplyOwners(int[] ownerCids)
    {
        // rebuild cid -> slot map
        _cidToSlot.Clear();
        for (int i = 0; i < ownerCids.Length; i++)
        {
            int cid = ownerCids[i];
            if (cid >= 0) _cidToSlot[cid] = i;
        }

        if (_all == null) _all = FindObjectsOfType<LobbyColorBinder>(true);

        // enable/disable buttons
        foreach (var b in _all)
        {
            bool taken = b.slotIndex >= 0 && b.slotIndex < ownerCids.Length && ownerCids[b.slotIndex] >= 0;
            if (b.pickButton != null) b.pickButton.interactable = !taken;
        }

        // recolor player names in the lobby list
        var lobby = Object.FindObjectOfType<LobbyUI>();
        lobby?.RefreshNameColors();
    }

    // convenience for LobbyUI
    public static bool TryGetColorForCid(int connId, out Color color)
    {
        color = Color.white;
        if (_cidToSlot.TryGetValue(connId, out int slot))
        {
            if (slot >= 0 && slot < PlayerColors.Palette.Length)
            {
                color = PlayerColors.Palette[slot];
                return true;
            }
        }
        return false;
    }
}