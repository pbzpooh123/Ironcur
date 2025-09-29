using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using FishNet;
using FishNet.Managing.Scened;

public class LobbyUI : MonoBehaviour
{
    [Header("UI Panels")]
    public TMP_Text roomCodeText;
    public Transform playerListContainer;
    public GameObject playerEntryPrefab;
    public Button quitButton;
    public Button readyButton;
    public Button startGameButton;
    public GameObject lobbyPanel;

    private void Start()
    {
        if (roomCodeText == null) Debug.LogError("LobbyUI: roomCodeText is NOT assigned!");
        if (playerListContainer == null) Debug.LogError("LobbyUI: playerListContainer is NOT assigned!");
        if (playerEntryPrefab == null) Debug.LogError("LobbyUI: playerEntryPrefab is NOT assigned!");
        if (quitButton == null) Debug.LogError("LobbyUI: quitButton is NOT assigned!");
        if (readyButton == null) Debug.LogError("LobbyUI: readyButton is NOT assigned!");
        if (startGameButton == null) Debug.LogError("LobbyUI: startGameButton is NOT assigned!");

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
            Debug.Log("All players ready. Switching to game scene...");

            SceneLoadData loadData = new SceneLoadData("MainGameScene")
            {
                ReplaceScenes = ReplaceOption.All
            };

            InstanceFinder.SceneManager.LoadGlobalScenes(loadData);
            CloseAllPanels();
        }
    }

    private void CloseAllPanels()
    {
        lobbyPanel.SetActive(false);
    }

    private void QuitLobby()
    {
        if (InstanceFinder.IsServer)
        {
            InstanceFinder.ServerManager.StopConnection(true);
        }

        if (InstanceFinder.IsClient)
        {
            InstanceFinder.ClientManager.StopConnection();
        }

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
