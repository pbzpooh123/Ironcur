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
    [SerializeField] private List<PlayerInfoPanel> _debugPanels = new();

    // GameHUD.cs (fields)
    private int _pendingHighlightCid = -1;


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

        var infoPanel = panelObj.GetComponent<PlayerInfoPanel>();
        if (infoPanel != null)
        {
            infoPanel.SetInfo(name, money);
            infoPanel.SetOwnerCid(ownerCid); // this calls NotifyPanelCidChanged
        }
        RegisterPanel(infoPanel, name, ownerCid);
        return infoPanel;
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
        if (!panel) return;
        if (cid >= 0) _panelsByCid[cid] = panel;
        if (!_debugPanels.Contains(panel)) _debugPanels.Add(panel);
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
    public void NotifyPanelCidChanged(PlayerInfoPanel panel, int newCid)
    {
        if (panel == null) return;

        // Remove old keys for this panel
        foreach (var kv in new List<KeyValuePair<int, PlayerInfoPanel>>(_panelsByCid))
            if (kv.Value == panel) _panelsByCid.Remove(kv.Key);

        if (newCid >= 0) _panelsByCid[newCid] = panel;
        Debug.Log($"[GameHUD] CID map updated: {panel.OwnerName} -> cid={newCid}");

        // If a highlight was requested earlier for this cid, apply it now
        if (_pendingHighlightCid == newCid)
            SetCurrentTurnByCid(_pendingHighlightCid);
    }

    public void SetCurrentTurnByCid(int ownerCid)
    {
        _pendingHighlightCid = ownerCid;

        if (_currentTurnPanel != null)
            _currentTurnPanel.SetTurnActive(false);

        var panel = FindPanelByCid(ownerCid);
        if (panel == null)
        {
            Debug.LogWarning($"[GameHUD] SetCurrentTurnByCid({ownerCid}) but no panel found. Known CIDs: [{string.Join(",", _panelsByCid.Keys)}]");
            _currentTurnPanel = null;
            return;
        }

        panel.SetTurnActive(true);
        _currentTurnPanel = panel;
        Debug.Log($"[GameHUD] SetCurrentTurnByCid({ownerCid}) -> '{panel.OwnerName}', spriteNull={(panel.highlightImage==null||panel.highlightImage.sprite==null)}");
    }


    private static string Norm(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToLowerInvariant();
}
