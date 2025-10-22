// HotkeyMultiplierDebug.cs
using UnityEngine;

public class HotkeyMultiplierDebug : MonoBehaviour
{
    [Header("Effect")]
    [Tooltip("Payout multiplier to apply (e.g., 1.5 = +50%)")]
    public float multiplier = 1.5f;

    [Tooltip("How many rounds the multiplier lasts")]
    public int durationRounds = 2;

    [Header("Target")]
    [Tooltip("If true, apply to ALL players. If false, only the pressing player.")]
    public bool applyToAll = true;

    void Update()
    {
        // Only trigger on key down once
        if (Input.GetKeyDown(KeyCode.Keypad2))
        {
            if (DebugServerBridge.Instance == null)
            {
                Debug.LogWarning("[HotkeyMultiplierDebug] DebugServerBridge not found in scene.");
                return;
            }

            if (applyToAll)
                DebugServerBridge.Instance.CmdBoostAllPayouts(multiplier, Mathf.Max(1, durationRounds));
            else
                DebugServerBridge.Instance.CmdBoostMine(multiplier, Mathf.Max(1, durationRounds));
        }
    }
}
