using UnityEngine;

// --- ADDED: Enum for Discard Effects --- 
public enum DiscardEffectType
{
    None,
    DealDamageToOpponentPet,
    GainBlockPlayer, 
    GainBlockPet,
    DrawCard,
    GainEnergy
}
// --- END ADDED ---

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
    public int drawAmount = 0; // How many cards to draw when played (0 if none)
    public int discardRandomAmount = 0; // How many random cards to discard from hand when played (0 if none)
    public int healingAmount = 0; // Amount of health to restore (0 if none)
    public int tempMaxHealthChange = 0; // Amount to change max health for this combat (0 if none)
    // Add other effects like buffs, debuffs, etc.

    [Header("Status Effect Application")]
    public StatusEffectType statusToApply = StatusEffectType.None;
    public int statusDuration = 0; // Turns the status lasts
    public int statusPotency = 0; // e.g., Damage reduction for Weak, % increase for Break?

    [Header("Discard Trigger Effect")]
    public DiscardEffectType discardEffectType = DiscardEffectType.None;
    public int discardEffectValue = 0; // Value associated with the discard effect (e.g., damage amount, block amount)

    [Header("Upgrade Info")]
    public CardData upgradedVersion = null; // Link to the ScriptableObject representing the upgraded version of this card

    // Add more properties later (e.g., damage, block, effects)
} 