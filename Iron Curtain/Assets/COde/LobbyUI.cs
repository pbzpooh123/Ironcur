using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using Mirror;

public class LobbyUI : MonoBehaviour
{
    public TMP_Text roomCodeText;
    public Transform playerListContainer;
    public GameObject playerEntryPrefab;
    public Button quitButton;
    public Button readyButton;
    public Button startGameButton;


    private void Start()
    {
        if (roomCodeText == null) Debug.LogError("LobbyUI: roomCodeText is NOT assigned!");
        if (playerListContainer == null) Debug.LogError("LobbyUI: playerListContainer is NOT assigned!");
        if (playerEntryPrefab == null) Debug.LogError("LobbyUI: playerEntryPrefab is NOT assigned!");
        if (quitButton == null) Debug.LogError("LobbyUI: quitButton is NOT assigned!");

        quitButton.onClick.AddListener(QuitLobby);
        readyButton.onClick.AddListener(OnReadyClicked);
        startGameButton.onClick.AddListener(OnStartClicked);
        startGameButton.interactable = false; // Only interactable if host AND all ready


        // Removed the old line that showed the room code immediately
        // roomCodeText.text = "Room Code: " + NetworkManagerLobby.instance.roomCode;
    }

    public void SetRoomCode(string code)
    {
        roomCodeText.text = "Room Code: " + code;
    }

    public void UpdatePlayerList(List<string> playerDetails)
    {
        if (playerListContainer == null)
        {
            Debug.LogError("LobbyUI: playerListContainer is NULL when updating player list!");
            return;
        }

        if (playerEntryPrefab == null)
        {
            Debug.LogError("LobbyUI: playerEntryPrefab is NULL when updating player list!");
            return;
        }

        foreach (Transform child in playerListContainer)
            Destroy(child.gameObject);

        foreach (var details in playerDetails)
        {
            GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);

            TMP_Text textComponent = entry.GetComponent<TMP_Text>();
            if (textComponent == null)
            {
                textComponent = entry.GetComponentInChildren<TMP_Text>();
                if (textComponent == null)
                {
                    Debug.LogError("LobbyUI: playerEntryPrefab does NOT have a TMP_Text component!");
                    continue;
                }
            }

            textComponent.text = details;
        }
        
        if (NetworkServer.active) // Host only
        {
            startGameButton.interactable = NetworkManagerLobby.instance.AllPlayersReady();
        }

    }
    
    void OnReadyClicked()
    {
        var localPlayer = NetworkClient.connection.identity.GetComponent<NetworkLobbyPlayer>();
        localPlayer.CmdToggleReady();
    }

    void OnStartClicked()
    {
        if (NetworkServer.active && NetworkManagerLobby.instance.AllPlayersReady())
        {
            Debug.Log("All players ready. Starting game...");
            // Load your game scene
            NetworkManagerLobby.instance.ServerChangeScene("MainGameScene"); // Replace with your scene name
        }
    }


    void QuitLobby()
    {
        NetworkManagerLobby.instance.StopHost();
        Application.Quit();
    }
}
