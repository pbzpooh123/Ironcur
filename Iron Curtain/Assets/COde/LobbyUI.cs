using UnityEngine;
using TMPro;
using UnityEngine.UI;
using System.Collections.Generic;
using FishNet;
using FishNet.Managing.Scened;
using FishNet.Transporting;

public class LobbyUI : MonoBehaviour
{
    [Header("UI")]
    public TMP_Text roomCodeText;
    public Transform playerListContainer;
    public GameObject playerEntryPrefab;
    public Button quitButton;
    public Button readyButton;
    public Button startGameButton;
    public GameObject lobbyPanel;

    private void Start()
    {
        if (!roomCodeText) Debug.LogError("LobbyUI: roomCodeText is NOT assigned!");
        if (!playerListContainer) Debug.LogError("LobbyUI: playerListContainer is NOT assigned!");
        if (!playerEntryPrefab) Debug.LogError("LobbyUI: playerEntryPrefab is NOT assigned!");
        if (!quitButton) Debug.LogError("LobbyUI: quitButton is NOT assigned!");
        if (!readyButton) Debug.LogError("LobbyUI: readyButton is NOT assigned!");
        if (!startGameButton) Debug.LogError("LobbyUI: startGameButton is NOT assigned!");
        
        readyButton.onClick.AddListener(OnReadyClicked);
        startGameButton.onClick.AddListener(OnStartClicked);
        
        startGameButton.gameObject.SetActive(InstanceFinder.IsServer);
        
        startGameButton.interactable = false;
    }
    

    public void SetRoomCode(string code)
    {
        if (!roomCodeText) return;
        // Show exactly what we’re passed (keeps things in sync no matter who calls it)
        roomCodeText.text = string.IsNullOrEmpty(code) ? "Room Code: —" : $"Room Code: {code}";
    }

    /// <summary>
    /// Call this from your lobby manager whenever the roster or any ready state changes.
    /// </summary>
    public void UpdatePlayerList(List<string> playerDetails)
    {
        if (!playerListContainer || !playerEntryPrefab)
        {
            Debug.LogError("LobbyUI: playerListContainer or playerEntryPrefab is NULL when updating player list!");
            return;
        }

        foreach (Transform child in playerListContainer)
            Destroy(child.gameObject);

        foreach (var details in playerDetails)
        {
            var entry = Instantiate(playerEntryPrefab, playerListContainer);

            // Allow the prefab to hold the TMP on root OR child
            var textComponent = entry.GetComponent<TMP_Text>();
            if (!textComponent) textComponent = entry.GetComponentInChildren<TMP_Text>();

            if (!textComponent)
            {
                Debug.LogError("LobbyUI: playerEntryPrefab does NOT have a TMP_Text component!");
                continue;
            }

            textComponent.text = details;
        }

       
        if (InstanceFinder.IsServer)
            startGameButton.interactable = NetworkManagerLobby.Instance.AllPlayersReady();
    }

    private void OnReadyClicked()
    {
        if (NetworkLobbyPlayer.Local != null)
        {
            NetworkLobbyPlayer.Local.ToggleReady();
            return;
        }
        StartCoroutine(WaitForLocalAndToggle());
    }

    private System.Collections.IEnumerator WaitForLocalAndToggle()
    {
        float t = 2f;
        while (t > 0f && NetworkLobbyPlayer.Local == null)
        {
            t -= Time.unscaledDeltaTime;
            yield return null;
        }
        if (NetworkLobbyPlayer.Local != null)
            NetworkLobbyPlayer.Local.ToggleReady();
        else
            Debug.LogWarning("Local lobby player not spawned yet; try again shortly.");
    }

    private void OnStartClicked()
    {
        if (!InstanceFinder.IsServer)
            return;

        if (!NetworkManagerLobby.Instance.AllPlayersReady())
            return;
        
        Load("MainGameScene");
        UnLoad("Lobby");
    }

    private void Load(string sceneName)
    {
        if (!InstanceFinder.IsServer) return;

        SceneLoadData sld = new SceneLoadData(sceneName);
        InstanceFinder.SceneManager.LoadGlobalScenes(sld);
    }
    private void UnLoad(string sceneName)
    {
        if (!InstanceFinder.IsServer) return;

        SceneUnloadData sld = new   SceneUnloadData(sceneName);
        InstanceFinder.SceneManager.UnloadGlobalScenes(sld);
    }
    
    
    private void QuitLobby()
    {
        // Clients: disconnect self
        if (InstanceFinder.IsClient)
            InstanceFinder.ClientManager.StopConnection();

        // Host: shuts down server & kicks clients
        if (InstanceFinder.IsServer)
            InstanceFinder.ServerManager.StopConnection(true);

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
    
}
