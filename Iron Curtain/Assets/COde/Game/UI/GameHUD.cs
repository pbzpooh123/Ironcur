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

    // NEW: map playerName -> panel for fast binding
    private readonly Dictionary<string, PlayerInfoPanel> _panelsByName = new();

    private void Awake()
    {
        Instance = this;
        anchors = new Transform[4] { topLeftAnchor, topRightAnchor, bottomLeftAnchor, bottomRightAnchor };
    }

    public PlayerInfoPanel CreatePlayerPanel(int slotIndex, string name, int money)
    {
        if (playerPanelPrefab == null)
        {
            Debug.LogError("Player Panel Prefab is not assigned!");
            return null;
        }

        if (slotIndex < 0 || slotIndex >= anchors.Length)
        {
            Debug.LogError($"Slot {slotIndex} is invalid!");
            return null;
        }

        GameObject panel = Instantiate(playerPanelPrefab, anchors[slotIndex]);
        panel.name = $"PlayerPanel_{slotIndex}";

        PlayerInfoPanel infoPanel = panel.GetComponent<PlayerInfoPanel>();
        if (infoPanel != null)
            infoPanel.SetInfo(name, money);

        // register mapping for later lookup
        _panelsByName[name] = infoPanel;

        Debug.Log($"Spawned Player Panel for {name} in slot {slotIndex}");
        return infoPanel;
    }

    /// <summary>
    /// Find the UI panel by player name.
    /// </summary>
    public PlayerInfoPanel FindPanelByName(string name)
    {
        _panelsByName.TryGetValue(name, out var panel);
        return panel;
    }
}