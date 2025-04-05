using UnityEngine;
using TMPro;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;

public class MainMenuUI : MonoBehaviour
{
    public TMP_InputField nameInput;
    public TMP_Dropdown businessDropdown;
    public TMP_Dropdown countryDropdown;
    public TMP_InputField roomCodeInput;

    public GameObject mainMenuPanel;
    public GameObject lobbyPanel;
    public NetworkManagerLobby networkManager;

    private void Start()
    {
        // Load saved player name (if any)
        if (PlayerPrefs.HasKey("PlayerName"))
            nameInput.text = PlayerPrefs.GetString("PlayerName");
    }

    public void HostGame()
    {
        SavePlayerInfo();

        // Start both server and client for hosting
        InstanceFinder.ServerManager.StartConnection();
        InstanceFinder.ClientManager.StartConnection();

        // Switch UI panels
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
    }

    public void JoinGame()
    {
        SavePlayerInfo();

        // Start only the client connection
        InstanceFinder.ClientManager.StartConnection();

        // Delay room join request to give client time to connect and spawn
        Invoke(nameof(RequestJoinRoom), 1.5f);
    }

    private void RequestJoinRoom()
    {
        foreach (var obj in InstanceFinder.ClientManager.Objects.Spawned.Values)
        {
            if (obj.IsOwner && obj.TryGetComponent(out NetworkLobbyPlayer player))
            {
                player.JoinRoom(roomCodeInput.text.ToUpper());
                return;
            }
        }

        Debug.LogError("Local player not found! Has the player spawned yet?");
    }

    private void SavePlayerInfo()
    {
        PlayerPrefs.SetString("PlayerName", nameInput.text);
        PlayerPrefs.SetString("Business", businessDropdown.options[businessDropdown.value].text);
        PlayerPrefs.SetString("Country", countryDropdown.options[countryDropdown.value].text);
        PlayerPrefs.Save();
    }
}
