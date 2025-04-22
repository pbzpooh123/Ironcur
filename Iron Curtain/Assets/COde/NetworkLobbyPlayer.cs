using FishNet.Object;
using FishNet.Connection;
using FishNet.Object.Synchronizing;
using UnityEngine;
using System.Collections.Generic;

public class NetworkLobbyPlayer : NetworkBehaviour
{
    [SerializeField] private string _playerNameInspector;
    [SerializeField] private string _businessInspector;
    [SerializeField] private string _countryInspector;
    [SerializeField] private float _profitInspector;
    public readonly SyncVar<string> playerName = new();
    public readonly SyncVar<string> business = new();
    public readonly SyncVar<string> country = new();
    public readonly SyncVar<bool> isReady = new();
    public readonly SyncVar<float> profit = new();
    
    private LobbyUI lobbyUI;
    
    private void OnNameChanged(string oldVal, string newVal, bool asServer)
    {
        _playerNameInspector = newVal;
    }

    private void OnBusinessChanged(string oldVal, string newVal, bool asServer)
    {
        _businessInspector = newVal;
    }

    private void OnCountryChanged(string oldVal, string newVal, bool asServer)
    {
        _countryInspector = newVal;
    }

    private void OnProfitChanged(float oldVal, float newVal, bool asServer)
    {
        _profitInspector = newVal;
    }

    public override void OnStartClient()
    {
        base.OnStartClient();
        Invoke(nameof(FindLobbyUI), 0.5f);
    }

    void FindLobbyUI()
    {
        lobbyUI = FindObjectOfType<LobbyUI>();

        if (lobbyUI == null)
        {
            Debug.LogError("LobbyUI still not found! Check if it exists in the scene.");
            return;
        }

        if (IsOwner)
        {
            SetPlayerInfo(
                PlayerPrefs.GetString("PlayerName"),
                PlayerPrefs.GetString("Business"),
                PlayerPrefs.GetString("Country")
            );

            RequestRoomCode();
        }
    }

    [ServerRpc]
    public void SetPlayerInfo(string newName, string newBusiness, string newCountry)
    {
        playerName.Value = newName;
        business.Value = newBusiness;
        country.Value = newCountry;


        NetworkManagerLobby.Instance.UpdateLobbyUI();
    }

    [ServerRpc]
    public void RequestRoomCode()
    {
        TargetReceiveRoomCode(Owner, NetworkManagerLobby.Instance.roomCode);
    }

    [TargetRpc]
    public void TargetReceiveRoomCode(NetworkConnection conn, string code)
    {
        LobbyUI ui = FindObjectOfType<LobbyUI>();
        if (ui != null)
        {
            ui.SetRoomCode(code);
        }
    }

    [ObserversRpc]
    public void UpdatedPlayerList(List<string> playerDetails)

    {
        if (lobbyUI == null)
        {
            lobbyUI = FindObjectOfType<LobbyUI>();
            if (lobbyUI == null)
            {
                Debug.LogError("NetworkLobbyPlayer: LobbyUI is still missing! Cannot update player list.");
                return;
            }
        }

        lobbyUI.UpdatePlayerList(playerDetails);
    }

    [ServerRpc]
    public void JoinRoom(string enteredCode)
    {
        if (NetworkManagerLobby.Instance.roomCode == enteredCode)
        {
            TargetShowWaitingPanel(Owner);
        }
        else
        {
            Debug.Log("Invalid Room Code!");
        }
    }

    [TargetRpc]
    public void TargetShowWaitingPanel(NetworkConnection conn)
    {
        MainMenuUI ui = FindObjectOfType<MainMenuUI>();
        if (ui != null)
        {
            ui.lobbyPanel.SetActive(true);
            ui.mainMenuPanel.SetActive(false);
        }
    }

    [ServerRpc]
    public void ToggleReady()
    {
        isReady.Value = !isReady.Value;
        NetworkManagerLobby.Instance.UpdateLobbyUI();
    }

    private void OnReadyStatusChanged(bool oldVal, bool newVal, bool asServer)
    {
        if (lobbyUI == null) lobbyUI = FindObjectOfType<LobbyUI>();
        lobbyUI?.UpdatePlayerList(NetworkManagerLobby.Instance.GetPlayerList());
    }
    [TargetRpc]
    public void TargetUpdateProfit(NetworkConnection conn, float value)
    {
        GameUI.Instance?.UpdateProfit(value);
    }

    [TargetRpc]
    public void TargetShowEndScreen(NetworkConnection conn, string winnerName)
    {
        GameUI.Instance?.ShowWinner(winnerName);
    }

}
