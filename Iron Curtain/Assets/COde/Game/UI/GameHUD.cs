using UnityEngine;
using System.Collections.Generic;

public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance;

    [Header("UI Setup")]
    public GameObject playerPanelPrefab;

    [Header("Corner Anchors")]
    public Transform topLeftAnchor;
    public Transform topRightAnchor;
    public Transform bottomLeftAnchor;
    public Transform bottomRightAnchor;

    private Transform[] anchors;

    private PlayerInfoPanel _currentTurnPanel;

    // name → panel (case-insensitive)
    private readonly Dictionary<string, PlayerInfoPanel> _panelsByName = new();
    // cid  → panel
    private readonly Dictionary<int, PlayerInfoPanel> _panelsByCid = new();

    private void Awake()
    {
        Instance = this;
        anchors = new Transform[4] { topLeftAnchor, topRightAnchor, bottomLeftAnchor, bottomRightAnchor };
    }

    /// <summary>
    /// Creates a player panel at a corner slot. ownerCid is optional (-1 if unknown).
    /// </summary>
    public PlayerInfoPanel CreatePlayerPanel(int slotIndex, string name, int money, int ownerCid = -1)
    {
        if (playerPanelPrefab == null)
        {
            Debug.LogError("Player Panel Prefab is not assigned!");
            return null;
        }

        if (anchors == null || slotIndex < 0 || slotIndex >= anchors.Length || anchors[slotIndex] == null)
        {
            Debug.LogError($"[GameHUD] Slot {slotIndex} is invalid or anchor missing!");
            return null;
        }

        GameObject panelObj = Instantiate(playerPanelPrefab, anchors[slotIndex]);
        panelObj.name = $"PlayerPanel_{slotIndex}";

        var panel = panelObj.GetComponent<PlayerInfoPanel>();
        if (panel != null)
        {
            panel.SetInfo(name, money);
            panel.SetOwnerCid(ownerCid);
        }

        RegisterPanel(panel, name, ownerCid);

        Debug.Log($"[GameHUD] Spawned Player Panel for {name} (cid={ownerCid}) in slot {slotIndex}");
        return panel;
    }

    public PlayerInfoPanel FindPanelByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;
        _panelsByName.TryGetValue(Norm(name), out var panel);
        return panel;
    }

    public PlayerInfoPanel FindPanelByCid(int cid)
    {
        if (cid < 0) return null;
        _panelsByCid.TryGetValue(cid, out var panel);
        return panel;
    }

    public void UpdatePanelName(PlayerInfoPanel panel, string oldName, string newName)
    {
        if (panel == null) return;

        var oldKey = Norm(oldName);
        if (!string.IsNullOrWhiteSpace(oldKey) &&
            _panelsByName.TryGetValue(oldKey, out var existing) && existing == panel)
            _panelsByName.Remove(oldKey);

        var newKey = Norm(newName);
        if (!string.IsNullOrWhiteSpace(newKey))
            _panelsByName[newKey] = panel;

        if (panel.name != null)
        {
            panel.SetInfo(newName, 0); // keep money unchanged via UpdateMoney calls elsewhere
        }
    }

    private void RegisterPanel(PlayerInfoPanel panel, string name, int cid)
    {
        if (panel == null) return;

        var key = Norm(name);
        if (!string.IsNullOrWhiteSpace(key))
            _panelsByName[key] = panel;

        if (cid >= 0)
            _panelsByCid[cid] = panel;
    }

    /// <summary>Mark the current turn by player name (for older flows).</summary>
    public void SetCurrentTurn(string playerName)
    {
        // clear old
        if (_currentTurnPanel) _currentTurnPanel.SetTurnActive(false);

        var panel = FindPanelByName(playerName);
        if (panel)
        {
            panel.SetTurnActive(true);
            _currentTurnPanel = panel;
        }
        else
        {
            _currentTurnPanel = null;
        }
    }

    /// <summary>Mark the current turn by owner connection id (recommended).</summary>
    public void SetCurrentTurnByCid(int ownerCid)
    {
        // turn off any previous highlight
        if (_currentTurnPanel) _currentTurnPanel.SetTurnActive(false);
        _currentTurnPanel = null;

        if (_panelsByCid.TryGetValue(ownerCid, out var panel) && panel != null)
        {
            panel.SetTurnActive(true);
            _currentTurnPanel = panel;
        }
    }

    /// <summary>Call when a panel’s OwnerCid changes to keep the cid→panel map in sync.</summary>
    public void NotifyPanelCidChanged(PlayerInfoPanel panel, int newCid)
    {
        if (panel == null) return;
        if (newCid >= 0) _panelsByCid[newCid] = panel;
    }

    private static string Norm(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToLowerInvariant();
}
