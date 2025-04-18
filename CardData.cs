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

// --- ADDED: Enum for Combo Effects --- 
public enum ComboEffectType
{
    None,
    DealDamageToOpponentPet,
    GainBlockPlayer,
    GainBlockPet,
    DrawCard,
    GainEnergy
    // Add more complex combo effects later
}
// --- END ADDED ---

// --- ADDED: Enum for Cost Change Target ---
public enum CostChangeTargetType
{
    None,
    PlayerHand,
    OpponentHand
}
// --- END ADDED ---

// --- ADDED: Enum for Upgrade Target Rule ---
public enum UpgradeTargetRule
{
    Random,
    Cheapest,
    MostExpensive
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

    [Header("Damage Over Time (DoT)")]
    public int dotDamageAmount = 0; // Damage per turn
    public int dotDuration = 0; // Turns the DoT lasts

    [Header("Combo Effect")]
    public bool isComboStarter = false; // Does this card contribute to/start a combo?
    public int comboTriggerValue = 0; // Required combo count to trigger combo effect
    public ComboEffectType comboEffectType = ComboEffectType.None; // Effect to apply on combo trigger
    public int comboEffectValue = 0; // Value for the combo effect (e.g., damage, block)
    public CardDropZone.TargetType comboEffectTarget = CardDropZone.TargetType.EnemyPet; // Who does the combo effect hit?

    [Header("Cost Modification Effect")]
    public CostChangeTargetType costChangeTarget = CostChangeTargetType.None;
    public int costChangeAmount = 0; // e.g., +1 or -1
    public int costChangeDuration = 1; // Turns the change lasts (e.g., 1 for opponent's next turn)
    public int costChangeCardCount = 0; // How many cards targeted (0 = all, >0 = random count)

    [Header("Critical Chance Buff Effect")]
    public CardDropZone.TargetType critChanceBuffTarget = CardDropZone.TargetType.PlayerSelf;
    public int critChanceBuffAmount = 0; // Percentage bonus (e.g., 20 for +20%)
    public int critChanceBuffDuration = 0; // Duration in turns (0 = end of combat)

    [Header("Temporary Card Upgrade Effect")]
    public int upgradeHandCardCount = 0; // How many cards in hand to upgrade
    public int upgradeHandCardDuration = 1; // Duration (0 = rest of combat, 1 = this turn only)
    public UpgradeTargetRule upgradeHandCardTargetRule = UpgradeTargetRule.Random;

    [Header("Discard Trigger Effect")]
    public DiscardEffectType discardEffectType = DiscardEffectType.None;
    public int discardEffectValue = 0; // Value associated with the discard effect (e.g., damage amount, block amount)

    [Header("Upgrade Info")]
    public CardData upgradedVersion = null; // Link to the ScriptableObject representing the upgraded version of this card

    // --- ADDED: Reflection Properties ---
    public bool isReflectionCard = false; // Does this card apply reflection?
    public int reflectionPercentage = 0; // % of damage reflected
    public int reflectionDuration = 0; // Turns the reflection lasts

    // --- ADDED: Scaling Attack Properties ---
    public bool isScalingAttack = false; // Does this attack scale with uses?
    public int scalingDamageIncrease = 0; // Damage increase per use this combat
    public string scalingIdentifier = ""; // Unique ID for this scaling card type

    // Add more properties later (e.g., damage, block, effects)
} 