using UnityEngine;
using TMPro;

public class PauseUI : MonoBehaviour
{
    public static PauseUI Instance;

    public GameObject overlay;     
    public TMP_Text statusText;     

    void Awake()
    {
        Instance = this;
        SetPaused(false, null);
    }

    public void SetPaused(bool paused, string pausedByName)
    {
        if (overlay) overlay.SetActive(paused);

        if (statusText)
        {
            if (!paused)
            {
                statusText.text = "";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(pausedByName))
                    statusText.text = "หยุดเกมชั่วคราว";
                else
                    statusText.text = $"หยุดเกมโดย {pausedByName}";
            }
        }

        Time.timeScale = paused ? 0f : 1f;
    }
}
