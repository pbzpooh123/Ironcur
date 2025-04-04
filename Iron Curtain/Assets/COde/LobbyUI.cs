using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;

public class LobbyUI : MonoBehaviour
{
    public TMP_Text roomCodeText;
    public Transform playerListContainer;
    public GameObject playerEntryPrefab;
    public Button quitButton;

    private void Start()
    {
        if (roomCodeText == null) Debug.LogError("LobbyUI: roomCodeText is NOT assigned!");
        if (playerListContainer == null) Debug.LogError("LobbyUI: playerListContainer is NOT assigned!");
        if (playerEntryPrefab == null) Debug.LogError("LobbyUI: playerEntryPrefab is NOT assigned!");
        if (quitButton == null) Debug.LogError("LobbyUI: quitButton is NOT assigned!");

        quitButton.onClick.AddListener(QuitLobby);

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
    }

    void QuitLobby()
    {
        NetworkManagerLobby.instance.StopHost();
        Application.Quit();
    }
}
