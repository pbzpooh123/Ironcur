using UnityEngine;
using System.Linq;

public class PortfolioButtonBinder : MonoBehaviour
{
    private PlayerInfoPanel panel;

    private void Awake()
    {
        panel = GetComponent<PlayerInfoPanel>();
        if (panel == null)
        {
            Debug.LogWarning("[PortfolioButtonBinder] No PlayerInfoPanel on this object.");
            return;
        }

        // Hook Portfolio button
        if (panel.portfolioButton != null)
        {
            panel.portfolioButton.onClick.RemoveAllListeners();
            panel.portfolioButton.onClick.AddListener(OnPortfolioClicked);
        }

        // Hook Stats button
        if (panel.statsButton != null)
        {
            panel.statsButton.onClick.RemoveAllListeners();
            panel.statsButton.onClick.AddListener(OnStatsClicked);
        }
    }

    // -------- Shared pawn resolver --------
    private PlayerPawn ResolvePawn()
    {
        if (panel == null) return null;

        // Prefer CID
        var targetPawn = FindPawnByCid(panel.OwnerCid);

        // Fallback by name
        if (targetPawn == null && !string.IsNullOrWhiteSpace(panel.OwnerName))
            targetPawn = FindPawnByName(panel.OwnerName);

        if (targetPawn == null)
        {
            Debug.LogWarning(
                $"[PlayerPanel] Pawn not found for panel: {panel.OwnerName} (cid={panel.OwnerCid})");
        }

        return targetPawn;
    }

    // -------- Button handlers --------
    private void OnPortfolioClicked()
    {
        var targetPawn = ResolvePawn();
        if (targetPawn == null) return;

        if (PortfolioUI.Instance == null || PortfolioUI.Instance.panel == null)
        {
            Debug.LogWarning("[Portfolio] PortfolioUI missing.");
            return;
        }

        PortfolioUI.Instance.ShowForPawn(targetPawn, panel.OwnerName);
        targetPawn.CmdRequestPortfolioForViewer();
    }

    private void OnStatsClicked()
    {
        var targetPawn = ResolvePawn();
        if (targetPawn == null) return;

        if (StatsUI.Instance == null)
        {
            Debug.LogWarning("[Stats] StatsUI.Instance is null.");
            return;
        }

        StatsUI.Instance.Show(targetPawn);
    }

    // -------- Pawn lookup helpers --------
    private PlayerPawn FindPawnByCid(int cid)
    {
        if (cid < 0) return null;

        var gm = GameManager.Instance;
        if (gm != null && gm.Players != null)
        {
            foreach (var p in gm.Players)
                if (p != null && p.Owner != null && p.Owner.ClientId == cid)
                    return p;
        }

        return GameObject.FindObjectsOfType<PlayerPawn>()
            .FirstOrDefault(p => p.Owner != null && p.Owner.ClientId == cid);
    }

    private PlayerPawn FindPawnByName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return null;

        var gm = GameManager.Instance;
        if (gm != null && gm.Players != null)
        {
            foreach (var p in gm.Players)
                if (p != null && p.playerName.Value == name)
                    return p;
        }

        return GameObject.FindObjectsOfType<PlayerPawn>()
            .FirstOrDefault(p => p.playerName.Value == name);
    }
}
