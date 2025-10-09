using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "Events/TimelineSO")]
public class TimelineSO : ScriptableObject
{
    public string timelineName;
    public List<GameEventSO> mainEvents = new();
}