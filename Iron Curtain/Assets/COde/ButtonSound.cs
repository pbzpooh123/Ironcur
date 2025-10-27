using UnityEngine;
using UnityEngine.UI;

public class ButtonSound : MonoBehaviour
{
    public AudioSource audioSource;   // ตัวเล่นเสียง
    public AudioClip clickSound;      // ไฟล์เสียงตอนกด

    void Start()
    {
        // หาปุ่มที่สคริปต์นี้ติดอยู่ แล้วเพิ่ม event ให้ตอนคลิก
        Button btn = GetComponent<Button>();
        if (btn != null)
        {
            btn.onClick.AddListener(PlayClickSound);
        }
    }

    void PlayClickSound()
    {
        if (audioSource != null && clickSound != null)
        {
            audioSource.PlayOneShot(clickSound);
        }
    }
}
