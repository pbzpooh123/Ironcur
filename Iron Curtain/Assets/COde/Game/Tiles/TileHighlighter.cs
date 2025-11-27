using System.Collections.Generic;
using UnityEngine;

public class TileHighlighter : MonoBehaviour
{
    public static TileHighlighter Instance;

    [Header("Pool")]
    public HighlightPulse pulsePrefab;
    public int poolSize = 32;

    [Header("Local offset (board space)")]
    [Tooltip("Offset in the board's local X/Y (so it works even if the board is rotated).")]
    public float localXOffset = 0f;
    public float localYOffset = 0.1f;

    [Tooltip("Z offset so highlight renders above the board.")]
    public float zOffset = -0.1f;

    [Header("Colors")]
    public Color passColor = new(0.2f, 0.6f, 1f, 1f);
    public Color landColor = new(1f, 0.85f, 0.2f, 1f);

    readonly Queue<HighlightPulse> _pool = new();
    Transform _root;

    void Awake()
    {
        Instance = this;
        _root = new GameObject("HighlightPool").transform;
        _root.SetParent(transform, false);

        for (int i = 0; i < poolSize; i++)
            _pool.Enqueue(Instantiate(pulsePrefab, _root));

        foreach (var p in _pool)
            p.gameObject.SetActive(false);
    }

    HighlightPulse Get()
    {
        if (_pool.Count > 0)
        {
            var p = _pool.Dequeue();
            _pool.Enqueue(p); // ring buffer
            return p;
        }
        return Instantiate(pulsePrefab, _root);
    }

    public void FlashPassAt(Vector3 worldPos, float size = 1f)
        => Spawn(worldPos, passColor, size);

    public void FlashLandAt(Vector3 worldPos, float size = 1.1f)
        => Spawn(worldPos, landColor, size);

    void Spawn(Vector3 worldPos, Color c, float size)
    {
        if (!pulsePrefab) return;

        var p = Get();

        // Convert hit position into THIS object's local space (board space)
        Vector3 local = transform.InverseTransformPoint(worldPos);

        // Apply offset along board's own X/Y axes
        local.x += localXOffset;
        local.y += localYOffset;

        // Convert back to world
        Vector3 pos = transform.TransformPoint(local);
        pos.z += zOffset;

        p.transform.position = pos;
        p.color = c;


        p.gameObject.SetActive(true);
    }
}
