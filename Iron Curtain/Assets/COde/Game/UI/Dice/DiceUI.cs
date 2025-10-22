using UnityEngine;
using UnityEngine.UI;
using System.Collections;

public class DiceUI : MonoBehaviour
{
    public static DiceUI Instance;

    [Header("Assign 6 sprites in order: index 0 = face 1, ... index 5 = face 6")]
    public Sprite[] faces = new Sprite[6];

    [Header("Assign two Image components for each die")]
    public Image dieA;
    public Image dieB;

    [Header("Optional")]
    public float showSeconds = 1.0f;   // how long the dice stay visible

    void Awake() => Instance = this;

    public void Show(int d1, int d2)
    {
        if (dieA == null || dieB == null || faces == null || faces.Length < 6) return;
        d1 = Mathf.Clamp(d1, 1, 6);
        d2 = Mathf.Clamp(d2, 1, 6);

        dieA.enabled = true;
        dieB.enabled = true;
        dieA.sprite = faces[d1 - 1];
        dieB.sprite = faces[d2 - 1];

        StopAllCoroutines();
        StartCoroutine(AutoHide());
    }

    private IEnumerator AutoHide()
    {
        yield return new WaitForSeconds(showSeconds);
        if (dieA != null) dieA.enabled = false;
        if (dieB != null) dieB.enabled = false;
    }
}
