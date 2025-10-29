using System.Collections.Generic;
using UnityEngine;

public class TileHighlighter : MonoBehaviour
{
    public static TileHighlighter Instance;

    [Header("Pool")]
    public HighlightPulse pulsePrefab;
    public int poolSize = 32;
    public float zOffset = -0.1f; // render just above board (adjust for your camera)

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
        foreach (var p in _pool) p.gameObject.SetActive(false);
    }

    HighlightPulse Get()
    {
        if (_pool.Count > 0)
        {
            var p = _pool.Dequeue();
            _pool.Enqueue(p); // simple ring buffer reuse
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
        var pos = worldPos;
        pos.z += zOffset;
        p.transform.position = pos;
        p.color = c;
        p.startScale = size * 85f;
        p.endScale   = size * 115f;
        p.gameObject.SetActive(true);
    }
}
