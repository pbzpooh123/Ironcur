using UnityEngine;
using TMPro;
using Unity.Services.Relay;
using Unity.Services.Relay.Models;
using Unity.Networking.Transport.Relay;
using FishNet;
using FishNet.Managing;
using FishNet.Transporting;
using FishNet.Transporting.UTP;

public class MainMenuUI : MonoBehaviour
{
    public TMP_InputField nameInput;
    public TMP_Dropdown businessDropdown;
    public TMP_Dropdown countryDropdown;
    public TMP_InputField roomCodeInput;

    public GameObject mainMenuPanel;
    public GameObject lobbyPanel;
    public NetworkManagerLobby networkManager;

    private void Start()
    {
        if (PlayerPrefs.HasKey("PlayerName"))
            nameInput.text = PlayerPrefs.GetString("PlayerName");
    }

    public async void HostGame()
    {
        SavePlayerInfo();

        try
        {
            Allocation allocation = await RelayService.Instance.CreateAllocationAsync(4);
            string joinCode = await RelayService.Instance.GetJoinCodeAsync(allocation.AllocationId);
            Debug.Log("Join Code: " + joinCode);

            // Save the join code if needed (for displaying to players)
            PlayerPrefs.SetString("LastJoinCode", joinCode);

            // Set relay server data
            var transport = (FishyUnityTransport)InstanceFinder.NetworkManager.TransportManager.Transport;
            transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

            // Start server and client
            InstanceFinder.ServerManager.StartConnection();
            InstanceFinder.ClientManager.StartConnection();

            // Show lobby UI
            mainMenuPanel.SetActive(false);
            lobbyPanel.SetActive(true);
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

            // Show lobby UI
            mainMenuPanel.SetActive(false);
            lobbyPanel.SetActive(true);

            // Delay sending the join request until client spawns
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
}
