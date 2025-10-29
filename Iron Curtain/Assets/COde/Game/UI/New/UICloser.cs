// UICloser.cs
using UnityEngine;

public static class UICloser
{
    public static void CloseForPhaseClient(TurnPhase phase)
    {
        switch (phase)
        {
            case TurnPhase.Review:
                if (ReviewUI.Instance != null) ReviewUI.Instance.Hide();
                break;

            case TurnPhase.Proposal:
                if (ProposalUI.Instance != null) ProposalUI.Instance.Hide();
                break;

            case TurnPhase.TileEventPending:
                if (EventUI.Instance != null) EventUI.Instance.OnSideOk();
                InvestmentUI.Instance?.CloseAndContinue(); // optional, if you have it
                break;
                
            case TurnPhase.MainEvent:
                EventUI.Instance?.OnOk(); // or your OK handler
                break;

            case TurnPhase.Rolling:
                // nothing to close
                break;

            case TurnPhase.EndReady:
                // nothing extra; TurnUI buttons are already locked
                break;
        }
    }
}
