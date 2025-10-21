using System.Collections;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class ResultsEntryUI : MonoBehaviour
{
    public TMP_Text nameText;
    public TMP_Text scoreText;
    public TMP_Text bailoutText;
    public Slider scoreBar; // set Min=0, Max=1, WholeNumbers=false on prefab

    private Coroutine _co;

    /// <param name="globalMax">shared max across ALL players (>=1)</param>
    public void Bind(string playerName, int startMoney, int bailoutCount, int finalScore, int rankIndex, int globalMax)
    {
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        if (_co != null) StopCoroutine(_co);
        _co = StartCoroutine(CoAnimate(playerName, bailoutCount, finalScore, globalMax));
    }

    private IEnumerator CoAnimate(string playerName, int bailoutCount, int finalScore, int globalMax)
    {
        // labels
        if (nameText) nameText.text = playerName;
        if (bailoutText) bailoutText.text = (bailoutCount > 0) ? $"-{bailoutCount * 100}" : "0";

        // bar baseline
        if (scoreBar)
        {
            scoreBar.minValue = 0f;
            scoreBar.maxValue = 1f;         // IMPORTANT: shared normalization
            scoreBar.wholeNumbers = false;
            scoreBar.value = 0f;
        }
        if (scoreText) scoreText.text = "0";

        yield return null;

        float dur = 1.25f;
        float t = 0f;

        // target normalized fraction vs shared max
        float targetNorm = Mathf.Clamp01(finalScore / (float)globalMax);

        while (t < dur)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Clamp01(t / dur);

            int shown = Mathf.RoundToInt(Mathf.Lerp(0, finalScore, k));
            float norm = Mathf.Lerp(0f, targetNorm, k);

            if (scoreText) scoreText.text = shown.ToString();
            if (scoreBar) scoreBar.value = norm;

            yield return null;
        }

        if (scoreText) scoreText.text = finalScore.ToString();
        if (scoreBar) scoreBar.value = targetNorm;

        _co = null;
    }
}
