using UnityEngine;
using System.Collections.Generic;
using Photon.Realtime;
using Photon.Pun;
using System.Collections;

public class CardEffectService
{
    private GameManager gameManager;
    private CombatTurnManager _combatTurnManager;
    private Transform effectsCanvasTransform; // Added: Reference to the effects canvas
    private Camera effectsCanvasCamera; // Added: Camera assigned to the effects canvas
    private float effectsCanvasPlaneDistance; // Added: Plane distance for position calculation
    
    private CombatTurnManager CombatTurnManagerInstance
    {
        get
        {
            if (_combatTurnManager == null)
            {
                _combatTurnManager = gameManager.GetCombatManager()?.GetCombatTurnManager();
                if (_combatTurnManager == null) {
                     Debug.LogError("CardEffectService failed to get CombatTurnManager on demand!");
                }
            }
            return _combatTurnManager;
        }
    }
    
    public CardEffectService(GameManager gameManager)
    {
        this.gameManager = gameManager;
        
        // Find the Effects Canvas and its camera
        GameObject effectsCanvasGO = GameObject.Find("EffectsCanvas"); // Find by name (adjust if needed)
        if (effectsCanvasGO != null)
        {
            Canvas canvas = effectsCanvasGO.GetComponent<Canvas>();
            if (canvas != null && canvas.renderMode == RenderMode.ScreenSpaceCamera)
            {
                effectsCanvasTransform = effectsCanvasGO.transform;
                effectsCanvasCamera = canvas.worldCamera; 
                effectsCanvasPlaneDistance = canvas.planeDistance;
                if (effectsCanvasCamera == null)
                {
                     Debug.LogError("EffectsCanvas found, but it has no Render Camera assigned!");
                }
            }
            else
            {
                 Debug.LogError("Found EffectsCanvas, but it's not set to Screen Space - Camera mode!");
            }
        }
        else
        {
             Debug.LogError("CardEffectService could not find the EffectsCanvas GameObject! Make sure it exists in the scene and is named correctly.");
        }
    }
    
    public bool ProcessCardEffect(CardData cardData, CardDropZone.TargetType targetType, int previousPlaysThisCombat, int totalCopies)
    {
        PlayerManager playerManager = gameManager.GetPlayerManager();
        int currentTurn = CombatTurnManagerInstance?.GetCurrentCombatTurn() ?? 1; // Get current turn, default to 1 if manager missing
        
        // --- ADDED: Check Player Health Status for Threshold --- 
        bool isBelowHealthThreshold = false;
        if (cardData.healthThresholdPercent > 0) 
        {
            int currentHP = playerManager.GetLocalPlayerHealth();
            int maxHP = playerManager.GetEffectivePlayerMaxHealth();
            if (maxHP > 0) // Avoid division by zero
            {
                float currentHPPct = ((float)currentHP / maxHP) * 100f;
                if (currentHPPct < cardData.healthThresholdPercent)
                {
                    isBelowHealthThreshold = true;
                    Debug.Log($"Player health ({currentHP}/{maxHP} = {currentHPPct:F1}%) is below threshold ({cardData.healthThresholdPercent}%). Applying enhanced effect.");
                }
            }
        }
        // --- END ADDED ---
        
        // --- MODIFIED: Play Target Visual Effect --- 
        if (cardData.targetEffectPrefab != null) // Check prefab field now
        {
            PlayTargetEffectPrefab(targetType, cardData.targetEffectPrefab); // Call renamed method
        }
        // --- END MODIFIED ---
        
        // Apply effects based on target
        switch (targetType)
        {
            case CardDropZone.TargetType.EnemyPet:
                // --- Damage Processing ---
                if (cardData.damage > 0 || cardData.damageScalingPerTurn > 0 || cardData.damageScalingPerPlay > 0 || cardData.damageScalingPerCopy > 0)
                {
                    int baseDamage = cardData.damage;
                    int turnBonusDamage = 0;
                    int playBonusDamage = 0;
                    int copyBonusDamage = 0;
                    
                    // Calculate Turn Scaling Bonus
                    if (cardData.damageScalingPerTurn > 0) 
                    {
                        turnBonusDamage = Mathf.FloorToInt(cardData.damageScalingPerTurn * currentTurn);
                        Debug.Log($"Damage Scaling (Turn): Base={baseDamage}, Scale/Turn={cardData.damageScalingPerTurn}, Turn={currentTurn}, Bonus={turnBonusDamage}");
                    }
                    // Calculate Play Scaling Bonus
                    if (cardData.damageScalingPerPlay > 0) 
                    {
                        playBonusDamage = Mathf.FloorToInt(cardData.damageScalingPerPlay * previousPlaysThisCombat);
                        Debug.Log($"Damage Scaling (Play): Base={baseDamage}, Scale/Play={cardData.damageScalingPerPlay}, PrevPlays={previousPlaysThisCombat}, Bonus={playBonusDamage}");
                    }
                    // Calculate Copy Scaling Bonus
                    if (cardData.damageScalingPerCopy > 0)
                    {
                        copyBonusDamage = Mathf.FloorToInt(cardData.damageScalingPerCopy * totalCopies);
                        Debug.Log($"Damage Scaling (Copy): Base={baseDamage}, Scale/Copy={cardData.damageScalingPerCopy}, Copies={totalCopies}, Bonus={copyBonusDamage}");
                    }
                    int totalDamageBeforeEnhancements = baseDamage + turnBonusDamage + playBonusDamage + copyBonusDamage;
                    float damageMultiplier = 1.0f;
                    
                    // Apply Low Health Multiplier
                    if (isBelowHealthThreshold && cardData.damageMultiplierBelowThreshold > 1.0f) {
                        damageMultiplier *= cardData.damageMultiplierBelowThreshold;
                        Debug.Log($"Applying low health damage multiplier: {cardData.damageMultiplierBelowThreshold}x");
                    }
                    
                    int damageAfterMultipliers = Mathf.FloorToInt(totalDamageBeforeEnhancements * damageMultiplier);
                    
                    // Apply Player Strength Bonus
                    int strengthBonus = playerManager.GetPlayerStrength();
                    if (strengthBonus != 0) {
                        damageAfterMultipliers += strengthBonus;
                        Debug.Log($"Applying Player Strength Bonus to Damage: +{strengthBonus}");
                    }
                    
                    int totalDamageBeforeWeak = damageAfterMultipliers;
                    int actualDamage = totalDamageBeforeWeak;
                    
                    // Check Attacker (Player) Weakness
                    if (playerManager.IsPlayerWeak()) {
                        int reduction = Mathf.FloorToInt(actualDamage * 0.5f);
                        actualDamage = Mathf.Max(0, actualDamage - reduction);
                        Debug.Log($"Player is Weak! Reducing damage from {totalDamageBeforeWeak} to {actualDamage} (-50%)");
                    }
                    
                    if (actualDamage > 0) 
                    {
                        playerManager.DamageOpponentPet(actualDamage);
                        int finalPetHealth = playerManager.GetOpponentPetHealth(); // Get health AFTER damage
                        Debug.Log($"Dealt {actualDamage} damage to Opponent Pet. (Base: {baseDamage}, TurnB: {turnBonusDamage}, PlayB: {playBonusDamage}, CopyB: {copyBonusDamage}, StrB: {strengthBonus}, Multiplier: {damageMultiplier:F2}). New health: {finalPetHealth}, Est. Block: {playerManager.GetOpponentPetBlock()}");
                    }
                }
                
                // --- Block Processing ---
                if (cardData.block > 0 || cardData.blockScalingPerTurn > 0 || cardData.blockScalingPerPlay > 0 || cardData.blockScalingPerCopy > 0)
                {
                    int baseBlock = cardData.block;
                    int turnBonusBlock = 0;
                    int playBonusBlock = 0;
                    int copyBonusBlock = 0;
                    
                    // Calculate Turn Scaling Bonus
                    if (cardData.blockScalingPerTurn > 0)
                    {
                        turnBonusBlock = Mathf.FloorToInt(cardData.blockScalingPerTurn * currentTurn);
                        Debug.Log($"Block Scaling (Turn): Base={baseBlock}, Scale/Turn={cardData.blockScalingPerTurn}, Turn={currentTurn}, Bonus={turnBonusBlock}");
                    }
                    // Calculate Play Scaling Bonus
                    if (cardData.blockScalingPerPlay > 0) 
                    {
                        playBonusBlock = Mathf.FloorToInt(cardData.blockScalingPerPlay * previousPlaysThisCombat);
                        Debug.Log($"Block Scaling (Play): Base={baseBlock}, Scale/Play={cardData.blockScalingPerPlay}, PrevPlays={previousPlaysThisCombat}, Bonus={playBonusBlock}");
                    }
                    // Calculate Copy Scaling Bonus
                    if (cardData.blockScalingPerCopy > 0)
                    {
                        copyBonusBlock = Mathf.FloorToInt(cardData.blockScalingPerCopy * totalCopies);
                        Debug.Log($"Block Scaling (Copy): Base={baseBlock}, Scale/Copy={cardData.blockScalingPerCopy}, Copies={totalCopies}, Bonus={copyBonusBlock}");
                    }
                    int totalBlockBeforeEnhancements = baseBlock + turnBonusBlock + playBonusBlock + copyBonusBlock;
                    float blockMultiplier = 1.0f;
                    
                    // Apply Low Health Multiplier
                    if (isBelowHealthThreshold && cardData.blockMultiplierBelowThreshold > 1.0f) {
                        blockMultiplier *= cardData.blockMultiplierBelowThreshold;
                        Debug.Log($"Applying low health block multiplier: {cardData.blockMultiplierBelowThreshold}x");
                    }
                    
                    int totalBlock = Mathf.FloorToInt(totalBlockBeforeEnhancements * blockMultiplier);
                    
                    if (totalBlock > 0) 
                    {
                        playerManager.AddBlockToOpponentPet(totalBlock);
                        Debug.Log($"Applied {totalBlock} block to Opponent Pet. (Base: {baseBlock}, TurnB: {turnBonusBlock}, PlayB: {playBonusBlock}, CopyB: {copyBonusBlock}, Multiplier: {blockMultiplier:F2}). Est. Block: {playerManager.GetOpponentPetBlock()}");
                    }
                }
                
                // Healing & Temp Max HP for Enemy Pet
                if (cardData.healingAmount > 0)
                {
                    playerManager.HealOpponentPet(cardData.healingAmount);
                }
                
                // Apply Status Effect to Enemy Pet
                if (cardData.statusToApply != StatusEffectType.None)
                {
                    int valueToApply = 0;
                    switch (cardData.statusToApply) {
                        case StatusEffectType.Thorns: valueToApply = cardData.thornsAmount; break;
                        case StatusEffectType.Strength: valueToApply = cardData.strengthAmount; break;
                        default: valueToApply = cardData.statusDuration; break; // Weak, Break use duration
                    }

                    if (valueToApply > 0) {
                        playerManager.ApplyStatusEffectOpponentPet(cardData.statusToApply, valueToApply);
                    } else {
                         Debug.LogWarning($"Card {cardData.cardName} has {cardData.statusToApply} status but its corresponding amount (thornsAmount/strengthAmount/statusDuration) is 0.");
                    }
                }
                
                // Apply DoT to Enemy Pet
                if (cardData.dotDamageAmount > 0 && cardData.dotDuration > 0)
                {
                    playerManager.ApplyDotOpponentPet(cardData.dotDamageAmount, cardData.dotDuration);
                }
                
                // Apply HoT to Enemy Pet
                if (cardData.hotAmount > 0 && cardData.hotDuration > 0)
                {
                    playerManager.ApplyHotOpponentPet(cardData.hotAmount, cardData.hotDuration);
                }
                
                // --- MOVED/ADDED: Apply Energy Gain to Enemy Pet ---
                if (cardData.energyGain > 0)
                {
                    // Update local simulation and notify owner
                    gameManager.GetCardManager().GetPetDeckManager().AddEnergyToOpponentPet(cardData.energyGain);
                    Debug.Log($"Applied {cardData.energyGain} energy to Opponent Pet (local sim).");
                }
                // --- END MOVED/ADDED ---
                break;
                
            case CardDropZone.TargetType.PlayerSelf:
                // --- Damage Processing ---
                if (cardData.damage > 0 || cardData.damageScalingPerTurn > 0 || cardData.damageScalingPerPlay > 0 || cardData.damageScalingPerCopy > 0)
                {
                    int baseDamage = cardData.damage;
                    int turnBonusDamage = 0;
                    int playBonusDamage = 0;
                    int copyBonusDamage = 0;
                    
                    if (cardData.damageScalingPerTurn > 0) 
                    {
                        turnBonusDamage = Mathf.FloorToInt(cardData.damageScalingPerTurn * currentTurn);
                        Debug.Log($"Self-Damage Scaling (Turn): Base={baseDamage}, Scale/Turn={cardData.damageScalingPerTurn}, Turn={currentTurn}, Bonus={turnBonusDamage}");
                    }
                    if (cardData.damageScalingPerPlay > 0) 
                    {
                        playBonusDamage = Mathf.FloorToInt(cardData.damageScalingPerPlay * previousPlaysThisCombat);
                        Debug.Log($"Self-Damage Scaling (Play): Base={baseDamage}, Scale/Play={cardData.damageScalingPerPlay}, PrevPlays={previousPlaysThisCombat}, Bonus={playBonusDamage}");
                    }
                    if (cardData.damageScalingPerCopy > 0)
                    {
                        copyBonusDamage = Mathf.FloorToInt(cardData.damageScalingPerCopy * totalCopies);
                         Debug.Log($"Self-Damage Scaling (Copy): Base={baseDamage}, Scale/Copy={cardData.damageScalingPerCopy}, Copies={totalCopies}, Bonus={copyBonusDamage}");
                    }
                    int totalDamageBeforeEnhancements = baseDamage + turnBonusDamage + playBonusDamage + copyBonusDamage;
                    float damageMultiplier = 1.0f;
                    
                    // Apply Low Health Multiplier
                    if (isBelowHealthThreshold && cardData.damageMultiplierBelowThreshold > 1.0f) {
                        damageMultiplier *= cardData.damageMultiplierBelowThreshold;
                        Debug.Log($"Applying low health self-damage multiplier: {cardData.damageMultiplierBelowThreshold}x");
                    }
                    
                    int damageAfterMultipliers = Mathf.FloorToInt(totalDamageBeforeEnhancements * damageMultiplier);
                    
                    // Apply Player Strength Bonus
                    int strengthBonus = playerManager.GetPlayerStrength();
                    if (strengthBonus != 0) {
                        damageAfterMultipliers += strengthBonus;
                        Debug.Log($"Applying Player Strength Bonus to Self-Damage: +{strengthBonus}");
                    }
                    
                    int totalDamageBeforeWeak = damageAfterMultipliers;
                    int actualDamage = totalDamageBeforeWeak;
                    
                    // Check Attacker (Player) Weakness
                    if (playerManager.IsPlayerWeak()) {
                        int reduction = Mathf.FloorToInt(actualDamage * 0.5f);
                        actualDamage = Mathf.Max(0, actualDamage - reduction);
                        Debug.Log($"Player is Weak! Reducing self-damage from {totalDamageBeforeWeak} to {actualDamage} (-50%)");
                    }

                    if (actualDamage > 0)
                    {
                        playerManager.DamageLocalPlayer(actualDamage);
                        Debug.Log($"Dealt {actualDamage} damage to PlayerSelf. (Base: {baseDamage}, TurnB: {turnBonusDamage}, PlayB: {playBonusDamage}, CopyB: {copyBonusDamage}, StrB: {strengthBonus}, Multiplier: {damageMultiplier:F2}). New health: {playerManager.GetLocalPlayerHealth()}, Block: {playerManager.GetLocalPlayerBlock()}");
                    }
                }
                
                // --- Block Processing ---
                if (cardData.block > 0 || cardData.blockScalingPerTurn > 0 || cardData.blockScalingPerPlay > 0 || cardData.blockScalingPerCopy > 0)
                {
                    int baseBlock = cardData.block;
                    int turnBonusBlock = 0;
                    int playBonusBlock = 0;
                    int copyBonusBlock = 0;
                    
                    if (cardData.blockScalingPerTurn > 0)
                    {
                        turnBonusBlock = Mathf.FloorToInt(cardData.blockScalingPerTurn * currentTurn);
                         Debug.Log($"Block Scaling (Turn): Base={baseBlock}, Scale/Turn={cardData.blockScalingPerTurn}, Turn={currentTurn}, Bonus={turnBonusBlock}");
                    }
                     if (cardData.blockScalingPerPlay > 0) 
                    {
                        playBonusBlock = Mathf.FloorToInt(cardData.blockScalingPerPlay * previousPlaysThisCombat);
                        Debug.Log($"Block Scaling (Play): Base={baseBlock}, Scale/Play={cardData.blockScalingPerPlay}, PrevPlays={previousPlaysThisCombat}, Bonus={playBonusBlock}");
                    }
                    if (cardData.blockScalingPerCopy > 0)
                    {
                         copyBonusBlock = Mathf.FloorToInt(cardData.blockScalingPerCopy * totalCopies);
                         Debug.Log($"Block Scaling (Copy): Base={baseBlock}, Scale/Copy={cardData.blockScalingPerCopy}, Copies={totalCopies}, Bonus={copyBonusBlock}");
                    }
                    int totalBlockBeforeEnhancements = baseBlock + turnBonusBlock + playBonusBlock + copyBonusBlock;
                    float blockMultiplier = 1.0f;
                    
                    // Apply Low Health Multiplier
                    if (isBelowHealthThreshold && cardData.blockMultiplierBelowThreshold > 1.0f) {
                        blockMultiplier *= cardData.blockMultiplierBelowThreshold;
                        Debug.Log($"Applying low health block multiplier: {cardData.blockMultiplierBelowThreshold}x");
                    }
                    
                    int totalBlock = Mathf.FloorToInt(totalBlockBeforeEnhancements * blockMultiplier);
                    
                    if (totalBlock > 0)
                    {
                        playerManager.AddBlockToLocalPlayer(totalBlock);
                         Debug.Log($"Applied {totalBlock} block to PlayerSelf. (Base: {baseBlock}, TurnB: {turnBonusBlock}, PlayB: {playBonusBlock}, CopyB: {copyBonusBlock}, Multiplier: {blockMultiplier:F2}).");
                    }
                }
                
                // Healing & Temp Max HP for Player
                if (cardData.healingAmount > 0)
                {
                    playerManager.HealLocalPlayer(cardData.healingAmount);
                }
                
                // Apply Status Effect to Player
                if (cardData.statusToApply != StatusEffectType.None)
                {
                    int valueToApply = 0;
                    switch (cardData.statusToApply) {
                        case StatusEffectType.Thorns: valueToApply = cardData.thornsAmount; break;
                        case StatusEffectType.Strength: valueToApply = cardData.strengthAmount; break;
                        default: valueToApply = cardData.statusDuration; break; // Weak, Break use duration
                    }

                    if (valueToApply > 0) {
                        playerManager.ApplyStatusEffectLocalPlayer(cardData.statusToApply, valueToApply);
                    } else {
                         Debug.LogWarning($"Card {cardData.cardName} has {cardData.statusToApply} status but its corresponding amount (thornsAmount/strengthAmount/statusDuration) is 0.");
                    }
                }
                
                // Apply DoT to Player
                if (cardData.dotDamageAmount > 0 && cardData.dotDuration > 0)
                {
                    playerManager.ApplyDotLocalPlayer(cardData.dotDamageAmount, cardData.dotDuration);
                }
                
                // Apply HoT to Player
                if (cardData.hotAmount > 0 && cardData.hotDuration > 0)
                {
                    playerManager.ApplyHotLocalPlayer(cardData.hotAmount, cardData.hotDuration);
                }
                
                // --- MOVED: Apply Energy Gain to Player ---
                if (cardData.energyGain > 0)
                {
                    playerManager.GainEnergy(cardData.energyGain);
                }
                // --- END MOVED ---
                break;
                
            case CardDropZone.TargetType.OwnPet:
                // --- Damage Processing ---
                if (cardData.damage > 0 || cardData.damageScalingPerTurn > 0 || cardData.damageScalingPerPlay > 0 || cardData.damageScalingPerCopy > 0)
                {
                    int baseDamage = cardData.damage;
                    int turnBonusDamage = 0;
                    int playBonusDamage = 0;
                    int copyBonusDamage = 0;
                    
                    if (cardData.damageScalingPerTurn > 0) 
                    {
                        turnBonusDamage = Mathf.FloorToInt(cardData.damageScalingPerTurn * currentTurn);
                        Debug.Log($"Pet Damage Scaling (Turn): Base={baseDamage}, Scale/Turn={cardData.damageScalingPerTurn}, Turn={currentTurn}, Bonus={turnBonusDamage}");
                    }
                     if (cardData.damageScalingPerPlay > 0) 
                    {
                        playBonusDamage = Mathf.FloorToInt(cardData.damageScalingPerPlay * previousPlaysThisCombat);
                        Debug.Log($"Pet Damage Scaling (Play): Base={baseDamage}, Scale/Play={cardData.damageScalingPerPlay}, PrevPlays={previousPlaysThisCombat}, Bonus={playBonusDamage}");
                    }
                    if (cardData.damageScalingPerCopy > 0)
                    {
                         copyBonusDamage = Mathf.FloorToInt(cardData.damageScalingPerCopy * totalCopies);
                         Debug.Log($"Pet Damage Scaling (Copy): Base={baseDamage}, Scale/Copy={cardData.damageScalingPerCopy}, Copies={totalCopies}, Bonus={copyBonusDamage}");
                    }
                    int totalDamageBeforeEnhancements = baseDamage + turnBonusDamage + playBonusDamage + copyBonusDamage;
                    float damageMultiplier = 1.0f;
                    
                    // Apply Low Health Multiplier
                    if (isBelowHealthThreshold && cardData.damageMultiplierBelowThreshold > 1.0f) {
                        damageMultiplier *= cardData.damageMultiplierBelowThreshold;
                        Debug.Log($"Applying low health pet damage multiplier: {cardData.damageMultiplierBelowThreshold}x");
                    }
                    
                    int damageAfterMultipliers = Mathf.FloorToInt(totalDamageBeforeEnhancements * damageMultiplier);
                    
                    // Apply Player Strength Bonus
                    int strengthBonus = playerManager.GetPlayerStrength();
                    if (strengthBonus != 0) {
                        damageAfterMultipliers += strengthBonus;
                        Debug.Log($"Applying Player Strength Bonus to Pet Damage: +{strengthBonus}");
                    }
                    
                    int totalDamageBeforeWeak = damageAfterMultipliers;
                    int actualDamage = totalDamageBeforeWeak;
                    
                    // Check Attacker (Player) Weakness
                    if (playerManager.IsPlayerWeak()) {
                        int reduction = Mathf.FloorToInt(actualDamage * 0.5f);
                        actualDamage = Mathf.Max(0, actualDamage - reduction);
                        Debug.Log($"Player is Weak! Reducing pet damage from {totalDamageBeforeWeak} to {actualDamage} (-50%)");
                    }
                    
                    if (actualDamage > 0)
                    {
                        playerManager.DamageLocalPet(actualDamage);
                        int finalPetHealth = playerManager.GetLocalPetHealth(); // Get health AFTER damage
                        Debug.Log($"Dealt {actualDamage} damage to OwnPet. (Base: {baseDamage}, TurnB: {turnBonusDamage}, PlayB: {playBonusDamage}, CopyB: {copyBonusDamage}, StrB: {strengthBonus}, Multiplier: {damageMultiplier:F2}). New health: {finalPetHealth}, Block: {playerManager.GetLocalPetBlock()}");

                        // --- MODIFIED: Notify opponent about the NEW health total --- 
                        Photon.Realtime.Player opponentPlayer = gameManager.GetPlayerManager().GetOpponentPlayer();
                        if (opponentPlayer != null)
                        {
                            Debug.Log($"Sending RpcNotifyOpponentOfLocalPetHealthChange({finalPetHealth}) to opponent {opponentPlayer.NickName} after local pet took damage.");
                            gameManager.GetPhotonView().RPC("RpcNotifyOpponentOfLocalPetHealthChange", opponentPlayer, finalPetHealth);
                        }
                        else
                        {
                            Debug.LogWarning("CardEffectService (OwnPet Damage): Could not find opponent player to notify about pet health change.");
                        }
                        // --- END MODIFIED ---
                    }
                }
                
                // --- Block Processing ---
                if (cardData.block > 0 || cardData.blockScalingPerTurn > 0 || cardData.blockScalingPerPlay > 0 || cardData.blockScalingPerCopy > 0)
                {
                    int baseBlock = cardData.block;
                    int turnBonusBlock = 0;
                    int playBonusBlock = 0;
                    int copyBonusBlock = 0;
                    
                    if (cardData.blockScalingPerTurn > 0)
                    {
                        turnBonusBlock = Mathf.FloorToInt(cardData.blockScalingPerTurn * currentTurn);
                        Debug.Log($"Pet Block Scaling (Turn): Base={baseBlock}, Scale/Turn={cardData.blockScalingPerTurn}, Turn={currentTurn}, Bonus={turnBonusBlock}");
                    }
                     if (cardData.blockScalingPerPlay > 0) 
                    {
                        playBonusBlock = Mathf.FloorToInt(cardData.blockScalingPerPlay * previousPlaysThisCombat);
                        Debug.Log($"Pet Block Scaling (Play): Base={baseBlock}, Scale/Play={cardData.blockScalingPerPlay}, PrevPlays={previousPlaysThisCombat}, Bonus={playBonusBlock}");
                    }
                    if (cardData.blockScalingPerCopy > 0)
                    {
                        copyBonusBlock = Mathf.FloorToInt(cardData.blockScalingPerCopy * totalCopies);
                        Debug.Log($"Pet Block Scaling (Copy): Base={baseBlock}, Scale/Copy={cardData.blockScalingPerCopy}, Copies={totalCopies}, Bonus={copyBonusBlock}");
                    }
                    int totalBlockBeforeEnhancements = baseBlock + turnBonusBlock + playBonusBlock + copyBonusBlock;
                    float blockMultiplier = 1.0f;
                    
                    // Apply Low Health Multiplier
                    if (isBelowHealthThreshold && cardData.blockMultiplierBelowThreshold > 1.0f) {
                        blockMultiplier *= cardData.blockMultiplierBelowThreshold;
                         Debug.Log($"Applying low health pet block multiplier: {cardData.blockMultiplierBelowThreshold}x");
                    }
                    
                    int totalBlock = Mathf.FloorToInt(totalBlockBeforeEnhancements * blockMultiplier);
                    
                    if (totalBlock > 0)
                    {
                        playerManager.AddBlockToLocalPet(totalBlock);
                        Debug.Log($"Applied {totalBlock} block to OwnPet. (Base: {baseBlock}, TurnB: {turnBonusBlock}, PlayB: {playBonusBlock}, CopyB: {copyBonusBlock}, Multiplier: {blockMultiplier:F2}).");
                    }
                }
                
                // Healing & Temp Max HP for Own Pet
                if (cardData.healingAmount > 0)
                {
                    playerManager.HealLocalPet(cardData.healingAmount);
                    int finalPetHealth = playerManager.GetLocalPetHealth(); // Get health AFTER healing
                    Debug.Log($"Healed OwnPet by {cardData.healingAmount}. New health: {finalPetHealth}");
                }
                
                // Apply Status Effect to Own Pet
                if (cardData.statusToApply != StatusEffectType.None)
                {
                     int valueToApply = 0;
                    switch (cardData.statusToApply) {
                        case StatusEffectType.Thorns: valueToApply = cardData.thornsAmount; break;
                        case StatusEffectType.Strength: valueToApply = cardData.strengthAmount; break;
                        default: valueToApply = cardData.statusDuration; break; // Weak, Break use duration
                    }

                    if (valueToApply > 0) {
                        playerManager.ApplyStatusEffectLocalPet(cardData.statusToApply, valueToApply);
                    } else {
                         Debug.LogWarning($"Card {cardData.cardName} has {cardData.statusToApply} status but its corresponding amount (thornsAmount/strengthAmount/statusDuration) is 0.");
                    }
                }
                
                // Apply DoT to Own Pet
                if (cardData.dotDamageAmount > 0 && cardData.dotDuration > 0)
                {
                    playerManager.ApplyDotLocalPet(cardData.dotDamageAmount, cardData.dotDuration);
                }
                
                // Apply HoT to Own Pet
                if (cardData.hotAmount > 0 && cardData.hotDuration > 0)
                {
                    playerManager.ApplyHotLocalPet(cardData.hotAmount, cardData.hotDuration);
                }

                // --- ADDED: Apply Energy Gain to Own Pet --- 
                if (cardData.energyGain > 0)
                {
                    Debug.Log($"Applying {cardData.energyGain} energy to Own Pet.");
                    // Update the custom property directly, triggering UI update via OnPlayerPropertiesUpdate
                    int currentPetEnergy = gameManager.GetStartingPetEnergy(); // Start with base
                    if (PhotonNetwork.LocalPlayer.CustomProperties.TryGetValue(CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP, out object energyObj))
                    {
                        try { currentPetEnergy = (int)energyObj; } catch {}
                    }
                    int newPetEnergy = currentPetEnergy + cardData.energyGain;
                    ExitGames.Client.Photon.Hashtable petEnergyProp = new ExitGames.Client.Photon.Hashtable
                    {
                        { CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP, newPetEnergy }
                    };
                    PhotonNetwork.LocalPlayer.SetCustomProperties(petEnergyProp);
                }
                // --- END ADDED ---
                break;
        }
        
        // Apply target-independent effects
        
        // Apply Draw Card
        if (cardData.drawAmount > 0)
        {
            // --- MODIFIED: Draw cards based on target type ---
            if (targetType == CardDropZone.TargetType.PlayerSelf) 
            {
                // Draw cards for the player
                Debug.Log($"Drawing {cardData.drawAmount} cards for player.");
                DeckManager deckManager = gameManager.GetCardManager().GetDeckManager();
                for(int d=0; d < cardData.drawAmount; d++)
                {
                    deckManager.DrawCard();
                }
                gameManager.UpdateHandUI();
                gameManager.UpdateDeckCountUI();
            }
            else if (targetType == CardDropZone.TargetType.OwnPet)
            {
                // Draw cards for own pet
                Debug.Log($"Drawing {cardData.drawAmount} cards for own pet.");
                // TODO: Implement pet card drawing if not already implemented
                // This would require adding methods to PetDeckManager for the local pet
                
                // Notify owner this is not implemented yet
                Debug.LogWarning("Drawing cards for own pet is not fully implemented yet.");
            }
            else if (targetType == CardDropZone.TargetType.EnemyPet)
            {
                // Draw cards for enemy pet
                Debug.Log($"Drawing {cardData.drawAmount} cards for opponent's pet.");
                PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
                for(int d=0; d < cardData.drawAmount; d++)
                {
                    petDeckManager.DrawOpponentPetCard();
                }
                // Update opponent pet hand UI
                gameManager.GetCombatUIManager()?.UpdateOpponentPetHandUI();
            }
            // --- END MODIFIED ---
        }
        
        // Apply Discard Random
        if (cardData.discardRandomAmount > 0)
        {
            // --- MODIFIED: Discard cards based on target type ---
            if (targetType == CardDropZone.TargetType.PlayerSelf)
            {
                // Discard random cards from player's hand
                Debug.Log($"Discarding {cardData.discardRandomAmount} random cards from player's hand.");
                List<CardData> discarded = gameManager.GetCardManager().GetDeckManager().DiscardRandomCards(cardData.discardRandomAmount, targetType);
                // Animation is triggered within DiscardRandomCards, but we need to update UI after.
                if (discarded.Count > 0)
                {
                    gameManager.UpdateHandUI(); // Update hand if any cards were actually discarded
                    gameManager.UpdateDeckCountUI();
                }
            }
            else if (targetType == CardDropZone.TargetType.OwnPet)
            {
                // Discard random cards from own pet's hand
                Debug.Log($"Discarding {cardData.discardRandomAmount} random cards from own pet's hand.");
                // TODO: Implement pet card discarding if not already implemented
                
                // Notify owner this is not implemented yet
                Debug.LogWarning("Discarding cards from own pet's hand is not fully implemented yet.");
            }
            else if (targetType == CardDropZone.TargetType.EnemyPet)
            {
                // Discard random cards from enemy pet's hand
                Debug.Log($"Discarding {cardData.discardRandomAmount} random cards from opponent pet's hand.");
                PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
                petDeckManager.DiscardRandomOpponentPetCards(cardData.discardRandomAmount);
                // Update opponent pet hand UI
                gameManager.GetCombatUIManager()?.UpdateOpponentPetHandUI();
            }
            // --- END MODIFIED ---
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
            // --- MODIFIED: Use critBuffRule to determine the actual recipient --- 
            CritBuffTargetRule rule = cardData.critBuffRule;
            Debug.Log($"Applying Crit Buff: Amount={cardData.critChanceBuffAmount}, Duration={cardData.critChanceBuffDuration}, Rule={rule}, DropTarget={targetType}"); 
            
            if (rule == CritBuffTargetRule.Player)
            {
                // Always apply to the player regardless of drop zone
                playerManager.ApplyCritChanceBuffPlayer(cardData.critChanceBuffAmount, cardData.critChanceBuffDuration);
            }
            else if (rule == CritBuffTargetRule.Target)
            {
                // Apply based on where the card was dropped (targetType)
                if (targetType == CardDropZone.TargetType.PlayerSelf)
                {
                    playerManager.ApplyCritChanceBuffPlayer(cardData.critChanceBuffAmount, cardData.critChanceBuffDuration);
                }
                else if (targetType == CardDropZone.TargetType.OwnPet)
                {
                    playerManager.ApplyCritChanceBuffPet(cardData.critChanceBuffAmount, cardData.critChanceBuffDuration);
                }
                else if (targetType == CardDropZone.TargetType.EnemyPet)
                {
                    // Note: HealthManager handles sending RPC if needed
                    playerManager.ApplyCritChanceBuffOpponentPet(cardData.critChanceBuffAmount, cardData.critChanceBuffDuration);
                }
                else 
                {
                    // Log a warning if the drop target isn't one of the direct entities
                    Debug.LogWarning($"CritBuffRule is Target, but drop target was {targetType}. Crit buff not applied.");
                }
            }
            // --- END MODIFIED ---
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
        Debug.Log($"[CardEffectService] Processing Opponent Pet Card: {cardToPlay.cardName}, Primary Target When Pet Plays: {cardToPlay.primaryTargetWhenPlayedByPet}");
        PlayerManager playerManager = gameManager.GetPlayerManager();
        
        // --- MODIFIED: Apply effects based on primaryTargetWhenPlayedByPet --- 
        OpponentPetTargetType target = cardToPlay.primaryTargetWhenPlayedByPet;
        
        // --- Apply Direct Damage ---
        if (cardToPlay.damage > 0)
        {
            int baseDamage = cardToPlay.damage;
            // --- ADDED: Apply Opponent Pet Strength Bonus --- 
            int strengthBonus = playerManager.GetOpponentPetStrength();
            if (strengthBonus != 0) {
                baseDamage += strengthBonus;
                 Debug.Log($"Applying Opponent Pet Strength Bonus: +{strengthBonus}");
            }
            // --- END ADDED ---
            int actualDamage = baseDamage;
            
            // Check Attacker (Pet) Weakness
            if (playerManager.IsOpponentPetWeak()) { 
                 int reduction = Mathf.FloorToInt(actualDamage * 0.5f);
                 actualDamage = Mathf.Max(0, actualDamage - reduction);
                 Debug.Log($"Opponent Pet is Weak! Reducing damage from {cardToPlay.damage} to {actualDamage} (-50%)");
            }
            
            if (target == OpponentPetTargetType.Player) 
            {
                playerManager.DamageLocalPlayer(actualDamage);
                Debug.Log($"Opponent Pet dealt {actualDamage} damage to Player. (Base: {cardToPlay.damage}, StrB: {strengthBonus}). Player Health: {playerManager.GetLocalPlayerHealth()}, Block: {playerManager.GetLocalPlayerBlock()}");
            }
            else if (target == OpponentPetTargetType.Self)
            {
                playerManager.DamageOpponentPet(actualDamage); // Call the standard method
                Debug.Log($"Opponent Pet dealt {actualDamage} damage to ITSELF. (Base: {cardToPlay.damage}, StrB: {strengthBonus}). Pet Health: {playerManager.GetOpponentPetHealth()}, Block: {playerManager.GetOpponentPetBlock()}");
            }
        }
        
        // --- Apply Block ---
        if (cardToPlay.block > 0)
        {
            if (target == OpponentPetTargetType.Self)
            {
                playerManager.AddBlockToOpponentPet(cardToPlay.block);
                Debug.Log($"Opponent Pet gained {cardToPlay.block} block. Est. Block: {playerManager.GetOpponentPetBlock()}");
            }
            else if (target == OpponentPetTargetType.Player)
            {
                playerManager.AddBlockToLocalPlayer(cardToPlay.block); // Does pet block player?
                 Debug.LogWarning($"Opponent Pet card {cardToPlay.cardName} applied block to Player? Block: {cardToPlay.block}");
            }
        }
        
        // --- Apply Healing ---
        if (cardToPlay.healingAmount > 0)
        {
             if (target == OpponentPetTargetType.Self)
             {
                playerManager.HealOpponentPet(cardToPlay.healingAmount);
                Debug.Log($"Opponent Pet healed itself for {cardToPlay.healingAmount}. Est. Health: {playerManager.GetOpponentPetHealth()}");
             }
             else if (target == OpponentPetTargetType.Player)
             {
                playerManager.HealLocalPlayer(cardToPlay.healingAmount); 
                 Debug.LogWarning($"Opponent Pet card {cardToPlay.cardName} healed Player? Heal: {cardToPlay.healingAmount}");
             }
        }
        
        // --- Apply Status Effect ---
        if (cardToPlay.statusToApply != StatusEffectType.None)
        { 
            int valueToApply = 0;
            switch (cardToPlay.statusToApply) {
                case StatusEffectType.Thorns: valueToApply = cardToPlay.thornsAmount; break;
                case StatusEffectType.Strength: valueToApply = cardToPlay.strengthAmount; break;
                default: valueToApply = cardToPlay.statusDuration; break; // Weak, Break use duration
            }

            if (valueToApply > 0) {
                if (target == OpponentPetTargetType.Player) {
                    playerManager.ApplyStatusEffectLocalPlayer(cardToPlay.statusToApply, valueToApply);
                    Debug.Log($"Opponent Pet applied {cardToPlay.statusToApply} ({valueToApply}) to Player.");
                }
                else if (target == OpponentPetTargetType.Self) {
                     playerManager.ApplyStatusEffectOpponentPet(cardToPlay.statusToApply, valueToApply);
                     Debug.Log($"Opponent Pet applied {cardToPlay.statusToApply} ({valueToApply}) to ITSELF.");
                }
            }
        }
        
        // --- Apply DoT ---
        if (cardToPlay.dotDamageAmount > 0 && cardToPlay.dotDuration > 0)
        { 
            if (target == OpponentPetTargetType.Player)
            {
                playerManager.ApplyDotLocalPlayer(cardToPlay.dotDamageAmount, cardToPlay.dotDuration);
                Debug.Log($"Opponent Pet applied DoT ({cardToPlay.dotDamageAmount}dmg/{cardToPlay.dotDuration}t) to Player.");
            }
            else if (target == OpponentPetTargetType.Self)
            {
                playerManager.ApplyDotOpponentPet(cardToPlay.dotDamageAmount, cardToPlay.dotDuration); // Call the standard method
                Debug.Log($"Opponent Pet applied DoT ({cardToPlay.dotDamageAmount}dmg/{cardToPlay.dotDuration}t) to ITSELF.");
            }
        }
        
        // --- Apply HoT ---
        if (cardToPlay.hotAmount > 0 && cardToPlay.hotDuration > 0)
        { 
            if (target == OpponentPetTargetType.Player)
            {
                playerManager.ApplyHotLocalPlayer(cardToPlay.hotAmount, cardToPlay.hotDuration);
                Debug.Log($"Opponent Pet applied HoT ({cardToPlay.hotAmount}heal/{cardToPlay.hotDuration}t) to Player.");
            }
            else if (target == OpponentPetTargetType.Self)
            {
                playerManager.ApplyHotOpponentPet(cardToPlay.hotAmount, cardToPlay.hotDuration); 
                Debug.Log($"Opponent Pet applied HoT ({cardToPlay.hotAmount}heal/{cardToPlay.hotDuration}t) to ITSELF.");
            }
        }
        
        // --- Apply Energy Gain (Always applies to the caster - the Opponent Pet) ---
        if (cardToPlay.energyGain > 0)
        {
            gameManager.GetCardManager().GetPetDeckManager().AddEnergyToOpponentPet(cardToPlay.energyGain);
            Debug.Log($"Opponent Pet gained {cardToPlay.energyGain} energy (local sim).");
        }
        
        // --- Apply Draw Card (Opponent Pet draws - simulated locally) ---
        if (cardToPlay.drawAmount > 0)
        {
            Debug.Log($"Opponent Pet draws {cardToPlay.drawAmount} cards (simulated).");
            PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
            for(int d=0; d < cardToPlay.drawAmount; d++)
            {
                petDeckManager.DrawOpponentPetCard();
            }
        }
        
        // --- Apply Discard Random (Opponent Pet discards - simulated locally) ---
        if (cardToPlay.discardRandomAmount > 0)
        {
             Debug.Log($"Opponent Pet discards {cardToPlay.discardRandomAmount} random cards (simulated).");
             gameManager.GetCardManager().GetPetDeckManager().DiscardRandomOpponentPetCards(cardToPlay.discardRandomAmount);
        }

        // --- Opponent Pet Combo Check (Simulated) ---
        // Note: Currently, pet AI doesn't track combo points. Add if needed.
        // if (cardToPlay.isComboStarter)
        // {
        //     // Increment opponent pet combo count (needs state tracking)
        //     // If combo triggers:
        //     // ApplyOpponentPetComboEffect(cardToPlay);
        // }
        
        // --- Other effects like Cost Modification, Crit Chance, Temp Upgrade --- 
        // These are less common for pet decks currently and would likely target the opponent (player) or the pet itself.
        // Implement logic based on primaryTarget if these effects are added to pet cards.
        // Example:
        // if (cardToPlay.costChangeTarget != CostChangeTargetType.None)
        // {
        //     if(target == CardDropZone.TargetType.EnemyPet) { /* Apply cost mod to Player Hand */ }
        // }

        // --- END MODIFIED LOGIC --- 

        // After effects, update UI
        gameManager.UpdateHealthUI();
        gameManager.GetCombatUIManager().UpdateStatusEffectsUI();
        // No need to update opponent hand/deck UI as it's not visible
    }
    
    public void HandleDiscardTrigger(CardData card, CardDropZone.TargetType targetType = CardDropZone.TargetType.PlayerSelf)
    {
        // Only process if card has a discard effect
        if (card.discardEffectType == DiscardEffectType.None) return;

        PlayerManager playerManager = gameManager.GetPlayerManager();
        int value = card.discardEffectValue;
        
        // --- MODIFIED: Switch logic to respect targetType ---
        switch (card.discardEffectType)
        {
            case DiscardEffectType.DealDamageToOpponentPet:
                // This effect always targets the opponent pet regardless of drop target
                playerManager.DamageOpponentPet(value);
                break;
            
            case DiscardEffectType.GainBlockPlayer:
                // Apply block based on target
                if (targetType == CardDropZone.TargetType.PlayerSelf)
                {
                    playerManager.AddBlockToLocalPlayer(value);
                    Debug.Log($"Discard Effect: Gained {value} block for player.");
                }
                else if (targetType == CardDropZone.TargetType.OwnPet)
                {
                    playerManager.AddBlockToLocalPet(value);
                    Debug.Log($"Discard Effect: Gained {value} block for own pet.");
                }
                else if (targetType == CardDropZone.TargetType.EnemyPet)
                {
                    // This would require new methods to add block to opponent pet
                    Debug.LogWarning($"Discard Effect: GainBlock on EnemyPet not implemented. Target was {targetType}");
                }
                break;
            
            case DiscardEffectType.GainBlockPet:
                // This effect always targets your pet
                playerManager.AddBlockToLocalPet(value);
                break;
            
            case DiscardEffectType.DrawCard:
                // Apply draw based on target
                if (targetType == CardDropZone.TargetType.PlayerSelf)
                {
                    Debug.Log($"Discard Effect: Drawing {value} cards for player.");
                    DeckManager deckManager = gameManager.GetCardManager().GetDeckManager();
                    for(int i=0; i < value; i++) deckManager.DrawCard();
                    gameManager.UpdateHandUI();
                    gameManager.UpdateDeckCountUI();
                }
                else if (targetType == CardDropZone.TargetType.OwnPet)
                {
                    Debug.Log($"Discard Effect: Drawing {value} cards for own pet is not yet implemented.");
                    // TODO: Implement drawing for own pet
                }
                else if (targetType == CardDropZone.TargetType.EnemyPet)
                {
                    Debug.Log($"Discard Effect: Drawing {value} cards for opponent's pet.");
                    PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
                    for(int i=0; i < value; i++) petDeckManager.DrawOpponentPetCard();
                    gameManager.GetCombatUIManager()?.UpdateOpponentPetHandUI();
                }
                break;
            
            case DiscardEffectType.GainEnergy:
                // Apply energy gain based on target
                if (targetType == CardDropZone.TargetType.PlayerSelf)
                {
                    playerManager.GainEnergy(value);
                    Debug.Log($"Discard Effect: Gained {value} energy for player.");
                }
                else if (targetType == CardDropZone.TargetType.OwnPet)
                {
                    // Add energy to own pet (would need additional method)
                    Debug.LogWarning($"Discard Effect: GainEnergy for own pet not implemented. Target was {targetType}");
                }
                else if (targetType == CardDropZone.TargetType.EnemyPet)
                {
                    // Add energy to opponent pet
                    PetDeckManager petDeckManager = gameManager.GetCardManager().GetPetDeckManager();
                    petDeckManager.AddEnergyToOpponentPet(value);
                    Debug.Log($"Discard Effect: Added {value} energy to opponent's pet.");
                }
                break;
        }
        // --- END MODIFIED ---
    }
    
    private void ApplyComboEffect(CardData triggeringCard)
    {
        PlayerManager playerManager = gameManager.GetPlayerManager();
        int value = triggeringCard.comboEffectValue;
        if (value <= 0) return; // No effect if value is zero or less

        CardDropZone.TargetType target = triggeringCard.comboEffectTarget;

        switch (triggeringCard.comboEffectType)
        {
            case ComboEffectType.DealDamage:
                // Apply damage based on target
                if (target == CardDropZone.TargetType.EnemyPet) {
                    playerManager.DamageOpponentPet(value);
                    Debug.Log($"Combo Effect: Dealt {value} damage to Opponent Pet.");
                }
                // Add cases for other potential damage targets if needed (e.g., PlayerSelf, OwnPet)
                else {
                     Debug.LogWarning($"Combo Effect: DealDamage targeted {target}, which is not implemented for this effect.");
                }
                break;

            case ComboEffectType.GainBlock:
                // Apply block based on target
                if (target == CardDropZone.TargetType.PlayerSelf) {
                    playerManager.AddBlockToLocalPlayer(value);
                     Debug.Log($"Combo Effect: Gained {value} block for PlayerSelf.");
                }
                else if (target == CardDropZone.TargetType.OwnPet) {
                    playerManager.AddBlockToLocalPet(value);
                    Debug.Log($"Combo Effect: Gained {value} block for OwnPet.");
                }
                // Add case for EnemyPet if block can target them via combo
                // else if (target == CardDropZone.TargetType.EnemyPet) {
                //     playerManager.AddBlockToOpponentPet(value);
                // }
                else {
                     Debug.LogWarning($"Combo Effect: GainBlock targeted {target}, which is not implemented for this effect.");
                }
                break;

            case ComboEffectType.DrawCard:
                // DrawCard implicitly targets the player
                DeckManager deckManager = gameManager.GetCardManager().GetDeckManager();
                for(int i=0; i < value; i++) deckManager.DrawCard();
                gameManager.UpdateHandUI();
                gameManager.UpdateDeckCountUI();
                Debug.Log($"Combo Effect: Drew {value} cards.");
                break;

            case ComboEffectType.GainEnergy:
                // GainEnergy implicitly targets the player
                playerManager.GainEnergy(value);
                 Debug.Log($"Combo Effect: Gained {value} energy.");
                break;

            case ComboEffectType.None:
            default:
                 Debug.LogWarning($"Combo Effect: Triggered with type {triggeringCard.comboEffectType}, but no action defined.");
                break;
        }
    }

    // --- MODIFIED: Helper method to visualize target effects --- 
    private void PlayTargetEffectPrefab(CardDropZone.TargetType targetType, GameObject effectPrefab)
    {
        if (effectPrefab == null || gameManager == null || effectsCanvasTransform == null || effectsCanvasCamera == null) 
        {
            Debug.LogError("Cannot play target effect: Missing dependencies (prefab, gameManager, effectsCanvas, or effectsCamera).");
            return; // Missing dependencies
        }

        // Find target transform based on type (UI element)
        Transform targetTransform = null;
        CombatUIManager uiManager = gameManager.GetCombatUIManager();
        if (uiManager == null) return; // Need UI Manager for positions

        switch (targetType)
        {
            case CardDropZone.TargetType.EnemyPet:
                // Find the opponent pet's UI element (e.g., health bar or representation)
                // This requires access to UI elements, ideally via CombatUIManager
                GameObject oppPetUI = uiManager.GetOpponentPetUIArea(); // Assuming such a getter exists or can be added
                if (oppPetUI != null) targetTransform = oppPetUI.transform;
                break;
            case CardDropZone.TargetType.PlayerSelf:
                GameObject playerUI = uiManager.GetPlayerUIArea(); // Assuming such a getter exists
                if (playerUI != null) targetTransform = playerUI.transform;
                break;
            case CardDropZone.TargetType.OwnPet:
                 GameObject ownPetUI = uiManager.GetOwnPetUIArea(); // Assuming such a getter exists
                if (ownPetUI != null) targetTransform = ownPetUI.transform;
                break;
        }

        if (targetTransform != null)
        {
            // Calculate screen position of the UI target
            // For Overlay canvas, the transform.position is often close enough to screen pos,
            // but using WorldToScreenPoint is safer if anchors/pivots are complex.
            // We need the *Combat UI* canvas's camera (or null for overlay) here.
            // --- MODIFIED: Get canvas from root instance ---
            GameObject uiRoot = uiManager?.GetCombatRootInstance();
            Canvas uiCanvas = uiRoot?.GetComponentInParent<Canvas>();
            // --- END MODIFIED ---
            Camera uiCamera = (uiCanvas?.renderMode == RenderMode.ScreenSpaceOverlay) ? null : uiCanvas?.worldCamera;
            Vector2 screenPos = RectTransformUtility.WorldToScreenPoint(uiCamera, targetTransform.position);

            // Convert screen position to world position on the Effects Canvas plane
            Vector3 worldPos = effectsCanvasCamera.ScreenToWorldPoint(new Vector3(screenPos.x, screenPos.y, effectsCanvasPlaneDistance));
            
            // Instantiate under the *Effects Canvas* and set world position
            gameManager.StartCoroutine(InstantiateAndDestroyEffect(effectPrefab, worldPos, effectsCanvasTransform)); 
        }
        else
        {
            Debug.LogWarning($"PlayTargetEffectPrefab: Could not find target transform for {targetType}");
        }
    }

    private IEnumerator InstantiateAndDestroyEffect(GameObject prefab, Vector3 worldPosition, Transform parentTransform)
    {
        // Create a temporary GameObject by instantiating the prefab under the correct parent (Effects Canvas)
        GameObject effectGO = Object.Instantiate(prefab, parentTransform); 
        effectGO.transform.position = worldPosition; // Set world position
        
        // --- ADDED: Attempt to get Animator and animation length --- 
        Animator animator = effectGO.GetComponent<Animator>();
        float duration = 1.0f; // Default duration if animator/clip not found
        
        if (animator != null && animator.runtimeAnimatorController != null && animator.runtimeAnimatorController.animationClips.Length > 0)
        {
            // Assuming the first animation clip is the one we want the length of
            duration = animator.runtimeAnimatorController.animationClips[0].length;
            Debug.Log($"Target Effect: Found animation clip, duration: {duration}s");
        }
        else
        {
             Debug.LogWarning("Target Effect: Animator or Animation Clip not found on prefab. Using default duration.");
        }
        // --- END ADDED ---

        // Wait for the determined duration
        yield return new WaitForSeconds(duration);

        // Cleanup - Destroy the instantiated effect object
        if (effectGO != null) // Check if it wasn't destroyed elsewhere
        {
            Object.Destroy(effectGO);
        }
        // yield return null; // Keep the coroutine running for one frame to ensure instantiation happens
    }
    // --- END MODIFIED ---
}