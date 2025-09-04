using UnityEngine;
using UnityEngine.UI;
using FishNet.Object;

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

    // Called by PlayerPawn when it becomes my turn
    public void BindPawn(PlayerPawn pawn)
    {
        myPawn = pawn;
        rollDiceButton.interactable = true;
        endTurnButton.interactable = false;
    }

    private void OnRollDiceClicked()
    {
        if (myPawn != null && myPawn.IsOwner)
        {
            myPawn.CmdRollDiceAndMove();   // client → server
            rollDiceButton.interactable = false;
            endTurnButton.interactable = true;
        }
    }

    private void OnEndTurnClicked()
    {
        if (myPawn != null && myPawn.IsOwner)
        {
            myPawn.CmdEndTurn();          // client → server
            endTurnButton.interactable = false;
        }
    }
}
