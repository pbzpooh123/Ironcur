using UnityEngine;
using System.Collections.Generic;

public enum EventType { Tile, Main }
public enum TargetType { None, Stock, Factory, Global }

[System.Serializable]
public class EventEffect
{
    public TargetType targetType;     // Stock / Factory / Global
    public string targetName;         // เช่น "Steel & Iron" หรือ "Banking"
    public int moneyDelta;            // เงินทันที (อาจจะเป็นค่าปรับ / โบนัส)
    public float multiplier = 1f;     // ตัวคูณรายได้
    public int duration = 0;          // ระยะเวลาที่มีผล (round)
    public bool skipTurn;             // true = ผู้เล่นต้องข้าม turn
    public int randomMoneyMin;        // สำหรับ event ที่แจกเงินสุ่ม
    public int randomMoneyMax;
}

[CreateAssetMenu(fileName = "NewGameEvent", menuName = "Game/Event", order = 1)]
public class GameEventSO : ScriptableObject
{
    [Header("Basic Info")]
    public string eventName;
    [TextArea(3, 5)]
    public string description;
    public EventType type;

    [Header("Effects")]
    public List<EventEffect> effects = new List<EventEffect>();
}