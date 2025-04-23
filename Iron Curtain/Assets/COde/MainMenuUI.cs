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
    public TMP_InputField nameInput;
    public TMP_Dropdown businessDropdown;
    public TMP_Dropdown countryDropdown;
    public TMP_InputField roomCodeInput;

    public GameObject mainMenuPanel;
    public GameObject lobbyPanel;
    public NetworkManagerLobby networkManager;
    public LobbyUI lobbyUI;

    [Header("Business Info Preview")]
    public TMP_Text businessDescriptionText;
    public List<BusinessInfo> businessInfoList;

    private void Start()
    {
        if (PlayerPrefs.HasKey("PlayerName"))
            nameInput.text = PlayerPrefs.GetString("PlayerName");

        businessDropdown.onValueChanged.AddListener(OnBusinessChanged);
        OnBusinessChanged(businessDropdown.value);
    }

    public async void HostGame()
    {
        SavePlayerInfo();

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(4);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            NetworkManagerLobby.Instance.roomCode = joinCode;
            Debug.Log("Join Code: " + joinCode);

            PlayerPrefs.SetString("LastJoinCode", joinCode);

            var transport = (FishyUnityTransport)InstanceFinder.NetworkManager.TransportManager.Transport;
            transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

            InstanceFinder.ServerManager.StartConnection();
            InstanceFinder.ClientManager.StartConnection();

            mainMenuPanel.SetActive(false);
            lobbyPanel.SetActive(true);

            LobbyUI lobbyUI = lobbyPanel.GetComponent<LobbyUI>();
            if (lobbyUI != null)
            {
                lobbyUI.SetRoomCode(joinCode);
            }
            else
            {
                Debug.LogError("LobbyUI component not found on lobbyPanel!");
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

            if (lobbyUI != null)
                lobbyUI.SetRoomCode(joinCode);

            var transport = (FishyUnityTransport)InstanceFinder.NetworkManager.TransportManager.Transport;
            transport.SetRelayServerData(new RelayServerData(joinAllocation, "dtls"));

            InstanceFinder.ClientManager.StartConnection();

            mainMenuPanel.SetActive(false);
            lobbyPanel.SetActive(true);

            Invoke(nameof(RequestJoinRoom), 2f);
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
}
