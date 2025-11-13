// LobbyColorBinder.cs  (keep MonoBehaviour)
using UnityEngine;
using UnityEngine.UI;
using FishNet;
using System.Collections;
using System.Collections.Generic;

public class LobbyColorBinder : MonoBehaviour
{
    [Header("Per button")]
    public int slotIndex;
    public Image swatch;
    public Button pickButton;

    [Header("Character preview")]
    public Sprite characterSprite;


    private static LobbyColorBinder[] _all;
    private static readonly Dictionary<int, int> _cidToSlot = new();
    


    private void Awake()
    {
        if (swatch != null)
        {
            if (characterSprite != null)
            {
                // Show per-character icon
                swatch.sprite = characterSprite;
                swatch.color = Color.white; // make sure it’s not tinted weirdly
            }
            else if (slotIndex >= 0 && slotIndex < PlayerColors.Palette.Length)
            {
                // Fallback: old color-square behavior
                swatch.color = PlayerColors.Palette[slotIndex];
            }
        }

        if (pickButton != null)
        {
            pickButton.onClick.RemoveAllListeners();
            pickButton.onClick.AddListener(OnPick);
        }
    }


    private void OnEnable() => StartCoroutine(WaitAndRequest());

    private IEnumerator WaitAndRequest()
    {
        // Wait for client to start.
        while (InstanceFinder.ClientManager == null || !InstanceFinder.ClientManager.Started)
            yield return null;

        // Wait for ColorLockManager to exist and be spawned before using its ServerRpc.
        while (ColorLockManager.Instance == null || !ColorLockManager.Instance.IsSpawned)
            yield return null;

        ColorLockManager.Instance.CmdRequestSnapshot();
    }

    private void OnPick()
    {
        if (ColorLockManager.Instance == null || !ColorLockManager.Instance.IsSpawned) return;
        string name = PlayerPrefs.GetString("PlayerName", $"P{InstanceFinder.ClientManager.Connection.ClientId}");
        ColorLockManager.Instance.CmdPick(slotIndex, name);
    }

    public static bool TryGetSlotForCid(int cid, out int slot) => _cidToSlot.TryGetValue(cid, out slot);

    public static void ApplyOwners(int[] ownerCids)
    {
        _cidToSlot.Clear();
        for (int i = 0; i < ownerCids.Length; i++)
            if (ownerCids[i] >= 0) _cidToSlot[ownerCids[i]] = i;

        if (_all == null) _all = FindObjectsOfType<LobbyColorBinder>(true);
        foreach (var b in _all)
        {
            bool taken = b.slotIndex >= 0 && b.slotIndex < ownerCids.Length && ownerCids[b.slotIndex] >= 0;
            if (b.pickButton) b.pickButton.interactable = !taken;
        }

        FindObjectOfType<LobbyUI>()?.RefreshNameColors();
    }

    public static bool TryGetColorForCid(int connId, out Color color)
    {
        color = Color.white;
        if (_cidToSlot.TryGetValue(connId, out int slot) &&
            slot >= 0 && slot < PlayerColors.Palette.Length)
        {
            color = PlayerColors.Palette[slot];
            return true;
        }
        return false;
    }
}
