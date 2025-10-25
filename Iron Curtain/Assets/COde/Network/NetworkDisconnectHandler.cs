using UnityEngine;
using UnityEngine.SceneManagement;
using FishNet;
using FishNet.Transporting;
using FishNet.Managing.Client;

public class NetworkDisconnectHandler : MonoBehaviour
{
    [Tooltip("Scene to load when connection to host is lost.")]
    public string mainMenuSceneName = "Lobbby";

    private void OnEnable()
    {
        var cm = InstanceFinder.ClientManager;
        if (cm != null) cm.OnClientConnectionState += HandleClientConnectionState;
    }

    private void OnDisable()
    {
        var cm = InstanceFinder.ClientManager;
        if (cm != null) cm.OnClientConnectionState -= HandleClientConnectionState;
    }

    private void HandleClientConnectionState(ClientConnectionStateArgs args)
    {
        // Host closed or connection dropped → go to menu.
        if (args.ConnectionState == LocalConnectionState.Stopped ||
            args.ConnectionState == LocalConnectionState.Stopping)
        {
            SafeReturnToMenu("Connection to host lost.");
        }
    }
    public void SafeReturnToMenu(string reason = "")
    {
        Time.timeScale = 1f;
        if (!string.IsNullOrEmpty(mainMenuSceneName))
            SceneManager.LoadScene(mainMenuSceneName);
    }
}
