using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

/// <summary>
/// Centralizes status effect management across all combat entities.
/// Provides standardized methods for applying and processing status effects.
/// </summary>
public class StatusEffectController
{
    private Dictionary<StatusEffectType, string> statusEffectNames = new Dictionary<StatusEffectType, string>
    {
        { StatusEffectType.None, "None" },
        { StatusEffectType.Weak, "Weak" },
        { StatusEffectType.Break, "Break" },
        { StatusEffectType.Thorns, "Thorns" },
        { StatusEffectType.Regeneration, "Regeneration" },
        { StatusEffectType.Strength, "Strength" }
    };
    
    private GameManager gameManager;
    private Dictionary<CombatEntity, Dictionary<StatusEffectType, int>> statusEffects = 
        new Dictionary<CombatEntity, Dictionary<StatusEffectType, int>>();
    
    public StatusEffectController(GameManager gameManager)
    {
        this.gameManager = gameManager;
    }
    
    public void Initialize()
    {
        statusEffects.Clear();
    }
    
    public void RegisterEntity(CombatEntity entity)
    {
        if (!statusEffects.ContainsKey(entity))
        {
            statusEffects[entity] = new Dictionary<StatusEffectType, int>();
            foreach (StatusEffectType type in System.Enum.GetValues(typeof(StatusEffectType)))
            {
                statusEffects[entity][type] = 0;
            }
        }
    }
    
    public void ApplyStatusEffect(CombatEntity entity, StatusEffectType type, int value, bool notifyNetworkIfNeeded = true)
    {
        if (type == StatusEffectType.None || value <= 0) return;
        
        RegisterEntity(entity);
        
        // Update the status effect value
        int currentValue = statusEffects[entity][type];
        int newValue = 0;
        
        switch (type)
        {
            case StatusEffectType.Weak:
            case StatusEffectType.Break:
            case StatusEffectType.Regeneration:
                // For duration-based effects, add the duration
                newValue = currentValue + value;
                break;
                
            case StatusEffectType.Thorns:
            case StatusEffectType.Strength:
                // For stack-based effects, add the value
                newValue = currentValue + value;
                break;
        }
        
        statusEffects[entity][type] = newValue;
        
        Debug.Log($"Applied {statusEffectNames[type]} to {entity.GetName()} with value {value}. New total: {newValue}");
        
        // Update the entity with the new status
        entity.ApplyStatusEffect(type, value);
        
        // Notify the network if needed and if entity is locally owned
        if (notifyNetworkIfNeeded && entity.GetEntityType() != CombatEntity.EntityType.OpponentPet && PhotonNetwork.InRoom)
        {
            // Notification happens inside entity.ApplyStatusEffect via property sync
        }
        
        // Update UI
        gameManager.UpdateHealthUI();
    }
    
    public void ProcessTurnStartEffects(CombatEntity entity)
    {
        entity.ProcessTurnStartEffects();
    }
    
    public int GetStatusEffectValue(CombatEntity entity, StatusEffectType type)
    {
        if (!statusEffects.ContainsKey(entity) || !statusEffects[entity].ContainsKey(type))
        {
            return 0;
        }
        
        return statusEffects[entity][type];
    }
    
    public bool HasStatusEffect(CombatEntity entity, StatusEffectType type)
    {
        return GetStatusEffectValue(entity, type) > 0;
    }
    
    public void ResetStatusEffects(CombatEntity entity)
    {
        if (!statusEffects.ContainsKey(entity)) return;
        
        foreach (StatusEffectType type in System.Enum.GetValues(typeof(StatusEffectType)))
        {
            statusEffects[entity][type] = 0;
        }
        
        // Update the entity with reset statuses
        switch (entity.GetEntityType())
        {
            case CombatEntity.EntityType.LocalPlayer:
                entity.SetWeakTurns(0);
                entity.SetBreakTurns(0);
                entity.SetThorns(0);
                entity.SetStrength(0);
                break;
                
            case CombatEntity.EntityType.LocalPet:
                entity.SetWeakTurns(0);
                entity.SetBreakTurns(0);
                entity.SetThorns(0);
                entity.SetStrength(0);
                break;
                
            case CombatEntity.EntityType.OpponentPet:
                // Cannot directly set opponent pet status
                break;
        }
        
        // Update UI
        gameManager.UpdateHealthUI();
    }
    
    public void ResetAllStatusEffects()
    {
        foreach (var entity in statusEffects.Keys)
        {
            ResetStatusEffects(entity);
        }
        
        // Force UI update
        gameManager.UpdateHealthUI();
    }
}