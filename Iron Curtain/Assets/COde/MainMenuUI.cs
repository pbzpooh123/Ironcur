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
    public LobbyUI lobbyUI; // 👈 Reference to LobbyUI

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
            NetworkManagerLobby.Instance.roomCode = joinCode; // ✅ Now this is the ONLY room code
            Debug.Log("Join Code: " + joinCode);

            PlayerPrefs.SetString("LastJoinCode", joinCode); // optional

            // Relay transport setup
            var transport = (FishyUnityTransport)InstanceFinder.NetworkManager.TransportManager.Transport;
            transport.SetRelayServerData(new RelayServerData(allocation, "dtls"));

            // Start the network
            InstanceFinder.ServerManager.StartConnection();
            InstanceFinder.ClientManager.StartConnection();

            // Activate Lobby
            mainMenuPanel.SetActive(false);
            lobbyPanel.SetActive(true);

            // 👉 Set the code in the Lobby UI right after showing it
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

            // 👉 Set join code in Lobby UI
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
}
