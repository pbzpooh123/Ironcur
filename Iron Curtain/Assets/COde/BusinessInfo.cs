using UnityEngine;

[CreateAssetMenu(fileName = "New Business", menuName = "Game/Business Info")]
public class BusinessInfo : ScriptableObject
{
    public string businessName;
    [TextArea(2, 5)] public string description;
    [TextArea(2, 5)] public string perk;
}
