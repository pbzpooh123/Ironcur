using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DiceUI : MonoBehaviour
{
    public static DiceUI Instance;

    [Header("Dice UI Elements")]
    public Image diceLeft;
    public Image diceRight;
    public Sprite[] diceFaces; // Sprite ของเต๋า 1–6
    public Text resultText;

    private void Awake()
    {
        Instance = this;
        gameObject.SetActive(false);
    }

    public void ShowDiceRollingWithCallback(int final1, int final2, System.Action onComplete)
    {
        gameObject.SetActive(true);
        StartCoroutine(AnimateDiceWithCallback(final1, final2, onComplete));
    }

    private IEnumerator AnimateDiceWithCallback(int final1, int final2, System.Action onComplete)
    {
        float rollTime = 0.2f;
        float t = 0f;

        while (t < rollTime)
        {
            int r1 = Random.Range(1, 7);
            int r2 = Random.Range(1, 7);
            diceLeft.sprite = diceFaces[r1 - 1];
            diceRight.sprite = diceFaces[r2 - 1];
            t += Time.deltaTime;
            yield return new WaitForSeconds(0.05f);
        }

        diceLeft.sprite = diceFaces[Mathf.Clamp(final1 - 1, 0, 5)];
        diceRight.sprite = diceFaces[Mathf.Clamp(final2 - 1, 0, 5)];
        if (resultText != null)
            resultText.text = $"Total: {final1 + final2}";

        yield return new WaitForSeconds(2f);

        gameObject.SetActive(false);

        // 🔥 เรียก Callback เมื่อจบอนิเมชัน
        onComplete?.Invoke();
    }
    public void Show(int final1, int final2)
    {
        // เรียกใช้เมธอดเดิม โดยส่ง null เป็น callback
        ShowDiceRollingWithCallback(final1, final2, null);
    }

   
}