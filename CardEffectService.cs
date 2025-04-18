using UnityEngine;
using System.Collections.Generic;

public class CardEffectService
{
    private GameManager gameManager;
    
    public CardEffectService(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public bool ProcessCardEffect(CardData cardData, CardDropZone.TargetType targetType)
    {
        PlayerManager playerManager = gameManager.GetPlayerManager();
        StatusEffectManager statusManager = playerManager.GetStatusEffectManager(); // Get status manager
        
        // --- SCALING ATTACK: Calculate Bonus Damage ---
        int scalingBonusDamage = 0;
        if (cardData.isScalingAttack && cardData.damage > 0 && !string.IsNullOrEmpty(cardData.scalingIdentifier))
        {
            scalingBonusDamage = statusManager.GetScalingAttackBonus(cardData.scalingIdentifier, cardData.scalingDamageIncrease);
        }
        // --- END SCALING ATTACK ---

        // Apply effects based on target
        switch (targetType)
        {
            case CardDropZone.TargetType.EnemyPet:
                if (cardData.damage > 0)
                {
                    int baseDamage = cardData.damage;
                    int totalDamage = baseDamage + scalingBonusDamage; // Apply scaling bonus

                    // Check Attacker (Player) Weakness
                    int actualDamage = totalDamage;
                    if (playerManager.IsPlayerWeak()) {
                        int reduction = Mathf.FloorToInt(actualDamage * 0.25f);
                        actualDamage = Mathf.Max(0, actualDamage - reduction);
                        Debug.Log($"Player is Weak! Reducing damage from {totalDamage} to {actualDamage}");
                    }
                    
                    playerManager.DamageOpponentPet(actualDamage);
                    Debug.Log($"Dealt {actualDamage} (Base: {baseDamage}, Scaling: {scalingBonusDamage}) damage to Opponent Pet.");
                }
                
                // Healing & Temp Max HP for Enemy Pet (Unaffected by scaling)
                if (cardData.healingAmount > 0) { playerManager.HealOpponentPet(cardData.healingAmount); }
                if (cardData.tempMaxHealthChange != 0) { playerManager.ApplyTempMaxHealthOpponentPet(cardData.tempMaxHealthChange); }
                
                // Apply Status Effect to Enemy Pet
                if (cardData.statusToApply != StatusEffectType.None && cardData.statusDuration > 0) { playerManager.ApplyStatusEffectOpponentPet(cardData.statusToApply, cardData.statusDuration); }
                
                // Apply DoT to Enemy Pet
                if (cardData.dotDamageAmount > 0 && cardData.dotDuration > 0) { playerManager.ApplyDotOpponentPet(cardData.dotDamageAmount, cardData.dotDuration); }

                // --- REFLECTION: Apply Reflection Buff to Enemy Pet ---
                if (cardData.isReflectionCard && cardData.reflectionPercentage > 0 && cardData.reflectionDuration > 0)
                {
                    playerManager.ApplyReflectionOpponentPet(cardData.reflectionPercentage, cardData.reflectionDuration);
                }
                // --- END REFLECTION ---
                break;
                
            case CardDropZone.TargetType.PlayerSelf:
                if (cardData.block > 0) { playerManager.AddBlockToLocalPlayer(cardData.block); }
                if (cardData.healingAmount > 0) { playerManager.HealLocalPlayer(cardData.healingAmount); }
                if (cardData.tempMaxHealthChange != 0) { playerManager.ApplyTempMaxHealthPlayer(cardData.tempMaxHealthChange); }
                if (cardData.statusToApply != StatusEffectType.None && cardData.statusDuration > 0) { playerManager.ApplyStatusEffectLocalPlayer(cardData.statusToApply, cardData.statusDuration); }
                if (cardData.dotDamageAmount > 0 && cardData.dotDuration > 0) { playerManager.ApplyDotLocalPlayer(cardData.dotDamageAmount, cardData.dotDuration); }

                // --- REFLECTION: Apply Reflection Buff to Player ---
                if (cardData.isReflectionCard && cardData.reflectionPercentage > 0 && cardData.reflectionDuration > 0)
                {
                    playerManager.ApplyReflectionPlayer(cardData.reflectionPercentage, cardData.reflectionDuration);
                }
                // --- END REFLECTION ---
                break;
                
            case CardDropZone.TargetType.OwnPet:
                if (cardData.block > 0) { playerManager.AddBlockToLocalPet(cardData.block); }
                if (cardData.healingAmount > 0) { playerManager.HealLocalPet(cardData.healingAmount); }
                if (cardData.tempMaxHealthChange != 0) { playerManager.ApplyTempMaxHealthPet(cardData.tempMaxHealthChange); }
                if (cardData.statusToApply != StatusEffectType.None && cardData.statusDuration > 0) { playerManager.ApplyStatusEffectLocalPet(cardData.statusToApply, cardData.statusDuration); }
                if (cardData.dotDamageAmount > 0 && cardData.dotDuration > 0) { playerManager.ApplyDotLocalPet(cardData.dotDamageAmount, cardData.dotDuration); }

                // --- REFLECTION: Apply Reflection Buff to Own Pet ---
                if (cardData.isReflectionCard && cardData.reflectionPercentage > 0 && cardData.reflectionDuration > 0)
                {
                    playerManager.ApplyReflectionLocalPet(cardData.reflectionPercentage, cardData.reflectionDuration);
                }
                // --- END REFLECTION ---
                break;
        }
        
        // Apply target-independent effects
        
        if (cardData.energyGain > 0) { playerManager.GainEnergy(cardData.energyGain); }
        if (cardData.drawAmount > 0)
        {
            Debug.Log($"Drawing {cardData.drawAmount} cards.");
            DeckManager deckManager = gameManager.GetCardManager().GetDeckManager();
            for(int d=0; d < cardData.drawAmount; d++) { deckManager.DrawCard(); }
            gameManager.UpdateHandUI();
            gameManager.UpdateDeckCountUI();
        }
        if (cardData.discardRandomAmount > 0)
        {
             Debug.Log($"Discarding {cardData.discardRandomAmount} random cards from hand.");
             gameManager.GetCardManager().GetDeckManager().DiscardRandomCards(cardData.discardRandomAmount);
             gameManager.UpdateHandUI();
             gameManager.UpdateDeckCountUI();
        }
        if (cardData.isComboStarter)
        {
            playerManager.IncrementComboCount();
            if (playerManager.GetCurrentComboCount() >= cardData.comboTriggerValue) // Assume comboTriggerValue > 0 means it's a trigger card
            {
                Debug.Log($"COMBO TRIGGERED! ({playerManager.GetCurrentComboCount()}/{cardData.comboTriggerValue}) Applying effect: {cardData.comboEffectType}");
                ApplyComboEffect(cardData);
            }
        }
        if (cardData.costChangeTarget != CostChangeTargetType.None && cardData.costChangeAmount != 0)
        {
            if (cardData.costChangeTarget == CostChangeTargetType.OpponentHand) { playerManager.ApplyCostModifierToOpponentHand(cardData.costChangeAmount, cardData.costChangeDuration, cardData.costChangeCardCount); }
            else if (cardData.costChangeTarget == CostChangeTargetType.PlayerHand) { playerManager.ApplyCostModifierToLocalHand(cardData.costChangeAmount, cardData.costChangeDuration, cardData.costChangeCardCount); }
        }
        if (cardData.critChanceBuffAmount > 0)
        {
            if (cardData.critChanceBuffTarget == CardDropZone.TargetType.PlayerSelf) { playerManager.ApplyCritChanceBuffPlayer(cardData.critChanceBuffAmount, cardData.critChanceBuffDuration); }
            else if (cardData.critChanceBuffTarget == CardDropZone.TargetType.OwnPet) { playerManager.ApplyCritChanceBuffPet(cardData.critChanceBuffAmount, cardData.critChanceBuffDuration); }
        }
        if (cardData.upgradeHandCardCount > 0)
        {
            gameManager.GetCardManager().GetCardModificationService().ApplyTemporaryHandUpgrade(cardData.upgradeHandCardCount, cardData.upgradeHandCardDuration, cardData.upgradeHandCardTargetRule);
        }

        // --- SCALING ATTACK: Increment Counter AFTER effect resolves ---
        if (cardData.isScalingAttack && cardData.damage > 0 && !string.IsNullOrEmpty(cardData.scalingIdentifier))
        {
             statusManager.IncrementScalingAttackCounter(cardData.scalingIdentifier);
        }
        // --- END SCALING ATTACK ---
        
        return true;
    }
    
    public void ProcessOpponentPetCardEffect(CardData cardToPlay)
    {
        PlayerManager playerManager = gameManager.GetPlayerManager();
        StatusEffectManager statusManager = playerManager.GetStatusEffectManager();

        // --- SCALING ATTACK (Opponent): Calculate Bonus Damage ---
        // Note: Opponent scaling counters need to be synced or simulated.
        // For now, we assume opponent logic sends the *final* damage including scaling,
        // OR we need a way to track opponent scaling counters locally (complex sync).
        // Let's assume for now cardToPlay.damage ALREADY includes scaling from opponent's perspective.
        int scalingBonusDamage = 0; // Placeholder - actual calculation would depend on opponent state sync
        // --- END SCALING ATTACK ---
        
        if (cardToPlay.damage > 0)
        {
            int baseDamage = cardToPlay.damage; // Assume this includes scaling if implemented on opponent side
            int totalDamage = baseDamage + scalingBonusDamage; // Apply local scaling sim (if any)

            // Check Attacker (Opponent Pet) Weakness
            int actualDamage = totalDamage;
            if (playerManager.IsOpponentPetWeak()) {
                int reduction = Mathf.FloorToInt(actualDamage * 0.25f); 
                actualDamage = Mathf.Max(0, actualDamage - reduction);
                Debug.Log($"Opponent Pet is Weak! Reducing damage from {totalDamage} to {actualDamage}");
            }
            
            playerManager.DamageLocalPlayer(actualDamage);
            Debug.Log($"Opponent Pet dealt {actualDamage} damage to Local Player.");
        }
        
        // Apply Opponent Pet Block
        if (cardToPlay.block > 0) { playerManager.AddBlockToOpponentPet(cardToPlay.block); }
        if (cardToPlay.energyGain > 0)
        {
            PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
            int currentEnergy = petDeckManager.GetOpponentPetEnergy();
            petDeckManager.SetOpponentPetEnergy(currentEnergy + cardToPlay.energyGain);
        }
        if (cardToPlay.drawAmount > 0)
        {
             Debug.Log($"Opponent Pet drawing {cardToPlay.drawAmount} cards.");
             PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
             for(int d=0; d < cardToPlay.drawAmount; d++) { petDeckManager.DrawOpponentPetCard(); }
        }
        if (cardToPlay.discardRandomAmount > 0)
        {
             Debug.Log($"Opponent Pet discarding {cardToPlay.discardRandomAmount} random cards.");
             gameManager.GetCardManager().GetPetDeckManager().DiscardRandomOpponentPetCards(cardToPlay.discardRandomAmount);
        }
        // Apply Opponent Pet Status Effects (e.g., Weak, Break on Player)
        if (cardToPlay.statusToApply != StatusEffectType.None && cardToPlay.statusDuration > 0)
        {
             // Assuming opponent pet cards target the local player for statuses
             playerManager.ApplyStatusEffectLocalPlayer(cardToPlay.statusToApply, cardToPlay.statusDuration);
        }
        // Apply Opponent Pet DoT (on Player)
        if (cardToPlay.dotDamageAmount > 0 && cardToPlay.dotDuration > 0)
        {
             playerManager.ApplyDotLocalPlayer(cardToPlay.dotDamageAmount, cardToPlay.dotDuration);
        }
        // Apply Opponent Pet Reflection Buff (on Opponent Pet)
        if (cardToPlay.isReflectionCard && cardToPlay.reflectionPercentage > 0 && cardToPlay.reflectionDuration > 0)
        {
            // Apply locally for simulation/UI, actual state comes from network
            playerManager.ApplyReflectionOpponentPet(cardToPlay.reflectionPercentage, cardToPlay.reflectionDuration);
        }
        // Apply Opponent Pet Crit Buff (on Opponent Pet)
        if (cardToPlay.critChanceBuffAmount > 0 && cardToPlay.critChanceBuffTarget == CardDropZone.TargetType.OwnPet) // OwnPet from opponent's perspective
        {
            playerManager.ApplyCritChanceBuffOpponentPet(cardToPlay.critChanceBuffAmount, cardToPlay.critChanceBuffDuration);
        }

        // --- SCALING ATTACK (Opponent): Increment Counter ---
        // If opponent scaling counters are tracked locally, increment here.
        // if (cardToPlay.isScalingAttack && !string.IsNullOrEmpty(cardToPlay.scalingIdentifier))
        // {
        //     statusManager.IncrementOpponentScalingAttackCounter(cardToPlay.scalingIdentifier); // Needs implementation + sync
        // }
        // --- END SCALING ATTACK ---

        // Update UI after opponent turn simulation
        gameManager.UpdateHealthUI();
        gameManager.GetCombatManager().UpdateStatusAndComboUI();
    }
    
    public void HandleDiscardTrigger(CardData discardedCard)
    {
        if (discardedCard == null || discardedCard.discardEffectType == DiscardEffectType.None)
        {
            return; // No effect to trigger
        }

        Debug.Log($"Handling discard trigger for {discardedCard.cardName}: {discardedCard.discardEffectType}, Value: {discardedCard.discardEffectValue}");

        PlayerManager playerManager = gameManager.GetPlayerManager();
        int value = discardedCard.discardEffectValue;

        switch (discardedCard.discardEffectType)
        {
            case DiscardEffectType.DealDamageToOpponentPet:
                if (value > 0) playerManager.DamageOpponentPet(value);
                break;
            case DiscardEffectType.GainBlockPlayer:
                if (value > 0) playerManager.AddBlockToLocalPlayer(value);
                break;
            case DiscardEffectType.GainBlockPet:
                if (value > 0) playerManager.AddBlockToLocalPet(value);
                break;
            case DiscardEffectType.DrawCard:
                if (value > 0) {
                    DeckManager deckManager = gameManager.GetCardManager().GetDeckManager();
                    for(int i=0; i < value; i++) deckManager.DrawCard();
                    gameManager.UpdateHandUI();
                    gameManager.UpdateDeckCountUI();
                }
                break;
            case DiscardEffectType.GainEnergy:
                if (value > 0) playerManager.GainEnergy(value);
                break;
        }
    }
    
    private void ApplyComboEffect(CardData triggeringCard)
    {
        PlayerManager playerManager = gameManager.GetPlayerManager();
        Debug.Log($"Applying Combo Effect: {triggeringCard.comboEffectType}");
        int value = triggeringCard.comboEffectValue; // Use the value from CardData
        CardDropZone.TargetType target = triggeringCard.comboEffectTarget; // Use the target from CardData

        switch (triggeringCard.comboEffectType)
        {
            case ComboEffectType.GainEnergy:
                if (value > 0) playerManager.GainEnergy(value);
                break;

            case ComboEffectType.DrawCard:
                if (value > 0) {
                    DeckManager deckManager = gameManager.GetCardManager().GetDeckManager();
                    for(int d=0; d < value; d++) { deckManager.DrawCard(); }
                    gameManager.UpdateHandUI();
                    gameManager.UpdateDeckCountUI();
                }
                break;

            case ComboEffectType.DealDamageToOpponentPet:
                if (value > 0 && target == CardDropZone.TargetType.EnemyPet) // Ensure target matches type
                {
                    // Apply Weakness check if necessary
                    int actualDamage = value;
                     if (playerManager.IsPlayerWeak()) {
                         int reduction = Mathf.FloorToInt(actualDamage * 0.25f);
                         actualDamage = Mathf.Max(0, actualDamage - reduction);
                     }
                    playerManager.DamageOpponentPet(actualDamage);
                }
                // Add checks here if you want DealDamageToOpponentPet to potentially target others via comboEffectTarget
                break;

             case ComboEffectType.GainBlockPlayer:
                 if (value > 0 && target == CardDropZone.TargetType.PlayerSelf)
                 {
                     playerManager.AddBlockToLocalPlayer(value);
                 }
                 break;

             case ComboEffectType.GainBlockPet:
                 if (value > 0 && target == CardDropZone.TargetType.OwnPet)
                 {
                     playerManager.AddBlockToLocalPet(value);
                 }
                 break;
            
            // Add more cases as needed for other ComboEffectType enum values
            
            case ComboEffectType.None:
            default:
                 Debug.LogWarning($"Unhandled or None ComboEffectType: {triggeringCard.comboEffectType}");
                 break;
        }
        gameManager.UpdateHealthUI(); // Update UI if health/block changed
        gameManager.UpdateEnergyUI(); // Update UI if energy changed
    }
}