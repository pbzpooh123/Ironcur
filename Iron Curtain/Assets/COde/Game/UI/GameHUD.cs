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

    private Transform[] _anchors;

    // cache panels by slot to avoid duplicates
    private readonly Dictionary<int, PlayerInfoPanel> _panels = new();

    private void Awake()
    {
        Instance = this;
        _anchors = new Transform[4] { topLeftAnchor, topRightAnchor, bottomLeftAnchor, bottomRightAnchor };
    }



 

    public PlayerInfoPanel CreatePlayerPanel(int slotIndex, string name, string business, string country, int profit)
    {
        if (playerPanelPrefab == null)
        {
            Debug.LogError("Player Panel Prefab is not assigned!");
            return null;
        }
        if (slotIndex < 0 || slotIndex >= _anchors.Length)
        {
            Debug.LogError($"Slot {slotIndex} is invalid!");
            return null;
        }

        if (_panels.TryGetValue(slotIndex, out var existing))
        {
            return existing;
        }


        // Create new
        GameObject panelGO = Instantiate(playerPanelPrefab, _anchors[slotIndex]);
        panelGO.name = $"PlayerPanel_{slotIndex}";
        var infoPanel = panelGO.GetComponent<PlayerInfoPanel>();
        if (infoPanel == null)
        {
            Debug.LogError("Player Panel Prefab must have PlayerInfoPanel component.");
            return null;
        }
        
        
        _panels[slotIndex] = infoPanel;
        Debug.Log($"[HUD] Created panel for {name} in slot {slotIndex}");
        return infoPanel;
    }
    
    /// <summary>Remove a panel (e.g., player left).</summary>
    public void RemovePanel(int slotIndex)
    {
        if (_panels.TryGetValue(slotIndex, out var panel) && panel != null)
        {
            Destroy(panel.gameObject);
        }
        _panels.Remove(slotIndex);
    }
}
