using UnityEngine;
using System.Collections.Generic;

public class PawnNameTagManager : MonoBehaviour
{
    public static PawnNameTagManager Instance;

    [Header("Setup")]
    public PawnNameTag nameTagPrefab;
    public RectTransform nameTagParent;   // under a Canvas (Screen Space)

    private readonly Dictionary<PlayerPawn, PawnNameTag> _tags = new();

    private void Awake()
    {
        Instance = this;
        Debug.Log("[PawnNameTagManager] Awake. prefab=" +
                  (nameTagPrefab ? nameTagPrefab.name : "null") +
                  ", parent=" + (nameTagParent ? nameTagParent.name : "null"));
    }

    private void Start()
    {
        // When this scene loads, attach to all existing pawns
        AttachExistingPawns();
    }

    private void AttachExistingPawns()
    {
        var pawns = FindObjectsOfType<PlayerPawn>();
        Debug.Log($"[PawnNameTagManager] AttachExistingPawns, found {pawns.Length} pawns.");
        foreach (var p in pawns)
        {
            CreateForPawn(p);
        }
    }

    public void CreateForPawn(PlayerPawn pawn)
    {
        if (pawn == null) return;

        if (nameTagPrefab == null || nameTagParent == null)
        {
            Debug.LogWarning("[PawnNameTagManager] Missing prefab or parent, cannot create tag.");
            return;
        }

        if (_tags.ContainsKey(pawn))
        {
            // already created
            return;
        }

        var tag = Instantiate(nameTagPrefab, nameTagParent);
        tag.name = $"NameTag_{pawn.Owner?.ClientId ?? -1}";
        tag.Bind(pawn);

        _tags[pawn] = tag;
        Debug.Log($"[PawnNameTagManager] Created name tag for {pawn.playerName.Value} cid={pawn.Owner?.ClientId}");
    }

    public void RemoveForPawn(PlayerPawn pawn)
    {
        if (pawn == null) return;

        if (_tags.TryGetValue(pawn, out var tag) && tag != null)
            Destroy(tag.gameObject);

        _tags.Remove(pawn);
        Debug.Log($"[PawnNameTagManager] Removed name tag for pawn {pawn.Owner?.ClientId}");
    }
}
