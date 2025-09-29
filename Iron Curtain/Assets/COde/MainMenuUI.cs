using UnityEngine;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using FishNet;
using FishNet.Transporting.UTP;

public class MainMenuUI : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject startPanel;
    public GameObject nameCountryPanel;
    public GameObject businessPanel;
    public GameObject hostClientPanel;
    public GameObject lobbyPanel;
    public GameObject settingsPanel;
    public GameObject soundPanel;
    public GameObject languagePanel;

    [Header("Inputs")]
    public TMP_InputField nameInput;
    public TMP_Dropdown businessDropdown;
    public TMP_Dropdown countryDropdown;
    public TMP_InputField roomCodeInput;

    [Header("Business Info Preview")]
    public TMP_Text businessDescriptionText;

    [Header("Networking")]
    public LobbyUI lobbyUI;
    public UnityEngine.UI.Button hostButton;

    private bool isHosting;

    private async void Start()
    {
        await EnsureUnityServicesAsync();

        if (PlayerPrefs.HasKey("PlayerName"))
            nameInput.text = PlayerPrefs.GetString("PlayerName");

        OpenStartPanel();
    }

    private async System.Threading.Tasks.Task EnsureUnityServicesAsync()
    {
        if (UnityServices.State != ServicesInitializationState.Initialized &&
            UnityServices.State != ServicesInitializationState.Initializing)
        {
            await UnityServices.InitializeAsync();
        }
        if (!AuthenticationService.Instance.IsSignedIn)
        {
            await AuthenticationService.Instance.SignInAnonymouslyAsync();
        }
    }

    // === UI Navigation ===
    public void OpenStartPanel() { CloseAllPanels(); startPanel.SetActive(true); }
    public void OpenNameCountryPanel() { CloseAllPanels(); nameCountryPanel.SetActive(true); }
    public void OpenSettingsPanel() { CloseAllPanels(); settingsPanel.SetActive(true); }
    public void BacktoSettingsPanel(){ soundPanel.SetActive(false); languagePanel.SetActive(false); }
    public void OpenSoundPanel() { soundPanel.SetActive(true); languagePanel.SetActive(false); }
    public void OpenLanguagePanel(){ soundPanel.SetActive(false); languagePanel.SetActive(true); }
    public void OpenBusinessPanel(){ CloseAllPanels(); businessPanel.SetActive(true); }
    public void OpenHostClientPanel(){ CloseAllPanels(); hostClientPanel.SetActive(true); }
    private void CloseAllPanels(){ 
        startPanel.SetActive(false); 
        nameCountryPanel.SetActive(false); 
        businessPanel.SetActive(false); 
        hostClientPanel.SetActive(false);  
        settingsPanel.SetActive(false); 
    }

    public void OnClickStartGame(){ OpenNameCountryPanel(); }
    public void OnClickQuitGame(){ Application.Quit(); }
    public void OnClickNextFromNameCountry()
    {
        bool nameValid = !string.IsNullOrWhiteSpace(nameInput.text);
        bool countrySelected = countryDropdown.value >= 0;
        bool businessSelected = businessDropdown.value >= 0;

        if (nameValid && countrySelected && businessSelected) 
            OpenHostClientPanel();
        else 
            Debug.LogWarning("Please enter a name");
    }
    public void OnClickBackToNameCountry(){ OpenNameCountryPanel(); }

    // === Networking ===
    public async void HostGame()
    {
        if (isHosting) return;
        isHosting = true; hostButton.interactable = false;

        SavePlayerInfo();

        try
        {
            await EnsureUnityServicesAsync();

            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            NetworkManagerLobby.Instance.roomCode = joinCode;
            if (lobbyUI != null) lobbyUI.SetRoomCode(joinCode);

            var utp = InstanceFinder.NetworkManager.TransportManager.GetTransport<UnityTransport>();
            utp.SetRelayServerData(new RelayServerData(allocation, "dtls"));

            if (InstanceFinder.ServerManager.StartConnection())
                InstanceFinder.ClientManager.StartConnection();

            mainMenuPanel.SetActive(false);
            lobbyPanel.SetActive(true);
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("Relay Host Failed: " + e.Message);
            isHosting = false; hostButton.interactable = true;
        }
    }

    public async void JoinGame()
    {
        SavePlayerInfo();

        string joinCode = roomCodeInput.text.Trim().ToUpper();
        if (string.IsNullOrEmpty(joinCode) || joinCode.Length < 6)
        {
            Debug.LogWarning("Invalid room code.");
            return;
        }

        try
        {
            await EnsureUnityServicesAsync();

            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var utp = InstanceFinder.NetworkManager.TransportManager.GetTransport<UnityTransport>();
            utp.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            InstanceFinder.ClientManager.StartConnection();
            CloseAllPanels();
            lobbyPanel.SetActive(true);
            hostClientPanel.SetActive(false);

            if (lobbyUI != null) lobbyUI.SetRoomCode(joinCode);
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("Relay Join Failed: " + e.Message);
        }
    }

    private void SavePlayerInfo()
    {
        PlayerPrefs.SetString("PlayerName", nameInput.text);
        PlayerPrefs.Save();
    }
}
