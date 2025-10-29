using UnityEngine;
using TMPro;

public class PauseUI : MonoBehaviour
{
    public static PauseUI Instance;
    public GameObject overlay;      // assign a dark panel
    public TMP_Text statusText;     // "Paused by host"

    void Awake() => Instance = this;

    public void SetPaused(bool paused)
    {
        if (overlay) overlay.SetActive(paused);
        if (statusText) statusText.text = paused ? "Paused" : "";
    }
}
