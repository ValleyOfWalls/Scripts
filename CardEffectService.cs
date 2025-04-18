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
        
        // Apply effects based on target
        switch (targetType)
        {
            case CardDropZone.TargetType.EnemyPet:
                if (cardData.damage > 0)
                {
                    // Check Attacker (Player) Weakness
                    int actualDamage = cardData.damage;
                    if (playerManager.IsPlayerWeak()) {
                        int reduction = Mathf.FloorToInt(actualDamage * 0.25f);
                        actualDamage = Mathf.Max(0, actualDamage - reduction);
                        Debug.Log($"Player is Weak! Reducing damage from {cardData.damage} to {actualDamage}");
                    }
                    
                    playerManager.DamageOpponentPet(actualDamage);
                    Debug.Log($"Dealt {actualDamage} damage to Opponent Pet. New health: {playerManager.GetOpponentPetHealth()}, Est. Block: {playerManager.GetOpponentPetBlock()}");
                }
                
                // Healing & Temp Max HP for Enemy Pet
                if (cardData.healingAmount > 0)
                {
                    playerManager.HealOpponentPet(cardData.healingAmount);
                }
                
                if (cardData.tempMaxHealthChange != 0)
                {
                    playerManager.ApplyTempMaxHealthOpponentPet(cardData.tempMaxHealthChange);
                }
                
                // Apply Status Effect to Enemy Pet
                if (cardData.statusToApply != StatusEffectType.None && cardData.statusDuration > 0)
                {
                    playerManager.ApplyStatusEffectOpponentPet(cardData.statusToApply, cardData.statusDuration);
                }
                
                // Apply DoT to Enemy Pet
                if (cardData.dotDamageAmount > 0 && cardData.dotDuration > 0)
                {
                    playerManager.ApplyDotOpponentPet(cardData.dotDamageAmount, cardData.dotDuration);
                }
                break;
                
            case CardDropZone.TargetType.PlayerSelf:
                if (cardData.block > 0)
                {
                    playerManager.AddBlockToLocalPlayer(cardData.block);
                }
                
                // Healing & Temp Max HP for Player
                if (cardData.healingAmount > 0)
                {
                    playerManager.HealLocalPlayer(cardData.healingAmount);
                }
                
                if (cardData.tempMaxHealthChange != 0)
                {
                    playerManager.ApplyTempMaxHealthPlayer(cardData.tempMaxHealthChange);
                }
                
                // Apply Status Effect to Player
                if (cardData.statusToApply != StatusEffectType.None && cardData.statusDuration > 0)
                {
                    playerManager.ApplyStatusEffectLocalPlayer(cardData.statusToApply, cardData.statusDuration);
                }
                
                // Apply DoT to Player
                if (cardData.dotDamageAmount > 0 && cardData.dotDuration > 0)
                {
                    playerManager.ApplyDotLocalPlayer(cardData.dotDamageAmount, cardData.dotDuration);
                }
                break;
                
            case CardDropZone.TargetType.OwnPet:
                if (cardData.block > 0)
                {
                    playerManager.AddBlockToLocalPet(cardData.block);
                }
                
                // Healing & Temp Max HP for Own Pet
                if (cardData.healingAmount > 0)
                {
                    playerManager.HealLocalPet(cardData.healingAmount);
                }
                
                if (cardData.tempMaxHealthChange != 0)
                {
                    playerManager.ApplyTempMaxHealthPet(cardData.tempMaxHealthChange);
                }
                
                // Apply Status Effect to Own Pet
                if (cardData.statusToApply != StatusEffectType.None && cardData.statusDuration > 0)
                {
                    playerManager.ApplyStatusEffectLocalPet(cardData.statusToApply, cardData.statusDuration);
                }
                
                // Apply DoT to Own Pet
                if (cardData.dotDamageAmount > 0 && cardData.dotDuration > 0)
                {
                    playerManager.ApplyDotLocalPet(cardData.dotDamageAmount, cardData.dotDuration);
                }
                break;
        }
        
        // Apply target-independent effects
        
        // Apply energy gain
        if (cardData.energyGain > 0)
        {
            playerManager.GainEnergy(cardData.energyGain);
        }
        
        // Apply Draw Card
        if (cardData.drawAmount > 0)
        {
            Debug.Log($"Drawing {cardData.drawAmount} cards.");
            DeckManager deckManager = gameManager.GetCardManager().GetDeckManager();
            for(int d=0; d < cardData.drawAmount; d++)
            {
                deckManager.DrawCard();
            }
            gameManager.UpdateHandUI();
            gameManager.UpdateDeckCountUI();
        }
        
        // Apply Discard Random
        if (cardData.discardRandomAmount > 0)
        {
            Debug.Log($"Discarding {cardData.discardRandomAmount} random cards from hand.");
            gameManager.GetCardManager().GetDeckManager().DiscardRandomCards(cardData.discardRandomAmount);
            gameManager.UpdateHandUI();
            gameManager.UpdateDeckCountUI();
        }
        
        // Combo Check
        if (cardData.isComboStarter)
        {
            playerManager.IncrementComboCount();
            if (playerManager.GetCurrentComboCount() >= cardData.comboTriggerValue)
            {
                Debug.Log($"COMBO TRIGGERED! ({playerManager.GetCurrentComboCount()}/{cardData.comboTriggerValue}) Applying effect: {cardData.comboEffectType}");
                ApplyComboEffect(cardData);
            }
        }
        
        // Apply Cost Modifier Effect
        if (cardData.costChangeTarget != CostChangeTargetType.None && cardData.costChangeAmount != 0)
        {
            if (cardData.costChangeTarget == CostChangeTargetType.OpponentHand)
            {
                playerManager.ApplyCostModifierToOpponentHand(cardData.costChangeAmount, cardData.costChangeDuration, cardData.costChangeCardCount);
            }
            else if (cardData.costChangeTarget == CostChangeTargetType.PlayerHand)
            {
                playerManager.ApplyCostModifierToLocalHand(cardData.costChangeAmount, cardData.costChangeDuration, cardData.costChangeCardCount);
            }
        }
        
        // Apply Crit Chance Buff Effect
        if (cardData.critChanceBuffAmount > 0)
        {
            if (cardData.critChanceBuffTarget == CardDropZone.TargetType.PlayerSelf)
            {
                playerManager.ApplyCritChanceBuffPlayer(cardData.critChanceBuffAmount, cardData.critChanceBuffDuration);
            }
            else if (cardData.critChanceBuffTarget == CardDropZone.TargetType.OwnPet)
            {
                playerManager.ApplyCritChanceBuffPet(cardData.critChanceBuffAmount, cardData.critChanceBuffDuration);
            }
        }
        
        // Apply Temporary Hand Upgrade Effect
        if (cardData.upgradeHandCardCount > 0)
        {
            gameManager.GetCardManager().GetCardModificationService().ApplyTemporaryHandUpgrade(
                cardData.upgradeHandCardCount, 
                cardData.upgradeHandCardDuration, 
                cardData.upgradeHandCardTargetRule);
        }
        
        return true;
    }
    
    public void ProcessOpponentPetCardEffect(CardData cardToPlay)
    {
        PlayerManager playerManager = gameManager.GetPlayerManager();
        
        if (cardToPlay.damage > 0)
        {
            // Check Attacker (Opponent Pet) Weakness
            int actualDamage = cardToPlay.damage;
            if (playerManager.IsOpponentPetWeak()) {
                // Example: 25% reduction, rounded down
                int reduction = Mathf.FloorToInt(actualDamage * 0.25f); 
                actualDamage = Mathf.Max(0, actualDamage - reduction);
                Debug.Log($"Opponent Pet is Weak! Reducing damage from {cardToPlay.damage} to {actualDamage}");
            }
            
            playerManager.DamageLocalPlayer(actualDamage);
            Debug.Log($"Opponent Pet dealt {actualDamage} damage to Local Player. New health: {playerManager.GetLocalPlayerHealth()}, Block: {playerManager.GetLocalPlayerBlock()}");
        }
        
        // Apply Opponent Pet Block
        if (cardToPlay.block > 0)
        {
            playerManager.AddBlockToOpponentPet(cardToPlay.block);
            Debug.Log($"Opponent Pet gained {cardToPlay.block} block. New block (local sim): {playerManager.GetOpponentPetBlock()}");
        }
        
        // Apply Opponent Pet Energy Gain
        if (cardToPlay.energyGain > 0)
        {
            PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
            int currentEnergy = petDeckManager.GetOpponentPetEnergy();
            petDeckManager.SetOpponentPetEnergy(currentEnergy + cardToPlay.energyGain);
            Debug.Log($"Opponent Pet gained {cardToPlay.energyGain} energy. New energy: {petDeckManager.GetOpponentPetEnergy()}");
        }
        
        // Apply Opponent Pet Draw
        if (cardToPlay.drawAmount > 0)
        {
            Debug.Log($"Opponent Pet drawing {cardToPlay.drawAmount} cards.");
            PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
            for(int d=0; d < cardToPlay.drawAmount; d++)
            {
                petDeckManager.DrawOpponentPetCard();
            }
        }
        
        // Apply Opponent Pet Discard
        if (cardToPlay.discardRandomAmount > 0)
        {
            Debug.Log($"Opponent Pet discarding {cardToPlay.discardRandomAmount} random cards.");
            gameManager.GetCardManager().GetPetDeckManager().DiscardRandomOpponentPetCards(cardToPlay.discardRandomAmount);
        }
        
        // Apply Opponent Pet Healing
        if (cardToPlay.healingAmount > 0)
        {
            playerManager.HealOpponentPet(cardToPlay.healingAmount);
        }
        
        // Apply Opponent Pet Temp Max HP
        if (cardToPlay.tempMaxHealthChange != 0)
        {
            playerManager.ApplyTempMaxHealthOpponentPet(cardToPlay.tempMaxHealthChange);
        }
        
        // Apply Status Effects (Opponent Pet applying to Player)
        if (cardToPlay.statusToApply != StatusEffectType.None && cardToPlay.statusDuration > 0)
        {
            playerManager.ApplyStatusEffectLocalPlayer(cardToPlay.statusToApply, cardToPlay.statusDuration);
        }
        
        // Apply DoT (Opponent Pet applying to Player)
        if (cardToPlay.dotDamageAmount > 0 && cardToPlay.dotDuration > 0)
        {
            playerManager.ApplyDotLocalPlayer(cardToPlay.dotDamageAmount, cardToPlay.dotDuration);
        }
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
        int value = triggeringCard.comboEffectValue;

        switch (triggeringCard.comboEffectType)
        {
            case ComboEffectType.DealDamageToOpponentPet:
                if (value > 0) playerManager.DamageOpponentPet(value);
                break;
            case ComboEffectType.GainBlockPlayer:
                if (value > 0) playerManager.AddBlockToLocalPlayer(value);
                break;
            case ComboEffectType.GainBlockPet:
                if (value > 0) playerManager.AddBlockToLocalPet(value);
                break;
            case ComboEffectType.DrawCard:
                if (value > 0) {
                    DeckManager deckManager = gameManager.GetCardManager().GetDeckManager();
                    for(int i=0; i < value; i++) deckManager.DrawCard();
                    gameManager.UpdateHandUI();
                    gameManager.UpdateDeckCountUI();
                }
                break;
            case ComboEffectType.GainEnergy:
                if (value > 0) playerManager.GainEnergy(value);
                break;
        }
    }
}