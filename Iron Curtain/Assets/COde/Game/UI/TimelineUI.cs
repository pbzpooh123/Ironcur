using UnityEngine;
using TMPro;

public class TimelineUI : MonoBehaviour
{
    public static TimelineUI Instance;
    public TMP_Text label;   // assign in Inspector

    void Awake() => Instance = this;

    public void SetTimeline(string timelineName)
    {
        if (label != null)
            label.text = $"Timeline: {timelineName}";
    }
}