using UnityEngine;
using TMPro;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.UTP;
using System.Collections.Generic;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject startPanel;
    public GameObject nameCountryPanel;
    public GameObject hostClientPanel;
    public GameObject lobbyPanel;
    public GameObject settingsPanel;
    public GameObject soundPanel;
    public GameObject languagePanel;

    [Header("Inputs")]
    public TMP_InputField nameInput;
    public TMP_InputField roomCodeInput;

    [Header("Networking")]
    public LobbyUI lobbyUI;
    
    private bool isHosting = false;
    public UnityEngine.UI.Button hostButton;

    // [Header("Name & Color Panel")]
    // public Button[] colorButtons;     // assign swatch buttons
    // private int _selectedColorIndex = 0;

    private void Start()
    {
        // Load last used player name if available
        if (PlayerPrefs.HasKey("PlayerName"))
            nameInput.text = PlayerPrefs.GetString("PlayerName");

        DisconnectFromNetwork();
        CloseAllPanels();
        mainMenuPanel.SetActive(true);
        startPanel.SetActive(true);

        isHosting = false;
        if (hostButton != null) hostButton.interactable = true;

        if (lobbyUI != null) lobbyUI.SetRoomCode(string.Empty);
        if (roomCodeInput != null) roomCodeInput.text = string.Empty;
    }

    // --- UI Navigation ---

    public void OpenStartPanel()
    {
        CloseAllPanels();
        startPanel.SetActive(true);
    }

    public void OpenNameCountryPanel()
    {
        CloseAllPanels();
        nameCountryPanel.SetActive(true);
    }
    
    public void OpenSettingsPanel()
    {
        CloseAllPanels();
        settingsPanel.SetActive(true);
    }
    
    public void BacktoSettingsPanel()
    {
        soundPanel.SetActive(false);
        languagePanel.SetActive(false);
    }
    
    public void OpenSoundPanel()
    {
        soundPanel.SetActive(true);
        languagePanel.SetActive(false);
    }
    
    public void OpenLanguagePanel()
    {
        soundPanel.SetActive(false);
        languagePanel.SetActive(true);
    }
    

    public void OpenHostClientPanel()
    {
        CloseAllPanels();
        hostClientPanel.SetActive(true);
    }

    private void CloseAllPanels()
    {
        startPanel.SetActive(false);
        nameCountryPanel.SetActive(false);
        hostClientPanel.SetActive(false);
        lobbyPanel.SetActive(false);
        settingsPanel.SetActive(false);
    }

    // --- UI Button Handlers ---

    public void OnClickStartGame()
    {
        OpenNameCountryPanel();
    }

    public void OnClickQuitGame()
    {
        Application.Quit();
    }

    public void OnClickNextFromNameCountry()
    {
        bool nameValid = !string.IsNullOrWhiteSpace(nameInput.text);
        
        if (nameValid)
        {
            OpenHostClientPanel();
        }
        PlayerPrefs.SetString("PlayerName", nameInput.text);
        PlayerPrefs.Save();
    }

    public void OnClickBackToNameCountry()
    {
        OpenNameCountryPanel();
    }
    
    
    // --- Networking ---

    public async void HostGame()
    {
        if (isHosting)   // prevent double execution
        {
            Debug.LogWarning("Already hosting, ignoring duplicate click.");
            return;
        }
        isHosting = true;
        hostButton.interactable = false;

        Debug.Log("HostGame() called");

        SavePlayerInfo();

        try
        {
            Debug.Log("Requesting Relay allocation...");
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(4);
            Debug.Log("Relay allocation successful");

            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log("Join Code received: " + joinCode);

            NetworkManagerLobby.Instance.roomCode = joinCode;

            var transport = (UnityTransport)InstanceFinder.NetworkManager.TransportManager.Transport;
            transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

            InstanceFinder.ServerManager.StartConnection();
            InstanceFinder.ClientManager.StartConnection();

            mainMenuPanel.SetActive(false);
            lobbyPanel.SetActive(true);

            if (lobbyUI != null)
            {
                lobbyUI.SetRoomCode(joinCode);
            }
            else
            {
                Debug.LogError("lobbyUI is null!");
            }
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("Relay Host Failed: " + e.Message);
            isHosting = false; // reset on failure
        }
    }


    public async void JoinGame()
    {
        SavePlayerInfo();

        string joinCode = roomCodeInput.text.ToUpper();

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var transport = (UnityTransport)InstanceFinder.NetworkManager.TransportManager.Transport;
            transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            InstanceFinder.ClientManager.StartConnection();

            lobbyPanel.SetActive(true);
            hostClientPanel.SetActive(false);

            Invoke(nameof(RequestJoinRoom), 2f);

            if (lobbyUI != null)
                lobbyUI.SetRoomCode(joinCode);
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("Relay Join Failed: " + e.Message);
        }
    }

    private void RequestJoinRoom()
    {
        foreach (var obj in InstanceFinder.ClientManager.Objects.Spawned.Values)
        {
            if (obj.IsOwner && obj.TryGetComponent(out NetworkLobbyPlayer player))
            {
                player.JoinRoom(roomCodeInput.text.ToUpper());
                player.CmdSetProfile(nameInput.text,0);
                return;
            }
        }

        Debug.LogError("Local player not found! Has the player spawned yet?");
    }

    private void SavePlayerInfo()
    {
        PlayerPrefs.SetString("PlayerName", nameInput.text);
        PlayerPrefs.Save();
    }

    public void OnClickLobbyBackToMain()
{
    DisconnectFromNetwork();
        CloseAllPanels();
        mainMenuPanel.SetActive(true);
    startPanel.SetActive(true);

    isHosting = false;
    if (hostButton != null) hostButton.interactable = true;

    if (lobbyUI != null) lobbyUI.SetRoomCode(string.Empty);
    if (roomCodeInput != null) roomCodeInput.text = string.Empty;
}

private void DisconnectFromNetwork()
{
    var nm = InstanceFinder.NetworkManager;
    if (nm == null) return;

    if (nm.ServerManager.Started)
        nm.ServerManager.StopConnection(true);  


    if (nm.ClientManager.Started)
        nm.ClientManager.StopConnection();
}
}