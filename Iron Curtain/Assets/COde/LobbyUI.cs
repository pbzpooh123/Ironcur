using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using FishNet;

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
        startGameButton.interactable = false;
    }

    public void SetRoomCode(string code)
    {
        roomCodeText.text = "Room Code: " + NetworkManagerLobby.Instance.roomCode;

    }

    public void UpdatePlayerList(List<string> playerDetails)
    {
        if (playerListContainer == null || playerEntryPrefab == null)
        {
            Debug.LogError("LobbyUI: playerListContainer or playerEntryPrefab is NULL when updating player list!");
            return;
        }

        foreach (Transform child in playerListContainer)
            Destroy(child.gameObject);

        foreach (var details in playerDetails)
        {
            GameObject entry = Instantiate(playerEntryPrefab, playerListContainer);

            TMP_Text textComponent = entry.GetComponent<TMP_Text>();
            if (textComponent == null)
                textComponent = entry.GetComponentInChildren<TMP_Text>();

            if (textComponent == null)
            {
                Debug.LogError("LobbyUI: playerEntryPrefab does NOT have a TMP_Text component!");
                continue;
            }

            textComponent.text = details;
        }

        // Only the host (server) can start the game
        if (InstanceFinder.IsServer)
        {
            startGameButton.interactable = NetworkManagerLobby.Instance.AllPlayersReady();
        }
    }

    private void OnReadyClicked()
    {
        foreach (var obj in InstanceFinder.ClientManager.Objects.Spawned.Values)
        {
            if (obj.IsOwner && obj.TryGetComponent(out NetworkLobbyPlayer player))
            {
                player.ToggleReady();
                return;
            }
        }

        Debug.LogError("ReadyClicked: Local player not found!");
    }

    private void OnStartClicked()
    {
        if (InstanceFinder.IsServer && NetworkManagerLobby.Instance.AllPlayersReady())
        {
            Debug.Log("All players ready. Starting game...");
            // Load the game scene or trigger the next step
        }
    }

    private void QuitLobby()
    {
        if (InstanceFinder.IsServer)
        {
            InstanceFinder.ServerManager.StopConnection(true); // Stop server & all clients
        }

        if (InstanceFinder.IsClient)
        {
            InstanceFinder.ClientManager.StopConnection(); // Stop client
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
