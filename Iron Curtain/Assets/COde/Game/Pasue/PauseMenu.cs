using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Audio;
using UnityEngine.SceneManagement;
using FishNet;
using FishNet.Managing;

public class PauseMenu : MonoBehaviour
{
    public static bool IsOpen { get; private set; }

    [Header("Panels")]
    public GameObject pauseRoot;       
    // public GameObject settingsPanel;   

    //[Header("Audio (optional)")]
    //public AudioMixer mixer;            
    //public Slider masterSlider;
    //public Slider musicSlider;
    //public Slider sfxSlider;

    [Header("Scenes")]
    public string lobbySceneName = "Lobby";

    [Header("Options")]
    public KeyCode toggleKey = KeyCode.Escape;
    public bool pauseTimeScale = true;

    // PlayerPrefs keys
   // const string PP_MASTER = "Audio.Master";
    //const string PP_MUSIC  = "Audio.Music";
    //const string PP_SFX    = "Audio.Sfx";

    void Start()
    {
        
        if (pauseRoot) pauseRoot.SetActive(false);
        // if (settingsPanel) settingsPanel.SetActive(false);
        IsOpen = false;

    
        //InitAudioUI();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (IsOpen) Close();
            else Open();
        }
    }

    /* ====== Open / Close ====== */
    public void Open()
    {
        if (pauseRoot == null) return;
        pauseRoot.SetActive(true);
        // settingsPanel?.SetActive(false);
        IsOpen = true;

        // cursor
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        if (pauseTimeScale) Time.timeScale = 0f;
    }

    public void Close()
    {
        if (pauseRoot == null) return;
        pauseRoot.SetActive(false);
        // settingsPanel?.SetActive(false);
        IsOpen = false;


        if (pauseTimeScale) Time.timeScale = 1f;
    }

    /* ====== Button hooks ====== */
    public void OnResume() => Close();

    //public void OnToggleSettings()
    //{
        //if (settingsPanel == null) return;
        //settingsPanel.SetActive(!settingsPanel.activeSelf);
    //}

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

    /* ====== Audio ====== */
    //void InitAudioUI()
    //{
        // Default volumes if no prefs yet
        //float master = PlayerPrefs.GetFloat(PP_MASTER, 1f);
        //float music  = PlayerPrefs.GetFloat(PP_MUSIC,  1f);
        //float sfx    = PlayerPrefs.GetFloat(PP_SFX,    1f);

        //if (masterSlider) { masterSlider.value = master; masterSlider.onValueChanged.AddListener(SetMasterVolume); }
        //if (musicSlider)  { musicSlider.value  = music;  musicSlider.onValueChanged.AddListener(SetMusicVolume); }
        //if (sfxSlider)    { sfxSlider.value    = sfx;    sfxSlider.onValueChanged.AddListener(SetSfxVolume); }

        //ApplyVolume("MasterVolume", master);
        //ApplyVolume("MusicVolume",  music);
        //ApplyVolume("SFXVolume",    sfx);
    //}

    // Sliders call these
    // public void SetMasterVolume(float v)
    // {
    //     PlayerPrefs.SetFloat(PP_MASTER, v);
    //     ApplyVolume("MasterVolume", v);
    // }
    // public void SetMusicVolume(float v)
    // {
    //     PlayerPrefs.SetFloat(PP_MUSIC, v);
    //     ApplyVolume("MusicVolume", v);
    // }
    // public void SetSfxVolume(float v)
    // {
    //     PlayerPrefs.SetFloat(PP_SFX, v);
    //     ApplyVolume("SFXVolume", v);
    // }

    // void ApplyVolume(string param, float linear01)
    // {
    //     if (mixer == null || string.IsNullOrEmpty(param)) return;

    //     // Convert 0..1 slider to decibels. Clamp small values to -80dB (mute).
    //     float dB = (linear01 <= 0.0001f) ? -80f : Mathf.Log10(Mathf.Clamp01(linear01)) * 20f;
    //     mixer.SetFloat(param, dB);
    // }
}
