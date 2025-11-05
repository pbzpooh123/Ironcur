using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

public class VolumeSettings : MonoBehaviour
{
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private Slider masterSlider;
    [SerializeField] private Slider sfxSlider;

    public static VolumeSettings Instance;

    void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        if (PlayerPrefs.HasKey("MasterVolume"))
        {
            LoadVolume();
        }
        else
        {
            SetMasterVolume();
        }
        if (PlayerPrefs.HasKey("sfxVolume"))
        {
            LoadsfxVolume();
        }
        else
        {
            SetsfxVolume();
        }
    }

    public void SetMasterVolume()
    {
        float volume = masterSlider.value;
        audioMixer.SetFloat("BGm", Mathf.Log10(volume) * 20);
        PlayerPrefs.SetFloat("MasterVolume", volume);
    }

    private void LoadVolume()
    {
        masterSlider.value = PlayerPrefs.GetFloat("MasterVolume");
        SetMasterVolume();
    }

    public void SetsfxVolume()
    {
        float volume = sfxSlider.value;
        audioMixer.SetFloat("SFX", Mathf.Log10(volume) * 20);
        PlayerPrefs.SetFloat("sfxVolume", volume);
    }

    private void LoadsfxVolume()
    {
        sfxSlider.value = PlayerPrefs.GetFloat("sfxVolume");
        SetsfxVolume();
    }

    public void SetMasterVolumeViaExternal(float value)
    {
        if (masterSlider != null) masterSlider.value = value;
        SetMasterVolume(); 
    }

    public void SetSfxVolumeViaExternal(float value)
    {
        if (sfxSlider != null) sfxSlider.value = value;
        SetsfxVolume(); 
    }
}
