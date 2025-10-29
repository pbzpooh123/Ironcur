using System.Collections;
using UnityEngine;
using UnityEngine.UI;

public class DiceUI : MonoBehaviour
{
    public static DiceUI Instance;

    [Header("UI")]
    public Image die1Image;
    public Image die2Image;
    public Sprite[] faceSprites; // 6 sprites, index 0..5

    [Header("Timing")]
    [Tooltip("Real-time duration regardless of timeScale/FPS")]
    public float rollDurationSeconds = 1.2f;
    [Tooltip("How fast we shuffle random faces during the roll")]
    public float shuffleHz = 18f;

    private Coroutine _co;

    void Awake() => Instance = this;

    public void ShowDiceRollingWithCallback(int d1, int d2, System.Action onDone)
    {
        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoRollUnscaled(d1, d2, onDone));
    }

    private IEnumerator CoRollUnscaled(int d1, int d2, System.Action onDone)
    {
        // Safety: guard sprite arrays
        int Safe(int v) => Mathf.Clamp(v - 1, 0, (faceSprites?.Length ?? 1) - 1);

        // Shuffle faces in REAL time
        float t = 0f;
        float shuffleStep = (shuffleHz > 0f) ? 1f / shuffleHz : 0.05f;
        float nextShuffleAt = 0f;

        // Optional: stamp start time for debugging
        float startRt = Time.realtimeSinceStartup;

        while (t < rollDurationSeconds)
        {
            // Shuffle at fixed real-time cadence
            if (t >= nextShuffleAt)
            {
                if (die1Image && faceSprites != null && faceSprites.Length > 0)
                    die1Image.sprite = faceSprites[Random.Range(0, faceSprites.Length)];
                if (die2Image && faceSprites != null && faceSprites.Length > 0)
                    die2Image.sprite = faceSprites[Random.Range(0, faceSprites.Length)];

                nextShuffleAt += shuffleStep;
            }

            t += Time.unscaledDeltaTime;   // ← unscaled time
            yield return null;
        }

        // Snap to final faces
        if (die1Image && faceSprites != null && faceSprites.Length > 0)
            die1Image.sprite = faceSprites[Safe(d1)];
        if (die2Image && faceSprites != null && faceSprites.Length > 0)
            die2Image.sprite = faceSprites[Safe(d2)];

        yield return new WaitForSecondsRealtime(0.15f);

        onDone?.Invoke();
        _co = null;
    }
}
