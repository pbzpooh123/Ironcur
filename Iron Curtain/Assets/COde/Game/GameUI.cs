using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Object;
using FishNet;

public class GameUI : MonoBehaviour
{
    public static GameUI Instance;

    [Header("UI Elements")]
    public Image backgroundImage;
    public TMP_Text profitText;
    public TMP_Text countdownText;

    private NetworkLobbyPlayer localPlayer;

    private void Awake()
    {
        Instance = this;
        countdownText.gameObject.SetActive(false);
    }

    public void UpdateCountdown(float secondsLeft)
    {
        countdownText.text = $"⏱ {secondsLeft:F0}s";
        countdownText.gameObject.SetActive(true);
    }

    public void UpdateProfit(float amount)
    {
        profitText.text = $"💰 Profit: ${amount:N0}";
    }

    public void ShowWinner(string winnerName)
    {
        countdownText.text = $"🏆 Winner: {winnerName}";
        countdownText.gameObject.SetActive(true);
    }

    private void Start()
    {
        // Optional: Automatically find and assign local player for use later
        foreach (var obj in InstanceFinder.ClientManager.Objects.Spawned.Values)
        {
            if (obj.IsOwner && obj.TryGetComponent(out NetworkLobbyPlayer player))
            {
                localPlayer = player;
                break;
            }
        }

        if (localPlayer != null)
        {
            UpdateProfit(localPlayer.profit.Value);
            UpdateBackground(localPlayer.country.Value, localPlayer.business.Value);
        }
    }

    private void UpdateBackground(string country, string business)
    {
        string bgName = $"{country}_{business}".ToLower().Replace(" ", "");
        Sprite bgSprite = Resources.Load<Sprite>($"Backgrounds/{bgName}");

        if (bgSprite != null)
        {
            backgroundImage.sprite = bgSprite;
            backgroundImage.color = Color.white;
        }
        else
        {
            Debug.LogWarning($"Background not found: {bgName}");
        }
    }
}