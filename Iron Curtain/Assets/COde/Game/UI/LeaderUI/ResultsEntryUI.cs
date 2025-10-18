// ResultsEntryUI.cs
using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ResultsEntryUI : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text scoreText;
    public TMP_Text bailoutText;
    public Slider scoreBar;

    public void Bind(string playerName, int startMoney, int bailoutCount, int finalScore, int rankIndex)
    {
        // Always ensure row is active before any coroutine
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        StopAllCoroutines();
        StartCoroutine(CoAnimate(playerName, startMoney, bailoutCount, finalScore));
    }

    private IEnumerator CoAnimate(string playerName, int startMoney, int bailoutCount, int finalScore)
    {
        // Fill static labels first
        if (nameText) nameText.text = playerName;
        if (bailoutText) bailoutText.text = (bailoutCount > 0) ? $"-{bailoutCount * 100}" : "0";
        if (scoreBar) scoreBar.value = 0f;
        if (scoreText) scoreText.text = "0";

        // Wait a frame (just in case layout is catching up)
        yield return null;

        // Animate up to finalScore
        float dur = 1.25f;
        float t = 0f;
        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);
            int shown = Mathf.RoundToInt(Mathf.Lerp(0, finalScore, k));
            if (scoreText) scoreText.text = shown.ToString();
            if (scoreBar) scoreBar.value = (finalScore <= 0) ? 0f : (shown / (float)finalScore);
            yield return null;
        }

        if (scoreText) scoreText.text = finalScore.ToString();
        if (scoreBar) scoreBar.value = 1f;
    }
}
