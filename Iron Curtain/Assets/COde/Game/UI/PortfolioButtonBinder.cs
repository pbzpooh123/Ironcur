using UnityEngine;
using UnityEngine.UI;
using System.Linq;

public class PortfolioButtonBinder : MonoBehaviour
{
    private PlayerInfoPanel panel;

    private void Awake()
    {
        panel = GetComponent<PlayerInfoPanel>();
        if (panel != null && panel.portfolioButton != null)
        {
            panel.portfolioButton.onClick.RemoveAllListeners();
            panel.portfolioButton.onClick.AddListener(OnPortfolioClicked);
        }
    }

    private void OnPortfolioClicked()
    {
        if (panel == null) return;

        var targetPawn = FindPawnByCid(panel.OwnerCid);
        if (targetPawn == null && !string.IsNullOrWhiteSpace(panel.OwnerName))
            targetPawn = FindPawnByName(panel.OwnerName);

        if (targetPawn == null)
        {
            Debug.LogWarning($"[Portfolio] Pawn not found for panel: {panel.OwnerName} (cid={panel.OwnerCid})");
            return;
        }

        if (PortfolioUI.Instance == null || PortfolioUI.Instance.panel == null)
        {
            Debug.LogWarning("[Portfolio] PortfolioUI missing.");
            return;
        }

        PortfolioUI.Instance.ShowForPawn(targetPawn, panel.OwnerName);
        // request fresh snapshot to THIS viewer
        targetPawn.CmdRequestPortfolioForViewer();
    }

    private PlayerPawn FindPawnByCid(int cid)
    {
        if (cid < 0) return null;
        var gm = GameManager.Instance;
        if (gm != null && gm.Players != null)
            foreach (var p in gm.Players)
                if (p != null && p.Owner != null && p.Owner.ClientId == cid)
                    return p;

        return GameObject.FindObjectsOfType<PlayerPawn>()
            .FirstOrDefault(p => p.Owner != null && p.Owner.ClientId == cid);
    }

    private PlayerPawn FindPawnByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var gm = GameManager.Instance;
        if (gm != null && gm.Players != null)
            foreach (var p in gm.Players)
                if (p != null && p.playerName.Value == name)
                    return p;

        return GameObject.FindObjectsOfType<PlayerPawn>()
            .FirstOrDefault(p => p.playerName.Value == name);
    }
}
