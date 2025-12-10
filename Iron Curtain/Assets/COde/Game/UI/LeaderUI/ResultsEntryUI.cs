using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using FishNet.Object;

public class ResultsEntryUI : MonoBehaviour
{
    [Header("Basic UI")]
    public TMP_Text nameText;
    public TMP_Text pointsText;          // แต้มรวมสุดท้าย

    [Header("Title & Summary")]
    public TMP_Text titleText;           // ฉายา เช่น "นักลงทุนเชิงรุก"
    public TMP_Text summaryText;         // ข้อความอธิบายสั้น ๆ พฤติกรรมการลงทุน

    [Header("Awards (per player)")]
    public TMP_Text awardsText;          // แสดงรางวัลพิเศษ เช่น "เจ้าพ่อพอร์ตหุ้น (+25 แต้ม)"

    [Header("Player Portrait")]
    public Image playerIcon;             // sprite จาก PlayerPawn

    [Header("Drop Icon")]
    public Image pointIcon;              // ใช้ทำอนิเมชันดรอปแต้ม
    public RectTransform dropTarget;     // จุดที่ icon ตกลงมา
    public float dropOffsetY = 150f;

    [Header("Sprites + Colors")]
    public Sprite pointSprite;           // gold coin for money
    public Sprite badgeSprite;           // badge/trophy for awards
    public Color basePointColor    = Color.white;
    public Color bailoutPointColor = Color.red;
    public Color awardPointColor   = Color.yellow;

    private Coroutine _co;
    private int _scoreAnim;

    /// <summary>
    /// moneyRaw: เงินสดสุดท้าย (จาก server)
    /// bailoutCount: จำนวน bailout marks
    /// finalPoints: แต้มสุดท้ายที่ server ส่งมา (เงิน→แต้ม + โบนัสจาก awards)
    /// title: ฉายาประเภทนักลงทุน
    /// decisionSummary: ข้อความอธิบายพฤติกรรมการตัดสินใจ (ไม่มี sector)
    /// awardsSummary: รางวัลที่ผู้เล่นคนนี้ได้ (อาจเป็นหลายบรรทัด หรือว่าง)
    /// </summary>
    public void Bind(
        string playerName,
        int moneyRaw,
        int bailoutCount,
        int finalPoints,
        string title,
        string decisionSummary,
        string awardsSummary
    )
    {
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        // ตั้ง portrait ตามชื่อ
        SetupPlayerPortrait(playerName);

        // ตั้งข้อความพื้นฐาน
        if (nameText)    nameText.text    = playerName;
        if (titleText)   titleText.text   = title;
        if (summaryText) summaryText.text = decisionSummary;

        // ตั้งข้อความรางวัลต่อคน
        if (awardsText != null)
        {
            if (string.IsNullOrWhiteSpace(awardsSummary))
                awardsText.text = "";
            else
                awardsText.text = awardsSummary;
        }

        if (_co != null)
            StopCoroutine(_co);

        _co = StartCoroutine(CoAnimate(playerName, moneyRaw, bailoutCount, finalPoints));
    }

    private void SetupPlayerPortrait(string playerName)
    {
        if (playerIcon == null) return;
        StartCoroutine(CoSetupPortrait(playerName));
    }

    private IEnumerator CoSetupPortrait(string playerName)
    {
        float timeout = 3f;
        string targetName = playerName?.Trim() ?? "";

        while (timeout > 0f)
        {
            var lib = CharacterLibrary.Instance;
            if (lib != null)
            {
                PlayerPawn found = null;

                if (GameManager.Instance != null && GameManager.Instance.Players != null &&
                    GameManager.Instance.Players.Count > 0)
                {
                    foreach (var p in GameManager.Instance.Players)
                    {
                        if (p == null) continue;
                        if (string.Equals(p.playerName.Value?.Trim(), targetName,
                                          System.StringComparison.OrdinalIgnoreCase))
                        {
                            found = p;
                            break;
                        }
                    }
                }

                if (found == null)
                {
                    foreach (var p in FindObjectsOfType<PlayerPawn>())
                    {
                        if (p == null) continue;
                        if (string.Equals(p.playerName.Value?.Trim(), targetName,
                                          System.StringComparison.OrdinalIgnoreCase))
                        {
                            found = p;
                            break;
                        }
                    }
                }

                if (found != null)
                {
                    Debug.Log($"[ResultsEntryUI] Setting up portrait for {targetName} (colorIndex={found.colorIndex.Value})");

                    var sprite = lib.GetSprite(found.colorIndex.Value);
                    if (sprite != null)
                        playerIcon.sprite = sprite;

                    playerIcon.color = PlayerColors.GetOr(Color.white, found.colorIndex.Value);
                    yield break;
                }
            }

            timeout -= Time.unscaledDeltaTime;
            yield return null;
        }

        Debug.LogWarning($"[ResultsEntryUI] Failed to find pawn for '{playerName}' on this client.");
    }

    private IEnumerator CoAnimate(string playerName, int moneyRaw, int bailoutCount, int finalPoints)
    {
        if (pointsText) pointsText.text = "แต้มรวม: 0";

        yield return null;

        int rawMoneyPoints       = Mathf.Max(0, moneyRaw / 100);   // 100$ = 1 point
        int bailoutPenaltyPoints = Mathf.Max(0, bailoutCount);     // each bailout = -1 point
        int baseAfterPenalty     = Mathf.Max(0, rawMoneyPoints - bailoutPenaltyPoints);
        int awardBonusPoints     = Mathf.Max(0, finalPoints - baseAfterPenalty);

        int currentScore = 0;
        _scoreAnim = currentScore;

        // Stage 1: money points (gold)
        if (rawMoneyPoints > 0)
        {
            _scoreAnim = currentScore;
            yield return DropStage(rawMoneyPoints, pointSprite, basePointColor, 0.6f);
            currentScore = _scoreAnim;
        }

        // Stage 2: bailout penalty (red, subtract)
        if (bailoutPenaltyPoints > 0)
        {
            _scoreAnim = currentScore;
            yield return DropStage(-bailoutPenaltyPoints, pointSprite, bailoutPointColor, 0.6f);
            currentScore = _scoreAnim;
        }

        // Stage 3: award bonus (badge)
        if (awardBonusPoints > 0)
        {
            Sprite s = (badgeSprite != null) ? badgeSprite : pointSprite;
            _scoreAnim = currentScore;
            yield return DropStage(awardBonusPoints, s, awardPointColor, 0.6f);
            currentScore = _scoreAnim;
        }

        currentScore = finalPoints;
        _scoreAnim   = finalPoints;
        if (pointsText) pointsText.text = $"แต้มรวม: {currentScore}";

        _co = null;
    }

    private IEnumerator DropStage(int delta, Sprite sprite, Color color, float duration)
    {
        if (delta == 0)
            yield break;

        if (pointIcon == null || dropTarget == null)
        {
            _scoreAnim += delta;
            if (pointsText) pointsText.text = $"แต้มรวม: {_scoreAnim}";
            yield break;
        }

        RectTransform rt = pointIcon.rectTransform;
        rt.gameObject.SetActive(true);

        if (sprite != null)
            pointIcon.sprite = sprite;
        pointIcon.color = color;

        Vector2 end   = dropTarget.anchoredPosition;
        Vector2 start = end + new Vector2(0f, dropOffsetY);
        rt.anchoredPosition = start;

        int from = _scoreAnim;
        int to   = _scoreAnim + delta;

        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k     = Mathf.Clamp01(t / duration);
            float eased = k * k * (3f - 2f * k); // SmoothStep

            rt.anchoredPosition = Vector2.Lerp(start, end, eased);

            int shown  = Mathf.RoundToInt(Mathf.Lerp(from, to, eased));
            _scoreAnim = shown;
            if (pointsText) pointsText.text = $"แต้มรวม: {shown}";

            yield return null;
        }

        rt.anchoredPosition = end;
        _scoreAnim = to;
        if (pointsText) pointsText.text = $"แต้มรวม: {to}";

        yield return new WaitForSeconds(0.1f);
    }
}
