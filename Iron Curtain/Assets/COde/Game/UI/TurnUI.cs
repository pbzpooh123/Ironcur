using UnityEngine;
using UnityEngine.UI;

public class TurnUI : MonoBehaviour
{
    public Button rollDiceButton;
    public Button endTurnButton;

    private PlayerPawn myPawn;

    private void Start()
    {
        rollDiceButton.onClick.AddListener(OnRollDiceClicked);
        endTurnButton.onClick.AddListener(OnEndTurnClicked);
    }

    public void BindPawn(PlayerPawn pawn)
    {
        myPawn = pawn;
        rollDiceButton.interactable = false;
        endTurnButton.interactable = false;
    }

    private void OnRollDiceClicked()
    {
        if (myPawn != null && myPawn.IsOwner)
        {
            myPawn.OnRollDiceButton();
        }
    }

    private void OnEndTurnClicked()
    {
        if (myPawn != null && myPawn.IsOwner)
        {
            myPawn.OnEndTurnButton();
        }
    }

    public void SetEndTurnInteractable(bool enable)
    {
        endTurnButton.interactable = enable;
    }

    public void SetRollInteractable(bool enable)
    {
        rollDiceButton.interactable = enable;
    }

    public void ForceDisableEndTurn()
    {
        SetEndTurnInteractable(false);
    }
}