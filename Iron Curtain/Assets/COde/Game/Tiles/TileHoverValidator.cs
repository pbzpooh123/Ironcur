using UnityEngine;

public class TileHoverValidator : MonoBehaviour
{
    public string tilesLayerName = "Tiles";
    public bool autoFixLayer = true;
    public bool autoAddCollider = true;

    void Start()
    {
        int tilesLayer = LayerMask.NameToLayer(tilesLayerName);
        if (tilesLayer < 0) Debug.LogWarning($"[TileHoverValidator] Layer '{tilesLayerName}' does not exist.");

        var tiles = FindObjectsOfType<TileData>(true);
        int ok = 0, issues = 0;

        foreach (var t in tiles)
        {
            var src = t.GetComponent<TileHoverSource>();
            if (!src)
            {
                Debug.LogWarning($"[Hover] {t.name}: Missing TileHoverSource.");
                issues++;
                continue;
            }

            // Find collider in children
            var col = t.GetComponentInChildren<Collider>(true);
            if (!col)
            {
                if (autoAddCollider)
                {
                    var go = new GameObject("HitArea");
                    go.transform.SetParent(t.transform, false);
                    var bc = go.AddComponent<BoxCollider>();
                    bc.size = new Vector3(1f, 0.2f, 1f);
                    col = bc;
                    Debug.Log($"[Hover] {t.name}: Added BoxCollider HitArea.");
                }
                else
                {
                    Debug.LogWarning($"[Hover] {t.name}: No Collider in children.");
                    issues++;
                    continue;
                }
            }

            // Layer check
            if (tilesLayer >= 0 && col.gameObject.layer != tilesLayer)
            {
                if (autoFixLayer)
                {
                    col.gameObject.layer = tilesLayer;
                    Debug.Log($"[Hover] {t.name}: Set layer to {tilesLayerName}.");
                }
                else
                {
                    Debug.LogWarning($"[Hover] {t.name}: Collider layer is {LayerMask.LayerToName(col.gameObject.layer)} (expected {tilesLayerName}).");
                    issues++;
                }
            }

            // Size check
            if (col is BoxCollider bc2)
            {
                var s = bc2.size;
                if (s.x < 0.1f || s.z < 0.1f)
                {
                    bc2.size = new Vector3(Mathf.Max(1f, s.x), Mathf.Max(0.2f, s.y), Mathf.Max(1f, s.z));
                    Debug.Log($"[Hover] {t.name}: Adjusted BoxCollider size to {bc2.size}.");
                }
            }

            ok++;
        }

        Debug.Log($"[TileHoverValidator] Checked {tiles.Length} tiles. OK={ok}, Issues={issues}. Make sure HoverRaycaster.hoverMask includes only '{tilesLayerName}'.");
    }
}
