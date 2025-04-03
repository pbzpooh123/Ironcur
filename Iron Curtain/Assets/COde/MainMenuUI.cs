using UnityEngine;
using TMPro;
using Mirror;

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
        if (PlayerPrefs.HasKey("PlayerName"))
            nameInput.text = PlayerPrefs.GetString("PlayerName");
    }

    public void HostGame()
    {
        SavePlayerInfo();
        networkManager.StartHost();
        mainMenuPanel.SetActive(false);
        lobbyPanel.SetActive(true);
    }

    public void JoinGame()
    {
        SavePlayerInfo();
        networkManager.StartClient();
        Invoke(nameof(RequestJoinRoom), 1.5f); // Delay to allow client to connect
    }

    void RequestJoinRoom()
    {
        NetworkLobbyPlayer localPlayer = NetworkClient.localPlayer?.GetComponent<NetworkLobbyPlayer>();
        if (localPlayer != null)
        {
            localPlayer.CmdJoinRoom(roomCodeInput.text.ToUpper());
        }
        else
        {
            Debug.LogError("Local player not found! Ensure the client has connected.");
        }
    }


    void SavePlayerInfo()
    {
        PlayerPrefs.SetString("PlayerName", nameInput.text);
        PlayerPrefs.SetString("Business", businessDropdown.options[businessDropdown.value].text);
        PlayerPrefs.SetString("Country", countryDropdown.options[countryDropdown.value].text);
        PlayerPrefs.Save();
    }
}