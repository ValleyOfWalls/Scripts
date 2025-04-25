using UnityEngine;
using System.Collections.Generic;
using Photon.Pun;
using Photon.Realtime;
using ExitGames.Client.Photon;

/// <summary>
/// Represents a combat entity (player or pet) with standardized state and behavior.
/// Centralizes health, block, status effects, energy and other combat attributes.
/// </summary>
public class CombatEntity
{
    // Core references
    private GameManager gameManager;
    private string entityName;
    private EntityType entityType;
    
    // Basic properties
    private int currentHealth;
    private int maxHealth;
    private int block;
    private int energy;
    private int maxEnergy;
    
    // Status effects
    private int weakTurns;
    private int breakTurns;
    private int thorns;
    private int strength;
    
    // DoT and HoT effects
    private int dotDamage;
    private int dotTurns;
    private int hotAmount;
    private int hotTurns;
    
    // Crit effects
    private List<CritBuff> critBuffs = new List<CritBuff>();
    
    // Network properties
    private Player ownerPlayer;
    private bool isLocallyOwned;
    private Dictionary<string, string> propertyMappings = new Dictionary<string, string>();
    
    public enum EntityType
    {
        LocalPlayer,
        LocalPet,
        OpponentPet
    }
    
    private struct CritBuff
    {
        public int amount;
        public int turns;
    }
    
    public CombatEntity(GameManager gameManager, EntityType entityType, string name, int maxHealth, int maxEnergy, Player ownerPlayer)
    {
        this.gameManager = gameManager;
        this.entityType = entityType;
        this.entityName = name;
        this.maxHealth = maxHealth;
        this.currentHealth = maxHealth;
        this.maxEnergy = maxEnergy;
        this.energy = maxEnergy;
        this.ownerPlayer = ownerPlayer;
        this.isLocallyOwned = ownerPlayer == PhotonNetwork.LocalPlayer;
        
        InitializePropertyMappings();
    }
    
    private void InitializePropertyMappings()
    {
        switch (entityType)
        {
            case EntityType.LocalPlayer:
                propertyMappings.Add("Health", CombatStateManager.PLAYER_COMBAT_PLAYER_HP_PROP);
                propertyMappings.Add("Block", CombatStateManager.PLAYER_COMBAT_PLAYER_BLOCK_PROP);
                propertyMappings.Add("Energy", CombatStateManager.PLAYER_COMBAT_ENERGY_PROP);
                propertyMappings.Add("WeakTurns", CombatStateManager.PLAYER_COMBAT_PLAYER_WEAK_PROP);
                propertyMappings.Add("BreakTurns", CombatStateManager.PLAYER_COMBAT_PLAYER_BREAK_PROP);
                propertyMappings.Add("Thorns", CombatStateManager.PLAYER_COMBAT_PLAYER_THORNS_PROP);
                propertyMappings.Add("Strength", CombatStateManager.PLAYER_COMBAT_PLAYER_STRENGTH_PROP);
                propertyMappings.Add("DotTurns", CombatStateManager.PLAYER_COMBAT_PLAYER_DOT_TURNS_PROP);
                propertyMappings.Add("DotDamage", CombatStateManager.PLAYER_COMBAT_PLAYER_DOT_DMG_PROP);
                propertyMappings.Add("HotTurns", CombatStateManager.PLAYER_COMBAT_PLAYER_HOT_TURNS_PROP);
                propertyMappings.Add("HotAmount", CombatStateManager.PLAYER_COMBAT_PLAYER_HOT_AMT_PROP);
                propertyMappings.Add("CritChance", CombatStateManager.PLAYER_COMBAT_PLAYER_CRIT_PROP);
                break;
                
            case EntityType.LocalPet:
                propertyMappings.Add("Health", CombatStateManager.PLAYER_COMBAT_PET_HP_PROP);
                propertyMappings.Add("Energy", CombatStateManager.PLAYER_COMBAT_PET_ENERGY_PROP);
                propertyMappings.Add("WeakTurns", CombatStateManager.PLAYER_COMBAT_PET_WEAK_PROP);
                propertyMappings.Add("BreakTurns", CombatStateManager.PLAYER_COMBAT_PET_BREAK_PROP);
                propertyMappings.Add("Thorns", CombatStateManager.PLAYER_COMBAT_PET_THORNS_PROP);
                propertyMappings.Add("Strength", CombatStateManager.PLAYER_COMBAT_PET_STRENGTH_PROP);
                propertyMappings.Add("DotTurns", CombatStateManager.PLAYER_COMBAT_PET_DOT_TURNS_PROP);
                propertyMappings.Add("DotDamage", CombatStateManager.PLAYER_COMBAT_PET_DOT_DMG_PROP);
                propertyMappings.Add("HotTurns", CombatStateManager.PLAYER_COMBAT_PET_HOT_TURNS_PROP);
                propertyMappings.Add("HotAmount", CombatStateManager.PLAYER_COMBAT_PET_HOT_AMT_PROP);
                propertyMappings.Add("CritChance", CombatStateManager.PLAYER_COMBAT_PET_CRIT_PROP);
                break;
                
            case EntityType.OpponentPet:
                // This is a remote entity; we don't set properties directly but receive them from the network
                break;
        }
    }
    
    // Health & Block Methods
    
    public void TakeDamage(int amount, bool isCritical = false)
    {
        // Calculate how much damage is blocked
        int blockedAmount = Mathf.Min(amount, block);
        block -= blockedAmount;
        
        // Apply remaining damage to health
        int damageAfterBlock = amount - blockedAmount;
        if (damageAfterBlock > 0)
        {
            currentHealth -= damageAfterBlock;
            if (currentHealth < 0) currentHealth = 0;
        }
        
        // Show damage number
        if (damageAfterBlock > 0)
        {
            GameObject targetArea = GetUIAreaForEntity();
            if (targetArea != null)
            {
                gameManager.GetDamageNumberManager()?.ShowDamageNumber(damageAfterBlock, targetArea, isCritical);
            }
        }
        
        // Sync changes to network
        SyncToNetwork("Health", currentHealth);
        SyncToNetwork("Block", block);
        
        // Update UI
        gameManager.UpdateHealthUI();
        
        // Check for combat end conditions
        if (currentHealth <= 0)
        {
            HandleDeathCondition();
        }
    }
    
    public void AddBlock(int amount)
    {
        if (amount <= 0) return;
        
        block += amount;
        Debug.Log($"Added {amount} block to {entityName}. Total: {block}");
        
        // Sync changes to network
        SyncToNetwork("Block", block);
        
        // Update UI
        gameManager.UpdateHealthUI();
    }
    
    public void Heal(int amount)
    {
        if (amount <= 0 || currentHealth >= maxHealth) return;
        
        int healAmount = Mathf.Min(amount, maxHealth - currentHealth);
        currentHealth += healAmount;
        
        Debug.Log($"Healed {entityName} for {healAmount}. New health: {currentHealth}/{maxHealth}");
        
        // Show heal number
        GameObject targetArea = GetUIAreaForEntity();
        if (targetArea != null && healAmount > 0)
        {
            gameManager.GetDamageNumberManager()?.ShowHealNumber(healAmount, targetArea);
        }
        
        // Sync changes to network
        SyncToNetwork("Health", currentHealth);
        
        // Update UI
        gameManager.UpdateHealthUI();
    }
    
    public void ResetBlock()
    {
        if (block == 0) return;
        
        block = 0;
        Debug.Log($"Reset block for {entityName}");
        
        // Sync changes to network
        SyncToNetwork("Block", block);
        
        // Update UI
        gameManager.UpdateHealthUI();
    }
    
    // Status Effect Methods
    
    public void ApplyStatusEffect(StatusEffectType type, int value)
    {
        switch (type)
        {
            case StatusEffectType.Weak:
                weakTurns += value;
                SyncToNetwork("WeakTurns", weakTurns);
                Debug.Log($"Applied Weak to {entityName} for {value} turns. Total: {weakTurns}");
                break;
                
            case StatusEffectType.Break:
                breakTurns += value;
                SyncToNetwork("BreakTurns", breakTurns);
                Debug.Log($"Applied Break to {entityName} for {value} turns. Total: {breakTurns}");
                break;
                
            case StatusEffectType.Thorns:
                thorns += value;
                SyncToNetwork("Thorns", thorns);
                Debug.Log($"Applied {value} Thorns to {entityName}. Total: {thorns}");
                break;
                
            case StatusEffectType.Strength:
                strength += value;
                SyncToNetwork("Strength", strength);
                Debug.Log($"Applied {value} Strength to {entityName}. Total: {strength}");
                break;
                
            case StatusEffectType.Regeneration:
                ApplyHoT(value, 3); // Example: 3 turns of regen
                break;
        }
        
        // Update UI
        gameManager.UpdateHealthUI();
    }
    
    public void ApplyDoT(int damage, int duration)
    {
        if (damage <= 0 || duration <= 0) return;
        
        dotTurns += duration;
        dotDamage = damage; // Usually overwrites with new damage value
        
        Debug.Log($"Applied DoT to {entityName}: {damage} damage for {duration} turns. Total turns: {dotTurns}");
        
        // Sync to network
        SyncToNetwork("DotTurns", dotTurns);
        SyncToNetwork("DotDamage", dotDamage);
        
        // Update UI
        gameManager.UpdateHealthUI();
    }
    
    public void ApplyHoT(int amount, int duration)
    {
        if (amount <= 0 || duration <= 0) return;
        
        hotTurns += duration;
        hotAmount = amount; // Usually overwrites with new heal value
        
        Debug.Log($"Applied HoT to {entityName}: {amount} healing for {duration} turns. Total turns: {hotTurns}");
        
        // Sync to network
        SyncToNetwork("HotTurns", hotTurns);
        SyncToNetwork("HotAmount", hotAmount);
        
        // Update UI
        gameManager.UpdateHealthUI();
    }
    
    public void ApplyCritBuff(int amount, int duration)
    {
        if (amount <= 0 || duration <= 0) return;
        
        critBuffs.Add(new CritBuff { amount = amount, turns = duration });
        
        Debug.Log($"Applied Crit Chance Buff to {entityName}: +{amount}% for {duration} turns.");
        
        // Sync to network (send total effective crit chance)
        SyncToNetwork("CritChance", GetEffectiveCritChance());
        
        // Update UI
        gameManager.UpdateHealthUI();
    }
    
    // Process Turn Start Effects
    
    public void ProcessTurnStartEffects()
    {
        // Apply DoT first
        ProcessDoT();
        
        // Apply HoT
        ProcessHoT();
        
        // Decrement status effect durations
        DecrementStatusEffects();
        
        // Decrement crit buff durations
        DecrementCritBuffs();
        
        // Update UI
        gameManager.UpdateHealthUI();
    }
    
    private void ProcessDoT()
    {
        if (dotTurns <= 0 || dotDamage <= 0) return;
        
        Debug.Log($"{entityName} DoT ticking for {dotDamage} damage.");
        TakeDamage(dotDamage, false);
        
        dotTurns--; // Decrement after applying damage
        if (dotTurns == 0) dotDamage = 0; // Clear damage if duration ends
        
        // Sync to network
        SyncToNetwork("DotTurns", dotTurns);
        SyncToNetwork("DotDamage", dotDamage);
    }
    
    private void ProcessHoT()
    {
        if (hotTurns <= 0 || hotAmount <= 0) return;
        
        Debug.Log($"{entityName} HoT ticking for {hotAmount} healing.");
        Heal(hotAmount);
        
        hotTurns--; // Decrement after applying healing
        if (hotTurns == 0) hotAmount = 0; // Clear amount if duration ends
        
        // Sync to network
        SyncToNetwork("HotTurns", hotTurns);
        SyncToNetwork("HotAmount", hotAmount);
    }
    
    private void DecrementStatusEffects()
    {
        bool weakChanged = weakTurns > 0;
        bool breakChanged = breakTurns > 0;
        
        if (weakTurns > 0) weakTurns--;
        if (breakTurns > 0) breakTurns--;
        // Thorns doesn't decay per turn typically
        if (strength > 0) strength--; // Strength decays per turn
        
        // Sync changes to network if needed
        if (weakChanged) SyncToNetwork("WeakTurns", weakTurns);
        if (breakChanged) SyncToNetwork("BreakTurns", breakTurns);
        // Strength changes each turn if > 0
        if (strength >= 0) SyncToNetwork("Strength", strength);
        
        Debug.Log($"Decremented {entityName} Status Effects. Weak: {weakTurns}, Break: {breakTurns}, Thorns: {thorns}, Strength: {strength}");
    }
    
    public void DecrementCritBuffs()
    {
        bool changed = false;
        
        for (int i = critBuffs.Count - 1; i >= 0; i--)
        {
            CritBuff buff = critBuffs[i];
            buff.turns--;
            
            if (buff.turns <= 0)
            {
                critBuffs.RemoveAt(i);
                changed = true;
            }
            else
            {
                critBuffs[i] = buff; // Update
            }
        }
        
        // Sync changes to network if needed
        if (changed)
        {
            SyncToNetwork("CritChance", GetEffectiveCritChance());
        }
    }
    
    // Energy methods
    
    public void AddEnergy(int amount)
    {
        if (amount <= 0) return;
        
        energy += amount;
        Debug.Log($"Added {amount} energy to {entityName}. Total: {energy}");
        
        // Sync changes to network
        SyncToNetwork("Energy", energy);
        
        // Update UI
        gameManager.UpdateEnergyUI();
    }
    
    public void ConsumeEnergy(int amount)
    {
        if (amount <= 0) return;
        
        energy = Mathf.Max(0, energy - amount);
        Debug.Log($"Consumed {amount} energy for {entityName}. Remaining: {energy}");
        
        // Sync changes to network
        SyncToNetwork("Energy", energy);
        
        // Update UI
        gameManager.UpdateEnergyUI();
    }
    
    public void ResetEnergy()
    {
        energy = maxEnergy;
        Debug.Log($"Reset energy for {entityName} to {energy}");
        
        // Sync changes to network
        SyncToNetwork("Energy", energy);
        
        // Update UI
        gameManager.UpdateEnergyUI();
    }
    
    // Helper methods
    
    private void SyncToNetwork(string propertyName, int value)
    {
        if (!isLocallyOwned || !PhotonNetwork.InRoom) return;
        
        if (propertyMappings.TryGetValue(propertyName, out string networkPropName))
        {
            Hashtable props = new Hashtable { { networkPropName, value } };
            ownerPlayer.SetCustomProperties(props);
            //Debug.Log($"Synced {propertyName}={value} to network as {networkPropName}");
        }
    }
    
    private GameObject GetUIAreaForEntity()
    {
        CombatUIManager uiManager = gameManager.GetCombatUIManager();
        if (uiManager == null) return null;
        
        switch (entityType)
        {
            case EntityType.LocalPlayer:
                return uiManager.GetPlayerUIArea();
            case EntityType.LocalPet:
                return uiManager.GetOwnPetUIArea();
            case EntityType.OpponentPet:
                return uiManager.GetOpponentPetUIArea();
            default:
                return null;
        }
    }
    
    private void HandleDeathCondition()
    {
        if (entityType == EntityType.LocalPlayer)
        {
            // Player died, handle combat loss
            gameManager.GetPlayerManager().GetCombatStateManager().HandleCombatLoss();
        }
        else if (entityType == EntityType.OpponentPet)
        {
            // Opponent pet died, handle combat win
            gameManager.GetPlayerManager().GetCombatStateManager().HandleCombatWin();
        }
        // LocalPet death doesn't end combat typically
    }
    
    // Getters and setters
    
    public int GetHealth() => currentHealth;
    public void SetHealth(int value) { currentHealth = value; SyncToNetwork("Health", value); }
    
    public int GetMaxHealth() => maxHealth;
    public void SetMaxHealth(int value) { maxHealth = value; }
    
    public int GetBlock() => block;
    public void SetBlock(int value) { block = value; SyncToNetwork("Block", value); }
    
    public int GetEnergy() => energy;
    public void SetEnergy(int value) { energy = value; SyncToNetwork("Energy", value); }
    
    public int GetMaxEnergy() => maxEnergy;
    public void SetMaxEnergy(int value) { maxEnergy = value; }
    
    public int GetWeakTurns() => weakTurns;
    public void SetWeakTurns(int value) { weakTurns = value; SyncToNetwork("WeakTurns", value); }
    
    public int GetBreakTurns() => breakTurns;
    public void SetBreakTurns(int value) { breakTurns = value; SyncToNetwork("BreakTurns", value); }
    
    public int GetThorns() => thorns;
    public void SetThorns(int value) { thorns = value; SyncToNetwork("Thorns", value); }
    
    public int GetStrength() => strength;
    public void SetStrength(int value) { strength = value; SyncToNetwork("Strength", value); }
    
    public int GetDotDamage() => dotDamage;
    public int GetDotTurns() => dotTurns;
    
    public int GetHotAmount() => hotAmount;
    public int GetHotTurns() => hotTurns;
    
    public int GetEffectiveCritChance()
    {
        int baseCritChance = CombatCalculator.BASE_CRIT_CHANCE;
        int bonusFromBuffs = 0;
        
        foreach (var buff in critBuffs)
        {
            bonusFromBuffs += buff.amount;
        }
        
        return Mathf.Max(0, baseCritChance + bonusFromBuffs);
    }
    
    public string GetName() => entityName;
    public EntityType GetEntityType() => entityType;
    public bool IsWeakened() => weakTurns > 0;
    public bool IsBroken() => breakTurns > 0;
}