using UnityEngine;
using TMPro;
using Unity.Services.Core;
using Unity.Services.Authentication;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting.UTP;
using FishNet.Managing.Client;
using FishNet.Transporting;


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
        // Initialize UGS + anonymous sign-in once at boot
        await EnsureUnityServicesAsync();

        // Load last used player name if available
        if (PlayerPrefs.HasKey("PlayerName"))
            nameInput.text = PlayerPrefs.GetString("PlayerName");

        // Use a proper connection event instead of Invoke(...)
        InstanceFinder.ClientManager.OnClientConnectionState += OnClientConnectionStateChanged;

        OpenStartPanel();
    }

    private void OnDestroy()
    {
        if (InstanceFinder.ClientManager != null)
            InstanceFinder.ClientManager.OnClientConnectionState -= OnClientConnectionStateChanged;
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

    // --- UI Navigation (unchanged) ---
    public void OpenStartPanel() { CloseAllPanels(); startPanel.SetActive(true); }
    public void OpenNameCountryPanel() { CloseAllPanels(); nameCountryPanel.SetActive(true); }
    public void OpenSettingsPanel() { CloseAllPanels(); settingsPanel.SetActive(true); }
    public void BacktoSettingsPanel(){ soundPanel.SetActive(false); languagePanel.SetActive(false); }
    public void OpenSoundPanel() { soundPanel.SetActive(true); languagePanel.SetActive(false); }
    public void OpenLanguagePanel(){ soundPanel.SetActive(false); languagePanel.SetActive(true); }
    public void OpenBusinessPanel(){ CloseAllPanels(); businessPanel.SetActive(true); }
    public void OpenHostClientPanel(){ CloseAllPanels(); hostClientPanel.SetActive(true); }
    private void CloseAllPanels(){ startPanel.SetActive(false); nameCountryPanel.SetActive(false); businessPanel.SetActive(false); hostClientPanel.SetActive(false);  settingsPanel.SetActive(false); }

    public void OnClickStartGame(){ OpenNameCountryPanel(); }
    public void OnClickQuitGame(){ Application.Quit(); }
    public void OnClickNextFromNameCountry()
    {
        bool nameValid = !string.IsNullOrWhiteSpace(nameInput.text);
        bool countrySelected = countryDropdown.value >= 0;
        bool businessSelected = businessDropdown.value >= 0;

        if (nameValid && countrySelected && businessSelected) OpenHostClientPanel();
        else Debug.LogWarning("Please enter a name");
    }
    public void OnClickBackToNameCountry(){ OpenNameCountryPanel(); }

    // --- Networking ---

    public async void HostGame()
    {
        if (isHosting) { Debug.LogWarning("Already hosting, ignoring duplicate click."); return; }
        isHosting = true; hostButton.interactable = false;

        SavePlayerInfo();

        try
        {
            await EnsureUnityServicesAsync();

            // For a 4-player game (host + 3 clients) pass 3
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(3);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);

            // Store/show room code
            NetworkManagerLobby.Instance.roomCode = joinCode;
            if (lobbyUI != null) lobbyUI.SetRoomCode(joinCode);


            // Configure FishyUnityTransport for Relay
            var utp = InstanceFinder.NetworkManager.TransportManager.GetTransport<UnityTransport>();

            var transport = (UnityTransport)InstanceFinder.NetworkManager.TransportManager.Transport;
            transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));
            // If you’re on newer packages, prefer AllocationUtils.ToRelayServerData(allocation, "dtls");
            utp.SetRelayServerData(new RelayServerData(allocation, "dtls"));
            

            // Start server then (optionally) local client
            if (InstanceFinder.ServerManager.StartConnection())
            {
                InstanceFinder.ClientManager.StartConnection();
            }
            else
            {
                Debug.LogError("Server failed to start.");
                isHosting = false; hostButton.interactable = true;
                return;
            }

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
            var transport = (UnityTransport)InstanceFinder.NetworkManager.TransportManager.Transport;
            transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));


            InstanceFinder.ClientManager.StartConnection();
           
            lobbyPanel.SetActive(true);
            mainMenuPanel.SetActive(false);
            
            _pendingJoinCode = joinCode;

            if (lobbyUI != null) lobbyUI.SetRoomCode(joinCode);
            
        }
        catch (RelayServiceException e)
        {
            Debug.LogError("Relay Join Failed: " + e.Message);
        }
    }

    private string _pendingJoinCode;

    private void OnClientConnectionStateChanged(FishNet.Transporting.ClientConnectionStateArgs args)
    {
        if (args.ConnectionState == LocalConnectionState.Started)
        {
            // Local client connected: now your player object will spawn.
            // Wait one frame so spawned list is populated.
            StartCoroutine(CallJoinOnLocalPlayerNextFrame());
        }
    }

    private System.Collections.IEnumerator CallJoinOnLocalPlayerNextFrame()
    {
        yield return null;

        foreach (var nob in InstanceFinder.ClientManager.Objects.Spawned.Values)
        {
            if (nob.IsOwner && nob.TryGetComponent(out NetworkLobbyPlayer player))
            {
                if (!string.IsNullOrEmpty(_pendingJoinCode))
                    player.JoinRoom(_pendingJoinCode); // your [ServerRpc] JoinRoom(...)
                _pendingJoinCode = null;
                yield break;
            }
        }
        Debug.LogWarning("Local player not found yet; will try again next frame.");
        StartCoroutine(CallJoinOnLocalPlayerNextFrame());
    }

    private void SavePlayerInfo()
    {
        PlayerPrefs.SetString("PlayerName", nameInput.text);
        // consider also saving Business/Country if you use them elsewhere
        PlayerPrefs.Save();
    }
}
