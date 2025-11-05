using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using FishNet;
using FishNet.Managing;

public class PauseMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [Header("Root + Sound Panel")]
    public GameObject pauseRoot;
    public GameObject soundPanel;

    [Header("Sound Sliders (inside soundPanel)")]
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider sfxSlider;

    [Header("Scenes")]
    public string lobbySceneName = "Lobby";

    [Header("Options")]
    public KeyCode toggleKey = KeyCode.Escape;
    public bool pauseTimeScale = true;

    void Start()
    {
        if (pauseRoot)  pauseRoot.SetActive(false);
        if (soundPanel) soundPanel.SetActive(false);
        IsOpen = false;

        if (masterSlider) masterSlider.value = PlayerPrefs.GetFloat("MasterVolume", masterSlider.value);
        if (sfxSlider)    sfxSlider.value    = PlayerPrefs.GetFloat("sfxVolume",     sfxSlider.value);

        if (masterSlider) masterSlider.onValueChanged.AddListener(v =>
        {
            if (VolumeSettings.Instance && VolumeSettings.Instance.enabled)
            {
                if (VolumeSettings.Instance && VolumeSettings.Instance.GetComponent<VolumeSettings>() != null)
                {
                    // push to the real UI and reuse your SetMasterVolume()
                    if (VolumeSettings.Instance && VolumeSettings.Instance.GetComponent<VolumeSettings>() != null)
                    {
                        VolumeSettings.Instance.SendMessage("SetMasterVolumeViaExternal", v, SendMessageOptions.DontRequireReceiver);
                    }
                }
            }
        });

        if (sfxSlider) sfxSlider.onValueChanged.AddListener(v =>
        {
            if (VolumeSettings.Instance)
                VolumeSettings.Instance.SendMessage("SetSfxVolumeViaExternal", v, SendMessageOptions.DontRequireReceiver);
        });
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (!IsOpen) Open();
            else
            {
                // if in sound sub-panel, go back to main; else close
                if (soundPanel != null && soundPanel.activeSelf) OnBackFromSound();
                else Close();
            }
        }
    }

    public void Open()
    {
        if (!pauseRoot) return;
        pauseRoot.SetActive(true);
        if (soundPanel) soundPanel.SetActive(false);
        IsOpen = true;

        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;
        if (pauseTimeScale) Time.timeScale = 0f;
    }

    public void Close()
    {
        if (!pauseRoot) return;
        pauseRoot.SetActive(false);
        if (soundPanel) soundPanel.SetActive(false);
        IsOpen = false;

        if (pauseTimeScale) Time.timeScale = 1f;
    }

    public void OnResume() => Close();

    public void OnOpenSound()
    {
        if (soundPanel) soundPanel.SetActive(true);
        VolumeSettings.Instance.SetSfxVolumeViaExternal(sfxSlider.value);
        VolumeSettings.Instance.SetMasterVolumeViaExternal(masterSlider.value);
    }

  
    public void OnBackFromSound()
    {
        Debug.Log($"[PauseMenu] Back clicked. Will hide: {(soundPanel ? soundPanel.name : "NULL")}, id={GetInstanceID()}");
        if (soundPanel) soundPanel.SetActive(false);
    }

    public void OnReturnToLobby()
    {
        if (pauseTimeScale) Time.timeScale = 1f;

        var nm = InstanceFinder.NetworkManager;
        if (nm != null)
        {
            if (nm.ClientManager != null && nm.ClientManager.Started)
                nm.ClientManager.StopConnection();
            if (nm.ServerManager != null && nm.ServerManager.Started)
                nm.ServerManager.StopConnection(true);
        }

        if (!string.IsNullOrWhiteSpace(lobbySceneName))
            SceneManager.LoadScene(lobbySceneName);
        else
            Debug.LogError("[PauseMenu] Lobby scene name not set.");
    }

    public void OnQuit()
    {
        if (pauseTimeScale) Time.timeScale = 1f;
        Application.Quit();
    }
}
