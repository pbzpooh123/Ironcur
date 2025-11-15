using TMPro;
using UnityEngine;

public class TMPFontReplacer : MonoBehaviour
{
    public TMP_FontAsset newFont;

    void Awake()
    {
        if (newFont == null)
        {
            Debug.LogWarning("TMPFontReplacer: No font assigned.");
            return;
        }

        // UI text
        foreach (var tmp in FindObjectsOfType<TextMeshProUGUI>(true))
        {
            tmp.font = newFont;
        }

        // World-space / 3D text
        foreach (var tmp in FindObjectsOfType<TextMeshPro>(true))
        {
            tmp.font = newFont;
        }

        Debug.Log("TMPFontReplacer: Replaced fonts in scene.");
    }
}
