using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class ResultsEntryUI : MonoBehaviour
{
    [Header("Basic UI")]
    public TMP_Text nameText;
    public TMP_Text pointsText;          // single label: final total points

    [Header("Player Portrait")]
    public Image playerIcon;             // sprite from PlayerPawn

    [Header("Drop Icon")]
    public Image pointIcon;              // we move & recolor this
    public RectTransform dropTarget;     // where it should land (over/near player)
    public float dropOffsetY = 150f;     // how high the icon starts above target

    [Header("Sprites + Colors")]
    public Sprite pointSprite;           // gold coin for money
    public Sprite badgeSprite;           // badge/trophy for awards
    public Color basePointColor    = Color.white;
    public Color bailoutPointColor = Color.red;
    public Color awardPointColor   = Color.yellow;

    private Coroutine _co;
    private int _scoreAnim;              // shared score during animation

    /// <summary>
    /// moneyRaw: เงินสดสุดท้าย (จาก server)
    /// bailoutCount: จำนวน bailout marks
    /// finalPoints: แต้มสุดท้ายที่ server ส่งมา (เงิน→แต้ม + โบนัสจาก awards)
    /// </summary>
    public void Bind(string playerName, int moneyRaw, int bailoutCount, int finalPoints)
    {
        if (!gameObject.activeInHierarchy)
            gameObject.SetActive(true);

        SetupPlayerPortrait(playerName);

        if (_co != null)
            StopCoroutine(_co);

        _co = StartCoroutine(CoAnimate(playerName, moneyRaw, bailoutCount, finalPoints));
    }

    private void SetupPlayerPortrait(string playerName)
    {
        if (playerIcon == null) return;
        if (GameManager.Instance == null) return;

        PlayerPawn pawn = null;

        foreach (var p in GameManager.Instance.Players)
        {
            if (p == null) continue;
            if (p.playerName.Value == playerName)
            {
                pawn = p;
                break;
            }
        }

        if (pawn == null) return;

        var lib = CharacterLibrary.Instance;
        if (lib != null)
        {
            var sprite = lib.GetSprite(pawn.colorIndex.Value);
            if (sprite != null)
                playerIcon.sprite = sprite;
        }

        playerIcon.color = PlayerColors.GetOr(Color.white, pawn.colorIndex.Value);
    }

    private IEnumerator CoAnimate(string playerName, int moneyRaw, int bailoutCount, int finalPoints)
    {
        if (nameText)   nameText.text   = playerName;
        if (pointsText) pointsText.text = "แต้มรวม: 0";

        yield return null;

        // 1) Decompose points
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
            yield return DropStage(
                rawMoneyPoints,
                pointSprite,
                basePointColor,
                0.6f
            );
            currentScore = _scoreAnim;
        }

        // Stage 2: bailout penalty (red, subtract)
        if (bailoutPenaltyPoints > 0)
        {
            _scoreAnim = currentScore;
            yield return DropStage(
                -bailoutPenaltyPoints,
                pointSprite,
                bailoutPointColor,
                0.6f
            );
            currentScore = _scoreAnim;
        }

        // Stage 3: award bonus (badge)
        if (awardBonusPoints > 0)
        {
            Sprite s = (badgeSprite != null) ? badgeSprite : pointSprite;
            _scoreAnim = currentScore;
            yield return DropStage(
                awardBonusPoints,
                s,
                awardPointColor,
                0.6f
            );
            currentScore = _scoreAnim;
        }

        // Snap to authoritative value
        currentScore = finalPoints;
        _scoreAnim   = finalPoints;
        if (pointsText) pointsText.text = $"แต้มรวม: {currentScore}";

        _co = null;
    }

    private IEnumerator DropStage(
        int delta,
        Sprite sprite,
        Color color,
        float duration
    )
    {
        if (delta == 0)
            yield break;

        if (pointIcon == null || dropTarget == null)
        {
            // no animation object, just snap score
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

            int shown   = Mathf.RoundToInt(Mathf.Lerp(from, to, eased));
            _scoreAnim  = shown;
            if (pointsText) pointsText.text = $"แต้มรวม: {shown}";

            yield return null;
        }

        rt.anchoredPosition = end;
        _scoreAnim = to;
        if (pointsText) pointsText.text = $"แต้มรวม: {to}";

        yield return new WaitForSeconds(0.1f);
    }
}
