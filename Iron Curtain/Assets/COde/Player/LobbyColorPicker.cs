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

    public LobbyCharacterAnim characterAnim;

    private static LobbyColorBinder[] _all;
    private static readonly Dictionary<int, int> _cidToSlot = new();

    // Which character UI anim is currently "ours" (optional)
    private static LobbyCharacterAnim _currentSelected;

    // Per-instance: last owner cid of this slot (for change detection)
    private int _lastOwnerCid = -1;

    private void Awake()
    {
        if (swatch != null)
        {
            if (characterSprite != null)
            {
                swatch.sprite = characterSprite;
                swatch.color = Color.white;
            }
            else if (slotIndex >= 0 && slotIndex < PlayerColors.Palette.Length)
            {
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
        while (InstanceFinder.ClientManager == null || !InstanceFinder.ClientManager.Started)
            yield return null;

        while (ColorLockManager.Instance == null || !ColorLockManager.Instance.IsSpawned)
            yield return null;

        ColorLockManager.Instance.CmdRequestSnapshot();
    }

    private void OnPick()
    {
        if (ColorLockManager.Instance == null || !ColorLockManager.Instance.IsSpawned)
            return;

        string name = PlayerPrefs.GetString(
            "PlayerName",
            $"P{InstanceFinder.ClientManager.Connection.ClientId}"
        );

        // Only send request to server; DO NOT play anim here
        ColorLockManager.Instance.CmdPick(slotIndex, name);
    }

    /* ---------------- Static helpers used by ColorLockManager ---------------- */

    public static bool TryGetSlotForCid(int cid, out int slot) => _cidToSlot.TryGetValue(cid, out slot);

    public static void ApplyOwners(int[] ownerCids)
    {
        _cidToSlot.Clear();
        for (int i = 0; i < ownerCids.Length; i++)
            if (ownerCids[i] >= 0) _cidToSlot[ownerCids[i]] = i;

        if (_all == null) _all = FindObjectsOfType<LobbyColorBinder>(true);

        foreach (var b in _all)
        {
            if (b == null) continue;

            int newCid = -1;
            if (b.slotIndex >= 0 && b.slotIndex < ownerCids.Length)
                newCid = ownerCids[b.slotIndex];

            bool wasTaken = b._lastOwnerCid >= 0;
            bool isTaken  = newCid >= 0;

            // Button interactability
            if (b.pickButton != null)
                b.pickButton.interactable = !isTaken;

            // --- NEW: drive animations from network state ---
            if (!wasTaken && isTaken)
            {
                // Slot became taken -> select animation
                b.characterAnim?.PlaySelectAnim();

                // If this slot belongs to THIS client, track as current selected
                if (InstanceFinder.ClientManager != null &&
                    InstanceFinder.ClientManager.Connection != null &&
                    newCid == InstanceFinder.ClientManager.Connection.ClientId &&
                    b.characterAnim != null)
                {
                    _currentSelected = b.characterAnim;
                }
            }
            else if (wasTaken && !isTaken)
            {
                // Slot became free -> deselect animation
                b.characterAnim?.PlayDeselectAnim();

                if (_currentSelected == b.characterAnim)
                    _currentSelected = null;
            }

            // Store for next diff
            b._lastOwnerCid = newCid;
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
