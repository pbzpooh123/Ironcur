using UnityEngine;
using TMPro;
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
    /// Creates a player panel at a corner slot. ownerCid is optional; pass it if known.
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

        GameObject panel = Instantiate(playerPanelPrefab, anchors[slotIndex]);
        panel.name = $"PlayerPanel_{slotIndex}";

        // Ensure there is a CID tag on the panel for fallback binding
        var tag = panel.GetComponent<PlayerCidTag>();
        if (tag == null) tag = panel.AddComponent<PlayerCidTag>();
        tag.clientId = ownerCid;

        var infoPanel = panel.GetComponent<PlayerInfoPanel>();
        if (infoPanel != null)
        {
            infoPanel.SetInfo(name, money);
            infoPanel.SetOwnerCid(ownerCid);   // ← important for PortfolioButtonBinder
        }

        RegisterPanel(infoPanel, name, ownerCid);

        Debug.Log($"[GameHUD] Spawned Player Panel for {name} (cid={ownerCid}) in slot {slotIndex}");
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

        if (panel.nameText != null)
        {
            panel.ownerName = newName;
            panel.nameText.text = newName;
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
        else
        {
            // Try reading tag if cid wasn’t passed
            var t = panel.GetComponent<PlayerCidTag>();
            if (t != null && t.clientId >= 0)
                _panelsByCid[t.clientId] = panel;
        }
    }

    private static string Norm(string s) => string.IsNullOrWhiteSpace(s) ? "" : s.Trim().ToLowerInvariant();
}

/// <summary>
/// Tiny tag so a panel can be looked up by connection id (CID).
/// </summary>
public class PlayerCidTag : MonoBehaviour
{
    public int clientId = -1;
}
