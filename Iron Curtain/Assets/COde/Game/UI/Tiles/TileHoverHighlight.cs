// TileHoverHighlight.cs
using UnityEngine;

[RequireComponent(typeof(TileHover))]
public class TileHoverHighlight : MonoBehaviour
{
    public TileVisuals visuals;   // link your visuals script or an outline image
    private bool _isHover;

    void Reset()
    {
        if (!visuals) visuals = GetComponent<TileData>()?.visuals;
    }

    void OnEnable()  => Set(false);
    void OnDisable() => Set(false);

    public void Set(bool on)
    {
        _isHover = on;
        if (visuals != null) visuals.SetHover(on); // implement SetHover(bool) to show an outline
    }
}
