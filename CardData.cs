using UnityEngine;

[CreateAssetMenu(fileName = "New Card", menuName = "Cards/Card Data")]
public class CardData : ScriptableObject
{
    public string cardName = "New Card";
    public int cost = 1;
    public string description = "Card Effect Description";
    // Add more properties later (e.g., damage, block, effects)
} 