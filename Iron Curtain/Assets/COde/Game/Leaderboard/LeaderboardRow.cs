using TMPro;
using UnityEngine;

public class LeaderboardRow : MonoBehaviour
{
    public TMP_Text rankText;
    public TMP_Text nameText;
    public TMP_Text scoreText;

    public void Bind(int rank, string name, long score)
    {
        if (rankText) rankText.text = rank.ToString();
        if (nameText) nameText.text = name;
        if (scoreText) scoreText.text = score.ToString();
    }
}
