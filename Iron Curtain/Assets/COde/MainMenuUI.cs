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

public class MainMenuUI : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;
    public GameObject startPanel;
    public GameObject nameCountryPanel;
    public GameObject businessPanel;
    public GameObject hostClientPanel;
    public GameObject lobbyPanel;

    [Header("Inputs")]
    public TMP_InputField nameInput;
    public TMP_Dropdown businessDropdown;
    public TMP_Dropdown countryDropdown;
    public TMP_InputField roomCodeInput;

    [Header("Business Info Preview")]
    public TMP_Text businessDescriptionText;
    public List<BusinessInfo> businessInfoList;

    [Header("Networking")]
    public LobbyUI lobbyUI;

    private void Start()
    {
        // Load last used player name if available
        if (PlayerPrefs.HasKey("PlayerName"))
            nameInput.text = PlayerPrefs.GetString("PlayerName");

        businessDropdown.onValueChanged.AddListener(OnBusinessChanged);
        OnBusinessChanged(businessDropdown.value);

        OpenStartPanel(); // Default start panel
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

    public void OpenBusinessPanel()
    {
        CloseAllPanels();
        businessPanel.SetActive(true);
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
        businessPanel.SetActive(false);
        hostClientPanel.SetActive(false);
        lobbyPanel.SetActive(false);
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
        bool countrySelected = countryDropdown.value >= 0;
        bool businessSelected = businessDropdown.value >= 0;

        if (nameValid && countrySelected && businessSelected)
        {
            OpenHostClientPanel();
        }
        else
        {
            Debug.LogWarning("Please enter a name and select a country.");
        }
    }

    public void OnClickBackToNameCountry()
    {
        OpenNameCountryPanel();
    }

    public void OnClickConfirmBusiness()
    {
        if (businessDropdown.value >= 0)
        {
            OpenHostClientPanel();
        }
        else
        {
            Debug.LogWarning("Please select a business before continuing.");
        }
    }

    // --- Business Description ---

    public void OnBusinessChanged(int index)
    {
        if (index < 0 || index >= businessInfoList.Count || businessDescriptionText == null)
        {
            businessDescriptionText.text = "No info available.";
            return;
        }

        BusinessInfo info = businessInfoList[index];
        businessDescriptionText.text = $"<b>{info.businessName}</b>\n\n" +
                                       $"<b>Description:</b>\n{info.description}\n\n" +
                                       $"<color=green><b>Perk:</b></color>\n{info.perk}\n\n";
    }

    // --- Networking ---

    public async void HostGame()
    {
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

            var transport = (FishyUnityTransport)InstanceFinder.NetworkManager.TransportManager.Transport;
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
        }
    }


    public async void JoinGame()
    {
        SavePlayerInfo();

        string joinCode = roomCodeInput.text.ToUpper();

        try
        {
            JoinAllocation joinAllocation = await RelayService.Instance.JoinAllocationAsync(joinCode);

            var transport = (FishyUnityTransport)InstanceFinder.NetworkManager.TransportManager.Transport;
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
                return;
            }
        }

        Debug.LogError("Local player not found! Has the player spawned yet?");
    }

    private void SavePlayerInfo()
    {
        PlayerPrefs.SetString("PlayerName", nameInput.text);
        PlayerPrefs.SetString("Business", businessDropdown.options[businessDropdown.value].text);
        PlayerPrefs.SetString("Country", countryDropdown.options[countryDropdown.value].text);
        PlayerPrefs.Save();
    }
}
