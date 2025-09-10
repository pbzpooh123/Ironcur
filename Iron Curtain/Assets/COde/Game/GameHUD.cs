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

    public void CreatePlayerPanel(int slotIndex, string name, string business, string country, float profit)
    {
        if (playerPanelPrefab == null)
        {
            Debug.LogError("❌ Player Panel Prefab is not assigned!");
            return;
        }

        if (slotIndex < 0 || slotIndex >= anchors.Length)
        {
            Debug.LogError($"❌ Slot {slotIndex} is invalid!");
            return;
        }

        // Spawn panel at the correct corner
        GameObject panel = Instantiate(playerPanelPrefab, anchors[slotIndex]);
        panel.name = $"PlayerPanel_{slotIndex}";

        TMP_Text[] texts = panel.GetComponentsInChildren<TMP_Text>();
        if (texts.Length >= 4)
        {
            texts[0].text = name;
            texts[1].text = business;
            texts[2].text = country;
            texts[3].text = $"Profit: {profit:0}";
        }

        Debug.Log($"✅ Spawned Player Panel for {name} in slot {slotIndex}");
    }
    
}
