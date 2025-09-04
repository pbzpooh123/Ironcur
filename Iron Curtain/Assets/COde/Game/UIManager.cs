using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    public GameObject rollButton;
    public GameObject endTurnButton;

    private void Awake() => Instance = this;

    public void EnableTurnUI(bool showRoll, bool showEndTurn)
    {
        rollButton.SetActive(showRoll);
        endTurnButton.SetActive(showEndTurn);
    }
}