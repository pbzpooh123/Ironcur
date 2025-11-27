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

    public LobbyCharacterAnim characterAnim;
    private static LobbyCharacterAnim _currentSelected;



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
            $"P{FishNet.InstanceFinder.ClientManager.Connection.ClientId}"
        );
        ColorLockManager.Instance.CmdPick(slotIndex, name);

        if (characterAnim != null)
        {
            if (_currentSelected != null && _currentSelected != characterAnim)
                _currentSelected.PlayDeselectAnim();

            characterAnim.PlaySelectAnim();
            _currentSelected = characterAnim;
        }
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
