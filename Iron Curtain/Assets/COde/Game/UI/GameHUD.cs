using UnityEngine;
using TMPro;

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

    private void Awake()
    {
        Instance = this;
        anchors = new Transform[4] { topLeftAnchor, topRightAnchor, bottomLeftAnchor, bottomRightAnchor };
    }

    public PlayerInfoPanel CreatePlayerPanel(int slotIndex, string name, int profit)
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

        // Spawn panel at the correct corner
        GameObject panel = Instantiate(playerPanelPrefab, anchors[slotIndex]);
        panel.name = $"PlayerPanel_{slotIndex}";

        PlayerInfoPanel infoPanel = panel.GetComponent<PlayerInfoPanel>();
        if (infoPanel != null)
            infoPanel.SetInfo(name, profit);

        Debug.Log($"Spawned Player Panel for {name} in slot {slotIndex}");
        return infoPanel;
    }

    
    
}
