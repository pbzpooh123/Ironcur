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
    public GameObject lobbyPanel;

    [Header("Optional")]
    public Button copyCodeButton;

    // NEW: keep rows so we can recolor when color ownership changes
    private readonly List<(PlayerEntryRow row, int cid)> _rows = new();

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

        // Host-only visibility at open
        startGameButton.gameObject.SetActive(InstanceFinder.IsServerStarted);

        if (copyCodeButton != null)
            copyCodeButton.onClick.AddListener(CopyRoomCodeToClipboard);
    }

    public void SetRoomCode(string code)
    {
        roomCodeText.text = string.IsNullOrWhiteSpace(code) ? "Room Code: —" : $"Room Code: {code}";
    }

    public void UpdatePlayerList(List<string> names, List<bool> readies, List<int> connIds)
    {
        // clear current rows
        foreach (Transform child in playerListContainer)
            Destroy(child.gameObject);
        _rows.Clear();

        // local client id
        int localCid = InstanceFinder.ClientManager != null
            ? InstanceFinder.ClientManager.Connection.ClientId
            : -1;

        for (int i = 0; i < names.Count; i++)
        {
            var go = Instantiate(playerEntryPrefab, playerListContainer);
            var row = go.GetComponent<PlayerEntryRow>();
            if (row == null)
            {
                Debug.LogError("playerEntryPrefab must have a PlayerEntryRow component.");
                continue;
            }

            bool isLocal = (connIds[i] == localCid);
            row.Bind(
                names[i],
                readies[i],
                isLocal,
                onLocalToggleChanged: (bool val) =>
                {
                    var lp = FindLocalLobbyPlayer();
                    if (lp != null) lp.SetReady(val); // call ServerRpc
                }
            );

            // keep for recoloring later
            _rows.Add((row, connIds[i]));
        }

        // tint names now, based on latest color ownership snapshot
        RefreshNameColors();

        // Only the host sees Start, and it’s enabled only if everyone is ready
        bool isHost = InstanceFinder.IsServerStarted;
        startGameButton.gameObject.SetActive(isHost);
        startGameButton.interactable = isHost && NetworkManagerLobby.Instance.AllPlayersReady();
    }

    // NEW: recolor rows after color ownership updates
    public void RefreshNameColors()
    {
        foreach (var item in _rows)
        {
            if (LobbyColorBinder.TryGetColorForCid(item.cid, out var col))
                item.row.SetNameColor(col);
            else
                item.row.SetNameColor(Color.white);
        }
    }

    private NetworkLobbyPlayer FindLocalLobbyPlayer()
    {
        foreach (var obj in InstanceFinder.ClientManager.Objects.Spawned.Values)
            if (obj.IsOwner && obj.TryGetComponent(out NetworkLobbyPlayer p))
                return p;
        return null;
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
        if (InstanceFinder.IsServerStarted && NetworkManagerLobby.Instance.AllPlayersReady())
        {
            Debug.Log("All players ready. Switching to game scene...");

            var loadData = new FishNet.Managing.Scened.SceneLoadData("MainGameScene")
            {
                ReplaceScenes = FishNet.Managing.Scened.ReplaceOption.All
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
        if (InstanceFinder.IsServerStarted)
            InstanceFinder.ServerManager.StopConnection(true);

        if (InstanceFinder.IsClientStarted)
            InstanceFinder.ClientManager.StopConnection();

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void CopyRoomCodeToClipboard()
    {
        if (roomCodeText == null) return;
        string raw = roomCodeText.text;
        string code = raw.Replace("Room Code:", "").Trim();
        GUIUtility.systemCopyBuffer = code;
        Debug.Log($"[LobbyUI] Copied room code: {code}");
    }
}