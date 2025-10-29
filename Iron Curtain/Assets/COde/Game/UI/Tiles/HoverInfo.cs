// HoverInfo.cs
using UnityEngine;
using System.Collections.Generic;

public class HoverInfo
{
    public List<string> lines = new();
    public string title;     // “Steel & Iron Co.”
    public string subtitle;  // “Sector: Heavy Industry”
    public string line1;     // “Owner: Pooh (60%)”
    public string line2;     // “Price: $350M  •  Income: $50M”
    public Color tint;      // optional, use owner color or sector color
    
}
