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

        // Make sure they start disabled. Server will enable via RpcSetTurnState.
        SetRollInteractable(false);
        SetEndTurnInteractable(false);
    }

    public void BindPawn(PlayerPawn pawn)
    {
        myPawn = pawn;
        // Do not enable anything here; server will broadcast.
        SetRollInteractable(false);
        SetEndTurnInteractable(false);
    }

    private void OnRollDiceClicked()
    {
        if (myPawn != null && myPawn.IsOwner)
        {
            myPawn.OnRollDiceButton();  // calls CmdRollDiceAndMove (server validates CanRoll)
            SetRollInteractable(false); // prevent double-click
        }
    }

    private void OnEndTurnClicked()
    {
        if (myPawn != null && myPawn.IsOwner)
        {
            myPawn.OnEndTurnButton();   // calls CmdEndTurn (server validates CanEndTurn)
            SetEndTurnInteractable(false); // prevent double-click
        }
    }

    public void SetEndTurnInteractable(bool enable) => endTurnButton.interactable = enable;
    public void SetRollInteractable(bool enable)    => rollDiceButton.interactable = enable;

    public void ForceDisableEndTurn() => SetEndTurnInteractable(false);
}