using UnityEngine;

[CreateAssetMenu(fileName = "New Card", menuName = "Cards/Card Data")]
public class CardData : ScriptableObject
{
    public string cardName = "New Card";
    public int cost = 1;
    public string description = "Card Effect Description";
    
    [Header("Gameplay Effects")]
    public int damage = 0; // Amount of damage this card deals (0 if none)
    // public int block = 0; // Example for later
    // Add other effects like buffs, debuffs, draw, etc.

    // Add more properties later (e.g., damage, block, effects)
} 