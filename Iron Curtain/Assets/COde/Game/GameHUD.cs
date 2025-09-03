using UnityEngine;
using TMPro;

public class GameHUD : MonoBehaviour
{
    public static GameHUD Instance;

    [Header("Anchors")]
    public Transform topLeftAnchor;
    public Transform topRightAnchor;
    public Transform bottomLeftAnchor;
    public Transform bottomRightAnchor;

    [Header("Prefab")]
    public GameObject playerPanelPrefab;

    private PlayerInfoPanel[] slots;

    private void Awake()
    {
        if (Instance == null) Instance = this;

        slots = new PlayerInfoPanel[4];
    }

    public void SetPlayerInfo(int slotIndex, string name, string business, string country, float profit = 0f)
    {
        if (slotIndex < 0 || slotIndex >= 4) return;

        if (slots[slotIndex] == null)
        {
            Transform parent = slotIndex switch
            {
                0 => topLeftAnchor,
                1 => topRightAnchor,
                2 => bottomLeftAnchor,
                3 => bottomRightAnchor,
                _ => topLeftAnchor
            };

            GameObject panelObj = Instantiate(playerPanelPrefab, parent);
            slots[slotIndex] = panelObj.GetComponent<PlayerInfoPanel>();
        }

        slots[slotIndex].SetInfo(name, business, country, profit);
    }

    public void UpdateProfit(int slotIndex, float profit)
    {
        if (slotIndex < 0 || slotIndex >= 4 || slots[slotIndex] == null) return;
        slots[slotIndex].UpdateProfit(profit);
    }
}