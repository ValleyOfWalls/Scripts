using UnityEngine;

[CreateAssetMenu(fileName = "New Card", menuName = "Cards/Card Data")]
public class CardData : ScriptableObject
{
    public string cardName = "New Card";
    public int cost = 1;
    public string description = "Card Effect Description";
    
    [Header("Gameplay Effects")]
    public int damage = 0; // Amount of damage this card deals (0 if none)
    public int block = 0; // Amount of block this card provides (0 if none)
    public int energyGain = 0; // Amount of energy this card provides (0 if none)
    // Add other effects like buffs, debuffs, draw, etc.

    [Header("Upgrade Info")]
    public CardData upgradedVersion = null; // Link to the ScriptableObject representing the upgraded version of this card

    // Add more properties later (e.g., damage, block, effects)
} 