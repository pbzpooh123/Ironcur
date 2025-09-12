using UnityEngine;

public class BoardManager : MonoBehaviour
{
    public static BoardManager Instance;

    [Header("Board Path")]
    public Transform[] waypoints; // ช่องบนบอร์ด (ตั้งเป็น Empty Object วางในฉาก)

    private void Awake()
    {
        Instance = this;
    }

    public Vector3 GetTilePosition(int index)
    {
        if (index < 0 || index >= waypoints.Length)
            return waypoints[0].position;
        return waypoints[index].position;
    }

    public int TileCount => waypoints.Length;
}